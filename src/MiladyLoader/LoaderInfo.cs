namespace MiladyLoader;

public static class LoaderInfo
{
    public const string Name = "Milady Bruisers Mod Loader";
    public const string Version = "0.2.0";
    public const string GameFolderName = "Bruisers 2D Boxing";
    public const string GameExe = "Bruisers 2D Boxing.exe";

    /// <summary>Comment lines the loader wraps around each mod's code, so it can be found later.</summary>
    public static string BeginMarker(string modId) => $"// [Milady] {modId} BEGIN";
    public static string EndMarker(string modId) => $"// [Milady] {modId} END";
}
