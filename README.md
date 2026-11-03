# WorldBox 综合国力排行榜 (Comprehensive Power Ranking)

给 WorldBox 新增一个独立的「综合国力排行榜」，综合评估世界中每个文明国家的整体实力（军队、经济、人口、城市、名望、领土）。

## 安装（二选一）

**方式 A：使用游戏自带模组加载器（无需额外工具）**

1. 打开游戏，进入「设置」，开启「实验模式（Experimental Mode）」。
2. 把 `PowerRanking_memload.dll` 放到游戏目录下的：
   `worldbox_Data\StreamingAssets\mods\`
3. 载入一个世界，按 `F8` 打开排行榜。

**方式 B：如果已安装 NeoModLoader（NML）**

NML 与游戏自带加载器共存，同样按方式 A 放置即可，无需额外操作。

## 使用

- 按 `F8` 打开/关闭排行榜。
- 主列表显示：排名、国家名称、综合国力分数。
- 点击某一行，顶部显示该国的军队、经济、人口、城市、名望、领土详情。
- 每 3 秒自动刷新；拖动窗口标题文字可移动窗口。

## 依赖

- 仅需正版 WorldBox，且开启「实验模式」。
- 不依赖其他模组、不依赖 NeoModLoader、不依赖任何外部 DLL（本模组为单一自包含 DLL）。

## 自定义权重（可选）

把 `PowerRanking.weights.txt` 放到 `worldbox_Data\StreamingAssets\mods\`，内容示例：

```text
military=25
economy=25
population=15
cities=10
renown=10
territory=15
```

数值无需总和为 100，会自动归一化。文件缺失时使用默认值。

## 兼容性

- 基于 WorldBox（Unity 2022.3.60f1，Steam 版）的公开游戏 API 编写。
- 不修改、不替换、不删除任何游戏文件或存档。
- 仅读取数据并新建独立窗口，不影响原版 `list_kingdoms` 排行榜。

## 发布说明（重要）

WorldBox 的 **Steam 创意工坊只支持地图/世界，不支持代码模组（DLL）**，因此本模组不能上传到创意工坊。推荐发布渠道：

- GitHub Release
- Nexus Mods
- Thunderstore（WorldBox 分区）
- 社区模组站 / 网盘

上传时只需包含本目录内的文件（不含源码与开发工具）。
