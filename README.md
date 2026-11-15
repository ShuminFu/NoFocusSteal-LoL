# NoFocusSteal-LoL

[NoFocusSteal](https://github.com/BoazCohenJ/NoFocusSteal) 1.4.0 的分支（MIT），当前版本 **1.4.0.3**。

这不是官方项目。原作者是 [BoazCohenJ](https://github.com/BoazCohenJ)。本仓库只在官方 1.4.0 上加了英雄联盟「游戏 / 大厅」两套客户端的焦点规则：不向游戏或大厅注入代码。

官方说明仍在 [docs/README.upstream.md](docs/README.upstream.md)。

## 这套规则做什么

大厅是 `LeagueClient.exe`、`LeagueClientUx.exe`、`LeagueClientUxRender.exe`。游戏是 `League of Legends.exe`。

**大厅可以到前台的情况：**

- 刚打完一把：游戏在约 15 秒内拿过焦点，或上一个窗口就是游戏
- 你从 WeGame 或大厅启动闪屏打开大厅
- 你点击了大厅窗口本身
- 你用 Alt / Win，或点任务栏 / 缩略图 / Alt+Tab（名称能对上大厅）切过去

**会拦截的情况：**

- 你在 Grok Bot 或其他窗口里时，大厅自己弹出来（在聊天里按 Enter / Tab / Esc 也不会被当成要切到大厅）
- 对局开始时，游戏窗口自己抢焦点（你再点击游戏或 Alt+Tab，仍然可以进去）

**打完出来点下一把：**

大厅弹出后会留在前台，方便你点下一把。GameViewer、Grok Bot 等允许名单里的程序，不能靠大厅出现之前的点击记录、残留按键，或大厅窗口闪一下，把焦点抢回去。

只有你在大厅已经在前台之后，再点击这些程序、点它们的任务栏按钮，或用 Alt+Tab 切过去，它们才会出来。你真正最小化或关掉大厅后，焦点可以交给别的窗口。

`trustInjectedInput` 保持 `false`：联盟客户端会伪造按键来抢焦点，那些不能算成你的操作。本机鼠标点击仍然算主动操作。用 UU 远程从另一台电脑控制时，远程点击可能不会被识别成主动操作。

## 推荐配置

把 [settings.example.ini](settings.example.ini) 复制到 `%APPDATA%\NoFocusSteal\settings.ini`。改文件前先退出 NoFocusSteal，否则正在运行的程序可能把文件写回去。

15 秒的「刚打完一把」宽限写在程序里（`LobbyAfterGameGraceMs`），不在 ini 里。

## 编译

需要 Windows，以及已安装的 .NET Framework 4.8 和 .NET SDK（本机用的是 SDK 7）。

```bat
dotnet test tests/NoFocusSteal.Tests -c Release
dotnet build src/NoFocusSteal -c Release
```

生成的 exe 在 `src\NoFocusSteal\bin\Release\net48\NoFocusSteal.exe`。和官方一样，建议用「最高权限」的登录计划任务、加上 `--tray` 运行，这样才能管到同样以管理员权限启动的窗口。不要再另外加一个开机启动项，以免开出第二个实例。

把本仓库的 `main` 打上 `v*` 标签时，GitHub Actions 会编译并发布一份 exe。

## 相对官方 1.4.0 的改动

- **1.4.0.1**：大厅不再因为普通按键（包括 Enter）就自动允许；只有点击大厅、Alt/Win、刚离开对局，或从 WeGame / 启动闪屏打开时，才让大厅到前台。
- **1.4.0.2**：任务栏 / 缩略图 / Alt+Tab 的点击如果能对上大厅名称，也算你要打开大厅；允许名单里的程序不能在大厅还在前台时自行把大厅挤走。
- **1.4.0.3**：离开大厅必须是大厅已经到前台之后的点击、任务栏切换或 Alt/Win；过期的点击和残留按键作废。大厅窗口还在时，不把一次短暂隐藏或重建当成你离开了大厅。你真正最小化或关掉大厅后，仍然会让出焦点。

## License

[MIT](LICENSE)，与上游相同。Copyright (c) 2026 BoazCohenJ。本仓库中的大厅 / 游戏规则改动按同一许可提供。
