# 托盘管理 · TrayManager

根据个人审美和使用习惯制作的 Windows 11 托盘管理工具，使用 WinUI 3、Mica 和圆角卡片。

> **个人原创的独立项目。** 根据自己的审美和使用习惯从头制作，不是其他软件的修改版或 Fork，没有对应的“官方原版”。目前仍处于实验阶段，使用前请了解下方限制。与 Microsoft、Windows、PowerToys 及被管理的应用没有隶属或背书关系。

## 下载

前往 [Releases](https://github.com/Tomclanc/TrayManager/releases) 下载 x64 ZIP。解压运行 `TrayManager.exe`，或在 PowerShell 中运行包内的 `Install.ps1`，安装到当前用户目录并创建开始菜单入口。压缩包并非独立运行库全集，需要 .NET 10 Runtime 和 Windows App Runtime 2。

## 功能

- 列出可安全定位的实时托盘图标，搜索后按项隐藏／恢复。
- 不结束应用或后台服务，每 5 秒重新应用隐藏规则。
- 可隐藏本工具图标；双击自身图标打开窗口，不提供右键菜单。
- 单实例，重复启动唤回已有窗口；关闭窗口后继续后台运行。
- 跟随系统深浅色，支持 DPI 缩放。

## 重要限制

开关表示本工具的隐藏规则，不是系统原有隐藏状态；列表不保证包含全部图标。已被其他工具隐藏的图标不应重复管理。Windows 更新、应用重建图标或异常退出可能影响效果。

**1.0.2 尚无正式卸载器或卸载时自动恢复功能。** 删除程序前请点击“退出程序”，它会尝试恢复本次隐藏的图标。直接删除文件夹或强制结束进程不能保证恢复；无法定位时可重启对应应用。请勿把这一版理解为已实现自动卸载恢复。

详见 [使用说明](TrayManager/README.md)。

## 构建和测试

在 Windows 上安装 .NET 10 SDK 后，从仓库根目录执行：

```powershell
dotnet run --project TrayManager.Tests/TrayManager.Tests.csproj -c Release
./TrayManager/Build.ps1
```

测试会短暂创建一个专用托盘图标，检查身份校验、隐藏、恢复及缓存序列化，不修改其他应用图标。构建生成 `outputs` 内的程序目录和 ZIP。

源码不包含个人设置、日志、令牌或本机托盘扫描结果。
