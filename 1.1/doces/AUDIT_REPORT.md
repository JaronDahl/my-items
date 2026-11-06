# PowerRanking 1.0 代码审计报告

- 审计对象：`PowerRankingMod`（NeoModLoader 模组）
- 审计方式：只读静态代码审查，未运行游戏，未修改任何文件
- 审计文件：`src/CountryScore.cs`、`src/DragPanel.cs`、`src/PowerRankingEngine.cs`、`src/PowerRankingMod.cs`、`src/PowerRankingUI.cs`、`src/Weights.cs`、`mod.json`

> 标注「待验证」的项目表示：静态代码无法确认其真实运行时行为，必须在真实游戏环境验证后才能下结论。

---

## 1. 总体风险评估

**总体风险：中等。**

- 未发现会导致存档损坏、游戏崩溃或数据被覆盖的确定性缺陷。
- 主要风险集中在：大世界下的 CPU/GC 压力、刷新与 UI 重建的浪费、缺少异常隔离，以及对原版 UI 内部结构（`list_kingdoms` 预制体 + 反射 `create`）的强耦合带来的版本脆弱性。
- 模组不写存档、不修改原版对象（仅实例化预制体副本）、不使用 Harmony，数据与全局状态安全风险低。
- 单例结构（一个 `PowerRankingMod` + 一个窗口）在正常加载下成立；F8 与窗口可见性门控设计基本合理。

---

## 2. 发现的问题（按严重程度排序）

### 中（Medium）

#### M1 — 刷新链路缺少异常隔离，单个异常会中断整次刷新

- 位置：`PowerRankingEngine.Compute()`（`src/PowerRankingEngine.cs:10-51`）、`PowerRankingUI.RefreshNow()`（`src/PowerRankingUI.cs:48-67`）
- 原因：`Compute()` 没有任何 `try/catch`；它对每个国家调用 `countTotalWarriors()`、`getUnits()`、`countCities()`、`countZones()`、`getRenown()`、`getPopulationPeople()`。若某个国家处于「濒死/待移除/字段为 null」的边界状态导致任一调用抛异常，异常会沿 `RefreshNow → Update` 一路冒泡到 Unity 主线程。
- 影响：Unity 会捕获并打印异常（每 3 秒一次），但整次刷新会中断：`RebuildRows()`/`UpdateDetail()` 不会执行，列表停留在旧状态或部分状态，且日志被反复刷屏。不会崩游戏，但会影响 UI 一致性与日志可读性。
- 推荐修复：在 `RefreshNow()` 内包 `try/catch`，单国异常时跳过该国继续排名；对 `kingdom.data == null` 等情况做防御。
- 待验证：游戏是否会在移除国家时让 `World.world.kingdoms` 短暂包含已销毁对象。

#### M2 — 每次刷新对城市/单位做多趟冗余遍历

- 位置：`PowerRankingEngine.Compute()`（`src/PowerRankingEngine.cs:29-34`）、`CountEconomy()`（`:56-64`）
- 原因：每个国家依次调用 `countTotalWarriors()`（遍历城市）、`countZones()`（遍历城市）、`countCities()`（脏时遍历城市）、`getPopulationPeople()`（遍历城市，必要时再遍历单位）、`CountEconomy()`（遍历单位）。同一批城市被遍历约 4 次、单位遍历 1~2 次。
- 影响：时间与数据规模成线性，但常数较大；在大世界（多国家、多城市、多单位）下，3 秒一次的全量重算可能造成可感知的卡顿（帧尖峰）。
- 推荐修复：按国家一次遍历城市，同时累计「城市数、领土(zone 数)、军队(warrior 数)、人口」，再单独遍历单位累计经济，把指标收集合并成 1~2 趟。

#### M3 — 每次刷新、每次点击都全量销毁并重建 UI 行

- 位置：`PowerRankingUI.RebuildRows()`（`src/PowerRankingUI.cs:190-209`）、`Select()`（`:277-286`）、`MakeRow()`（`:220-239`）
- 原因：刷新（每 3 秒）和点击某行（`Select`）都会销毁所有行（`index>=1`）再重建全部行；每行是 1 个 `GameObject` + 多个组件（`RectTransform`、`LayoutElement`、`Image`、`Button`、`HorizontalLayoutGroup`、3 个 cell 各含 `LayoutElement` + `Text`），还附带 `onClick` 闭包分配。
- 影响：高频 `GameObject`/组件分配与 `Destroy`（延迟到帧末），产生明显的 GC 压力与布局重建成本；国家很多时尤其明显。点击单行只是为了高亮一行，却重建了整个列表。
- 推荐修复：复用行对象，刷新时仅更新 `Text.text` 与高亮；`Select` 只更新目标行的高亮，不重建列表。

#### M4 — 窗口关闭按钮（右上角 X）很可能无法正确关闭本窗口（待验证）

- 位置：`PowerRankingUI.BuildWindow()`（`src/PowerRankingUI.cs:78-137`），关闭按钮来自 `list_kingdoms` 预制体，未做显式绑定
- 原因：窗口是手动 `Object.Instantiate` 到 `CanvasMain.instance.transformWindows`，没有注册进 `ScrollWindow` 的窗口栈。预制体自带关闭按钮的 `onClick` 通常指向窗口栈/历史逻辑，而不是本模组的 `SetVisible(false)`。
- 影响：点击 X 可能无法关闭本窗口（甚至可能关闭/操作了别的窗口）；目前只有 `F8` 能可靠关闭。这直接影响用户体验。
- 推荐修复：找到本窗口实例的关闭按钮并显式 `onClick` 绑定到 `SetVisible(false)`；或移除/覆盖原关闭按钮。
- 待验证：必须实测 X 按钮行为。

#### M5 — 强耦合原版 UI 内部结构 + 反射调用内部方法，版本脆弱

- 位置：`PowerRankingUI.BuildWindow()`（`src/PowerRankingUI.cs:71-114`）
- 原因：依赖 `Resources.Load("windows/list_kingdoms")` 预制体，剥离 `ListWindow`/`SortingTab`/`KingdomListComponent`，并反射调用 `ScrollWindow.create`（`internal`）来阻止首帧滑出。
- 影响：一旦游戏更新重命名/移除这些组件或 `create` 方法，模组可能加载失败、窗口滑出屏幕或表现异常。反射 `create` 若返回 null 不会崩溃，但窗口会因 `Start()` 被滑出屏幕。
- 推荐修复：为反射失败提供降级路径（例如自建 `LateUpdate` 校正位置）；尽量减少对内部组件的假设，优先使用公开 API。
- 待验证：当前版本下 `create(false)` 反射是否在所有加载阶段都安全；`ListWindow.Awake()` 在实例化时是否有其他抛出点。

---

### 低（Low）

#### L1 — 单国家/零差异时归一化恒为 0.5，分数恒为 50.0

- 位置：`PowerRankingEngine.Normalize01()`（`src/PowerRankingEngine.cs:137-145`）
- 原因：当某指标所有国家相同（或只有 1 个国家）时返回中性 0.5，导致每个指标都按 0.5×权重计入；只有 1 个国家时总分恒为 50.0。
- 影响：排名顺序不受影响（大家都一样），但绝对分数失真；与最初「零值贡献 0」的预期不一致。
- 推荐修复：明确单国/无差异时的分数语义（例如直接显示 100 或不显示分数），或在文档中说明。

#### L2 — 权重文件未清洗负值，可能产生负数或 >100 的分数

- 位置：`Weights.Load()`（`src/Weights.cs:50-63`）、`Weights.Normalize()`（`:74-96`）
- 原因：`Normalize` 只处理「总和 ≤ 0」的情况；若出现 `military=-0.5, economy=1.5` 这类组合（总和仍 >0），归一化后保留负权重，最终分数可能为负或超过 100。
- 影响：仅当用户手改权重文件写入负值或异常值时触发，默认值无此问题。
- 推荐修复：`Normalize` 前对每个权重做 `Mathf.Max(0, v)` 截断。

#### L3 — 无 OnDestroy 清理，模组重载会泄漏窗口对象

- 位置：`PowerRankingMod` / `PowerRankingUI`（无 `OnDestroy`）
- 原因：窗口 `GameObject` 挂在 `CanvasMain.instance.transformWindows` 下，不是模组 `GameObject` 的子节点；若 NML 重载模组，旧窗口不会被销毁。
- 影响：重载后出现旧窗口残留（罕见，通常只在开发期触发）。
- 推荐修复：在 `OnDestroy`/`OnDisable` 中销毁 `window.gameObject`。

#### L4 — FolderPath 为空时 Path.Combine 抛异常

- 位置：`PowerRankingMod.OnModLoad()`（`src/PowerRankingMod.cs:17-18`）
- 原因：`Path.Combine(GetDeclaration().FolderPath, ...)` 若 `FolderPath` 为 null 会抛 `ArgumentNullException`，导致模组加载失败。
- 影响：正常发布/安装路径下 `FolderPath` 非空；仅异常安装场景触发。
- 推荐修复：对 `FolderPath` 判空，回退到 `Application.streamingAssetsPath` 或默认权重。

#### L5 — F8 快捷键可能与其他模组冲突

- 位置：`PowerRankingMod.Update()`（`src/PowerRankingMod.cs:37`）
- 原因：F8 是硬编码轮询按键，未提供配置项；其他模组（含 NML 自身功能）也可能占用 F8。
- 影响：按键冲突导致行为异常，但不影响数据。
- 推荐修复：把快捷键做成可配置项（写入 weights/config 文件）。
- 待验证：与 NML 及其他模组的按键占用情况。

#### L6 — 长国名可能横向溢出并重叠「分数」列

- 位置：`PowerRankingUI.MakeText()`（`src/PowerRankingUI.cs:325`）、`MakeRow()`（`:237-238`）
- 原因：所有文本 `horizontalOverflow = Overflow`，国名列弹性宽度但不会换行；极长国名会超出自身列宽，可能覆盖右侧「分数」。
- 影响：纯视觉问题。
- 推荐修复：对国名列用 `Wrap`/截断，或限制显示长度。

#### L7 — DragPanel.canvas 字段未使用（死代码）

- 位置：`DragPanel.cs:10,15`
- 原因：`canvas` 被赋值但从未使用。
- 影响：无功能影响，仅代码整洁问题。

---

## 3. 计算模型审计（要点）

- 指标重复计算：见 M2（城市/单位多趟遍历），属性能问题而非正确性问题。
- 溢出：`economy` 用 `long` 累计、`LogScale` 转 `double` 取对数，当前不会溢出；`military/population/cities/renown/territory` 为 `int`，在游戏实际数值量级下安全。
- 零值/负值：`LogScale` 将负值截为 0（`PowerRankingEngine.cs:117-120`），安全；权重负值见 L2。
- 单指标主导：默认权重最高 25%，且 log+min-max 有界，单指标无法主导排名；仅当用户把某权重设为 1.0 时才会（属显式配置）。
- 归一化合理性：min-max 是「相对排名」，某国分数会因其他国家变化而变化（即使自身数据不变），这是算法特性而非缺陷；无差异时返回 0.5 见 L1。

---

## 4. 测试建议（不破坏存档）

静态审查无法替代真实运行，以下为建议的运行时验证项：

1. 小型世界（极少国家）：验证正常显示、单国分数语义。
2. 大型世界 + 大量国家：观察 F8 打开、3 秒刷新、滚动与点击是否卡顿（验证 M2/M3）。
3. 国家频繁战争/扩张：观察指标与排名变化是否稳定，有无异常。
4. 国家灭亡/合并：观察列表是否自动增减、点击已灭国家是否异常（验证 M1）。
5. 频繁开关排行榜：观察窗口数量是否增加、旧窗口是否残留（验证 L3、窗口是否重复创建）。
6. 长时间运行：观察内存/GC 是否持续增长。
7. 多次加载不同存档：观察跨存档是否有残留状态。
8. 点击右上角 X：确认能否正确关闭本窗口（验证 M4）。
9. 与其他模组共存：确认 F8 无冲突。

> 以上均为真实运行测试项，本次仅完成静态审查，未声称测试通过。

---

## 5. 修复优先级建议

| 优先级 | 编号 | 说明 |
| --- | --- | --- |
| P1 | M4 | 关闭按钮失效（直接体验问题，需先实测确认） |
| P1 | M1 | 刷新异常隔离（防止日志刷屏与 UI 状态错乱） |
| P2 | M3 | UI 行复用，降低 GC 与重建成本 |
| P2 | M2 | 合并城市/单位遍历，降低大世界重算成本 |
| P2 | M5 | 降低对原版内部结构的耦合/反射脆弱性 |
| P3 | L1/L2 | 计算语义与权重清洗 |
| P3 | L3/L4/L5/L6/L7 | 清理、防御与细节 |
