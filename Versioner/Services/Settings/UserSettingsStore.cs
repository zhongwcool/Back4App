using System.IO;
using System.Text.Json;
using Versioner.Common;
using Versioner.Options;

namespace Versioner.Services.Settings;

public static class UserSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static Back4AppOptions Load()
    {
        if (File.Exists(AppPaths.SettingsPath))
            return Normalize(TryReadSettings(AppPaths.SettingsPath) ?? new Back4AppOptions());

        var legacy = TryReadLegacy(Path.Combine(AppContext.BaseDirectory, "appsettings.local.json"))
                     ?? TryReadLegacy(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));
        var options = Normalize(legacy ?? new Back4AppOptions());
        if (!options.IsConfigured)
            return options;

        try
        {
            Write(options);
        }
        catch (IOException)
        {
            // 下次保存时再写入本机配置。
        }

        return options;
    }

    public static void Save(Back4AppOptions target, Back4AppOptions incoming)
    {
        target.ApplicationId = incoming.ApplicationId.Trim();
        target.MasterKey = incoming.MasterKey.Trim();
        target.ServerUrl = string.IsNullOrWhiteSpace(incoming.ServerUrl)
            ? "https://parseapi.back4app.com"
            : incoming.ServerUrl.Trim();
        AppPaths.EnsureCreated();
        Write(target);
    }

    private static void Write(Back4AppOptions options)
    {
        var path = AppPaths.SettingsPath;
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(options, JsonOptions));
        File.Move(temp, path, overwrite: true);
    }

    private static Back4AppOptions Normalize(Back4AppOptions options)
    {
        options.ApplicationId = options.ApplicationId?.Trim() ?? "";
        options.MasterKey = options.MasterKey?.Trim() ?? "";
        options.ServerUrl = string.IsNullOrWhiteSpace(options.ServerUrl)
            ? "https://parseapi.back4app.com"
            : options.ServerUrl.Trim();
        return options;
    }

    private static Back4AppOptions? TryReadSettings(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<Back4AppOptions>(File.ReadAllText(path), JsonOptions)
                   ?? new Back4AppOptions();
        }
        catch (JsonException)
        {
            return new Back4AppOptions();
        }
        catch (IOException)
        {
            return new Back4AppOptions();
        }
    }

    private static Back4AppOptions? TryReadLegacy(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!property.Name.Equals("Back4App", StringComparison.OrdinalIgnoreCase)
                    || property.Value.ValueKind != JsonValueKind.Object)
                    continue;

                return new Back4AppOptions
                {
                    ApplicationId = ReadString(property.Value, "ApplicationId"),
                    MasterKey = ReadString(property.Value, "MasterKey"),
                    ServerUrl = ReadString(property.Value, "ServerUrl")
                };
            }
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }

        return null;
    }

    private static string ReadString(JsonElement section, string name)
    {
        foreach (var property in section.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                && property.Value.ValueKind == JsonValueKind.String)
                return property.Value.GetString() ?? "";
        }

        return "";
    }
}
