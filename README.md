# sts2AllowUserPause（死亡前暂停 / DeathGrace）

一个用于 **Slay the Spire 2** 的小型 Mod。

当前适配游戏版本：**v0.113**。Mod 文件名保持稳定，不包含游戏版本号。

它会在玩家即将死亡时弹出一个决策页面，避免游戏直接结算，让玩家可以选择：

- 确认死亡并进入正常结算
- 回到当前层开始时的存档状态

## 项目说明

- 主要逻辑在 [DeathGraceController.cs](DeathGraceController.cs)
- 弹窗 UI 在 [DeathDecisionPopup.cs](DeathDecisionPopup.cs)
- 本地化文本位于 `localization/eng/main_menu_ui.json` 和 `localization/zhs/main_menu_ui.json`

## 快速使用

从 [Releases](https://github.com/kazeokamali/sts2AllowUserPause/releases/latest) 下载 `sts2AllowUserPause.zip`，将其中的 DLL、JSON 和 PCK 放入游戏的 `mods/sts2AllowUserPause` 目录。也可以通过 [Steam 创意工坊](https://steamcommunity.com/sharedfiles/filedetails/?id=3747572624)订阅。

曾手动安装 `sts2AllowUserPause_v0.107` 的玩家，请先将旧目录移出 `mods`，避免新旧两个 ID 同时加载。只通过工坊订阅的玩家无需退订重订。

## 构建

在仓库根目录执行：

```powershell
dotnet build sts2AllowUserPause.csproj
```

项目当前会按 `sts2AllowUserPause.csproj` 中的配置，将产物复制到本地 STS2 的 `mods/sts2AllowUserPause` 目录。

## 说明与声明

- 本仓库仅用于**非商业**的学习与交流
- 游戏本体及相关资源版权归原作者 / 原版权方所有
- 本项目与官方无关
