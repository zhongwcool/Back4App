using System.Diagnostics;
using System.IO;

namespace Versioner.Helpers;

public static class VersionFile
{
    public static bool TryRead(string path, out string version, out string error)
    {
        version = "";
        error = "";
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            error = "找不到这个文件。";
            return false;
        }

        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            if (info.FileMajorPart == 0
                && info.FileMinorPart == 0
                && info.FileBuildPart == 0
                && info.FilePrivatePart == 0)
            {
                if (!VersionNumber.TryNormalize(info.FileVersion, out _, out version))
                {
                    error = "这个文件没有可用的文件版本。";
                    return false;
                }

                return true;
            }

            version = new Version(
                Math.Max(info.FileMajorPart, 0),
                Math.Max(info.FileMinorPart, 0),
                Math.Max(info.FileBuildPart, 0),
                Math.Max(info.FilePrivatePart, 0)).ToString();
            return true;
        }
        catch (Exception)
        {
            error = "无法读取文件版本。";
            return false;
        }
    }
}
