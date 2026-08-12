using System.Text.Json;
using ETS2LA.Backend;
using ETS2LA.Backend.Plugins;
using ETS2LA.Logging;

// Replicates ETS2LA's plugin loading pipeline against the built dist DLLs:
//   1. write the plugin DLLs into ~/.local/share/ETS2LA/{Plugins,Libraries}/<id>/
//   2. register them in ~/.config/ETS2LA/InstalledPluginManifest.json
//   3. PluginHandler.LoadLibraries() + LoadPlugins() + EnablePlugin(id)
// Prints which plugins were discovered, loaded and enabled.

string? rootArg = args.Length > 0 ? args[0] : null;
if (rootArg == null)
{
    Console.WriteLine("Usage: dotnet run -- <ETS2LA plugin root> [plugin dll] [library dll] [library dll]");
    return 1;
}

string pluginRoot = rootArg;
Busy();

string pluginDll = args.Length > 1 ? args[1] : "/root/ets2la-i18n/dist/Plugins/srlily.i18n/srlily.i18n.dll";
string libraryDll = args.Length > 2 ? args[2] : "/root/ets2la-i18n/dist/Libraries/srlily.i18n.library/srlily.i18n.library.dll";

string pluginsDir = Path.Combine(pluginRoot, "Plugins", "srlily.i18n");
string librariesDir = Path.Combine(pluginRoot, "Libraries", "srlily.i18n.library");
Directory.CreateDirectory(pluginsDir);
Directory.CreateDirectory(librariesDir);
File.Copy(pluginDll, Path.Combine(pluginsDir, "srlily.i18n.dll"), true);
File.Copy(libraryDll, Path.Combine(librariesDir, "srlily.i18n.library.dll"), true);

// Register in the manifest, mirroring RegisterPlugins.ps1/.sh
var configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ETS2LA");
Directory.CreateDirectory(configDir);
string manifestFile = Path.Combine(configDir, "InstalledPluginManifest.json");

InstalledPluginManifest manifest;
if (File.Exists(manifestFile))
{
    try { manifest = JsonSerializer.Deserialize<InstalledPluginManifest>(File.ReadAllText(manifestFile))!; }
    catch { manifest = new InstalledPluginManifest(); }
}
else manifest = new InstalledPluginManifest();

manifest.InstalledPlugins.RemoveAll(p => p.Id == "srlily.i18n" || p.Id == "srlily.i18n.library");
manifest.InstalledPlugins.Add(new InstalledPlugin
{
    Id = "srlily.i18n.library",
    Version = "1.1.3",
    DllPath = Path.Combine(librariesDir, "srlily.i18n.library.dll"),
    Dependencies = new List<string>(),
    Type = PluginType.Library
});
manifest.InstalledPlugins.Add(new InstalledPlugin
{
    Id = "srlily.i18n",
    Version = "1.1.3",
    DllPath = Path.Combine(pluginsDir, "srlily.i18n.dll"),
    Dependencies = new List<string> { "srlily.i18n.library" },
    Type = PluginType.Plugin
});
File.WriteAllText(manifestFile, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Manifest written: {manifestFile}");

var handler = new PluginHandler();
handler.LoadLibraries();
handler.LoadPlugins();

Console.WriteLine($"--- Loaded libraries ({handler.LoadedLibraryPlugins.Count}):");
foreach (var lib in handler.LoadedLibraryPlugins)
    Console.WriteLine($"    {lib.Info.Id} ({lib.Info.Name})");

Console.WriteLine($"--- Loaded plugins ({handler.LoadedPlugins.Count}):");
foreach (var plugin in handler.LoadedPlugins)
    Console.WriteLine($"    {plugin.Info.Id} ({plugin.Info.Name})");

var target = handler.LoadedPlugins.FirstOrDefault(p => p.Info.Id == "srlily.i18n");
if (target == null)
{
    Console.WriteLine("!!! srlily.i18n was NOT loaded. See errors above.");
    return 1;
}

bool enabled = handler.EnablePlugin(target);
Console.WriteLine(enabled
    ? $"srlily.i18n enabled: {target._IsRunning}"
    : "!!! srlily.i18n could not be enabled, see errors above.");
return enabled ? 0 : 1;

static void Busy() { }
