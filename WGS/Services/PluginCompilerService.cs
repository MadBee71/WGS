using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using WGS.Games;

namespace WGS.Services;

/// <summary>
/// Compiles a .cs file at runtime and loads it as an IGamePlugin.
/// Works with single-file (framework-dependent) publish by loading
/// reference assemblies from the .NET shared runtime on disk.
/// </summary>
public static class PluginCompilerService
{
    public static (IGamePlugin? plugin, string error) CompileAndLoad(string csFilePath)
    {
        string source;
        try   { source = File.ReadAllText(csFilePath, System.Text.Encoding.UTF8); }
        catch (Exception ex) { return (null, $"Cannot read file: {ex.Message}"); }

        var references = BuildReferences();
        if (references.Count == 0)
            return (null, "Could not locate .NET runtime assemblies. Make sure .NET 8 Runtime is installed.");

        var syntaxTree  = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            assemblyName: Path.GetFileNameWithoutExtension(csFilePath),
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release));

        using var ms     = new MemoryStream();
        var       result = compilation.Emit(ms);

        if (!result.Success)
        {
            var errors = result.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.GetMessage())
                .Take(8);
            return (null, "Compile errors:\n" + string.Join("\n", errors));
        }

        // Write to a temp file so Defender sees a named DLL instead of a memory-only load
        var tmpDll = Path.Combine(Path.GetTempPath(),
            $"wgs_plugin_{Path.GetFileNameWithoutExtension(csFilePath)}_{Guid.NewGuid():N}.dll");
        try { File.WriteAllBytes(tmpDll, ms.ToArray()); }
        catch (Exception ex) { return (null, $"Cannot write temp assembly: {ex.Message}"); }

        System.Reflection.Assembly assembly;
        try   { assembly = System.Reflection.Assembly.LoadFrom(tmpDll); }
        catch (Exception ex) { return (null, $"Cannot load assembly: {ex.Message}"); }
        // Note: temp file stays on disk until WGS exits (file is locked by the loaded assembly)

        var pluginType = assembly.GetTypes()
            .FirstOrDefault(t => typeof(IGamePlugin).IsAssignableFrom(t)
                              && !t.IsAbstract && !t.IsInterface);

        if (pluginType == null)
            return (null, "No class implementing IGamePlugin found in the file.");

        try
        {
            var plugin = (IGamePlugin?)Activator.CreateInstance(pluginType);
            return plugin is null
                ? (null, "Failed to instantiate plugin instance.")
                : (plugin, string.Empty);
        }
        catch (Exception ex)
        {
            return (null, $"Failed to create plugin instance: {ex.Message}");
        }
    }

    private static List<MetadataReference> BuildReferences()
    {
        var refs = new List<MetadataReference>();

        // 1. Runtime assemblies (netcoreapp / net8.0)
        var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
        AddDllsFromDir(refs, runtimeDir, [
            "System.Runtime.dll",
            "System.Collections.dll",
            "System.Linq.dll",
            "System.Console.dll",
            "System.IO.dll",
            "System.Text.RegularExpressions.dll",
            "netstandard.dll",
            "mscorlib.dll",
        ]);

        // 2. WPF / Windows Desktop — look in shared frameworks
        var dotnetRoot = Path.GetFullPath(Path.Combine(runtimeDir, "..", "..", ".."));
        foreach (var fxName in new[] { "Microsoft.WindowsDesktop.App", "Microsoft.NETCore.App" })
        {
            var fxBase = Path.Combine(dotnetRoot, "shared", fxName);
            if (!Directory.Exists(fxBase)) continue;
            var best = Directory.GetDirectories(fxBase)
                .OrderByDescending(d => d)
                .FirstOrDefault();
            if (best != null) AddAllDlls(refs, best);
        }

        // 3. WGS own assemblies (extracted to temp dir by single-file host)
        var appBase = AppContext.BaseDirectory;
        AddAllDlls(refs, appBase);

        return refs;
    }

    private static void AddDllsFromDir(List<MetadataReference> refs, string dir, IEnumerable<string> names)
    {
        foreach (var name in names)
        {
            var path = Path.Combine(dir, name);
            if (File.Exists(path)) refs.Add(MetadataReference.CreateFromFile(path));
        }
    }

    private static void AddAllDlls(List<MetadataReference> refs, string dir)
    {
        if (!Directory.Exists(dir)) return;
        foreach (var dll in Directory.GetFiles(dir, "*.dll"))
        {
            try
            {
                // Native/host DLLs (hostpolicy.dll, msquic.dll, mscordbi.dll, etc.) live in the
                // same folders as managed assemblies but aren't valid PE metadata — adding them
                // as references doesn't fail here (CreateFromFile is lazy), it fails much later
                // during Emit() with a wall of "could not be opened" errors for every one of them.
                // GetAssemblyName throws for anything that isn't actually a managed assembly.
                System.Reflection.AssemblyName.GetAssemblyName(dll);
                refs.Add(MetadataReference.CreateFromFile(dll));
            }
            catch { /* native DLL or unreadable — skip */ }
        }
    }
}
