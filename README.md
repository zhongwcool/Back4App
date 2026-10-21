# Versioner

基于 .NET 10 的 WPF 桌面应用，采用 Generic Host 依赖注入、CommunityToolkit.Mvvm 与 Material Design 3。

## 环境要求

- [.NET 10 SDK](https://dotnet.microsoft.com/download)（含 Windows Desktop）
- Windows 10 或更高版本

## 发布

单文件、非自包含（依赖目标机已安装 .NET 10 Desktop Runtime）、x64，输出到仓库根目录 `publish/`：

```bash
dotnet publish Versioner/Versioner.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

- `IncludeNativeLibrariesForSelfExtract=true`：把 SkiaSharp 的原生库 `libSkiaSharp.dll` 一并打进 exe，否则它会单独留在输出目录
- `appsettings.json` 不会打进 exe，需与 `Versioner.exe` 放在同一目录
- 本地存在 `appsettings.local.json` 时也会被复制到 `publish/`，分发前注意移除
