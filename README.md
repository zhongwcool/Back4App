# Versioner

基于 .NET 10 的 WPF 桌面应用，采用 Generic Host 依赖注入、CommunityToolkit.Mvvm 与 Material Design 3。

## 环境要求

- [.NET 10 SDK](https://dotnet.microsoft.com/download)（含 Windows Desktop）
- Windows 10 或更高版本

## 发布

单文件、非自包含（依赖目标机已安装 .NET 10 Desktop Runtime）、x64，输出到仓库根目录 `publish/`：

```bash
dotnet publish Versioner/Versioner.csproj -c Release -r win-x64 --self-contained false -o publish
```

发布结果只有 `Versioner.exe`。加载动画打进程序集；Back4App 的 Application Id 和 Master Key 写在 `%LocalAppData%\Versioner\settings.json`。没有 Master Key 时，启动会弹出配置窗口。
