# sts2AllowUserPause 杀戮尖塔2防止死亡直接结算Mod

一个用于 **Slay the Spire 2** 的小型 Mod。

它会在玩家即将死亡时弹出一个决策页面，避免游戏直接结算，让玩家可以选择：

- 确认死亡并进入正常结算
- 回到当前层开始时的存档状态

## 项目说明

- 主要逻辑在 [DeathGraceController.cs](D:/Coding/sts2mods/sts2AllowUserPause/DeathGraceController.cs:16)
- 弹窗 UI 在 [DeathDecisionPopup.cs](D:/Coding/sts2mods/sts2AllowUserPause/DeathDecisionPopup.cs:13)
- 本地化文本位于 `localization/eng/main_menu_ui.json` 和 `localization/zhs/main_menu_ui.json`

## 快速使用

release中有打包好的文件，解压后放在mods目录即可

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
