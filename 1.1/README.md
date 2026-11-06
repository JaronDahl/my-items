# WorldBox 综合国力排行榜 (Comprehensive Power Ranking)

一个 NeoModLoader（NML）模组，为 WorldBox 新增综合国力排行榜。按 `F8` 打开/关闭。

## 功能

- 六个维度：军事、经济、人口、领土、城市发展、国家影响力。
- 评分模型：非线性指标转换（对数 + 缩尾参考）+ 加权几何平均，体现六维协调程度。
- 主列表：排名 / 国家名称 / 综合国力分数，按分数从高到低排列。
- 点击某行可高亮选中；每 3 秒自动刷新。
- 权重可配置，默认 20/20/15/15/15/15，自动归一化。

## 安装

1. 先安装模组加载器 NeoModLoader（NML），见创意工坊「Mod Loader」分类，或 GitHub：https://github.com/WorldBoxOpenMods/ModLoader
2. 把整个 `PowerRanking` 文件夹放到游戏目录 `Mods\PowerRanking\`。
3. 启动游戏，载入一个世界。

## 使用

- 按 `F8` 打开/关闭排行榜。
- 右上角 X 可关闭窗口。
- 拖动窗口标题文字可移动窗口。

## 权重配置

编辑 `PowerRanking.weights.txt`（自动归一化，负值会被截断为 0）：

```text
military=20
economy=20
population=15
territory=15
development=15
influence=15
```

旧键 `cities`（=development）和 `renown`（=influence）仍兼容。

## 从源码编译

依赖：Windows 自带 `csc.exe`（.NET Framework 4.x），无需额外安装。

```powershell
powershell -ExecutionPolicy Bypass -File .\compile.ps1
```

编译产物会安装到 `worldbox\Mods\PowerRanking\`。

## 发布到 Steam 创意工坊

本模组通过 NML 游戏内「上传」功能发布到创意工坊：在 NML 模组列表中找到本模组，点击「上传」，填写 ChangeLog，完成 Steam 认证即可。通过认证为「Mod」，未认证为「Unverified Mod」。

## 依赖

- WorldBox（Steam 版）
- NeoModLoader（NML）

无其他依赖，无需 Harmony 或其他外部 DLL。
