// Local build shim for the BepInEx.PluginInfoProps-generated class (the NuGet analyzer isn't available in this
// no-SDK build environment). NOTE: this file is intentionally NOT committed to the repo — an SDK `dotnet build`
// generates an identical PluginInfo from the .csproj <Version>, so committing it would cause a duplicate type.
[assembly: System.Reflection.AssemblyVersion("1.0.8.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.8.0")]

internal static class PluginInfo
{
    public const string PLUGIN_GUID = "com.badwolf.thronefall_mp";
    public const string PLUGIN_NAME = "Thronefall Multiplayer";
    public const string PLUGIN_VERSION = "1.0.8";
}
