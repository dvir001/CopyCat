namespace PluralKit.Core;

public static class BuildInfoService
{
    public static string Version { get; private set; }
    public static string FullVersion { get; private set; }
    public static string Timestamp { get; private set; }
    public static bool IsDev { get; private set; }

    public static async Task LoadVersion()
    {
        using var stream = typeof(BuildInfoService).Assembly.GetManifestResourceStream("version");
        if (stream == null) throw new Exception("missing version information");

        using var reader = new StreamReader(stream);
        var data = (await reader.ReadToEndAsync()).Split("\n");

        FullVersion = data.Length > 0 && data[0].Length > 0 ? data[0] : "dev";
        Timestamp = data.Length > 1 ? data[1] : "0";

        IsDev = data.Length < 3 || data[2] == "";

        // show only short commit hash to users
        Version = FullVersion.Length >= 7 ? FullVersion.Remove(7) : FullVersion;
    }
}