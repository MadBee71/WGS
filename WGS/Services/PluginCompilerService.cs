using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using WGS.Games;

namespace WGS.Services;

/// <summary>
/// Runtime compiler for single-file and folder-based C# game plugins.
///
/// Reference strategy:
/// 1. TRUSTED_PLATFORM_ASSEMBLIES from the running .NET host.
/// 2. Currently loaded non-dynamic assemblies, including WPF/WinForms/WGS.
/// 3. Published WGS plugin-contract metadata copy for single-file builds.
/// 4. SDK/reference packs only as a final development fallback.
///
/// This keeps plugin import usable on normal published WGS installations without
/// requiring users to install the full .NET SDK.
/// </summary>
public static class PluginCompilerService
{
    public static (IGamePlugin? plugin, string error) CompileAndLoad(string sourcePath)
        => CompileAndLoadFiles([sourcePath], Path.GetFileNameWithoutExtension(sourcePath));

    public static (IGamePlugin? plugin, string error) CompileAndLoadFiles(
        IEnumerable<string> sourcePaths,
        string? assemblyName = null)
    {
        var paths = sourcePaths
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (paths.Count == 0)
            return (null, "No C# plugin source files were found.");

        try
        {
            var parseOptions = CSharpParseOptions.Default
                .WithLanguageVersion(LanguageVersion.Preview);

            var syntaxTrees = new List<SyntaxTree>();
            foreach (var path in paths)
            {
                try
                {
                    var source = File.ReadAllText(path);
                    syntaxTrees.Add(CSharpSyntaxTree.ParseText(
                        source,
                        parseOptions,
                        path: path));
                }
                catch (Exception ex)
                {
                    return (null, $"Cannot read {Path.GetFileName(path)}: {ex.Message}");
                }
            }

            var references = BuildMetadataReferences();
            if (references.Count == 0)
                return (null,
                    "Could not resolve compiler references from the running WGS/.NET process.");

            var safeAssemblyName = string.IsNullOrWhiteSpace(assemblyName)
                ? $"WGS.RuntimePlugin.{Guid.NewGuid():N}"
                : new string(assemblyName
                    .Where(c => char.IsLetterOrDigit(c) || c is '_' or '.')
                    .ToArray());

            if (string.IsNullOrWhiteSpace(safeAssemblyName))
                safeAssemblyName = $"WGS.RuntimePlugin.{Guid.NewGuid():N}";

            var compilation = CSharpCompilation.Create(
                safeAssemblyName,
                syntaxTrees,
                references,
                new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Release,
                    nullableContextOptions: NullableContextOptions.Enable));

            using var pe = new MemoryStream();
            var emit = compilation.Emit(pe);

            if (!emit.Success)
            {
                var errors = emit.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(FormatDiagnostic)
                    .Distinct()
                    .Take(20)
                    .ToList();

                return (null,
                    errors.Count == 0
                        ? "Plugin compilation failed."
                        : "Compile errors:" + Environment.NewLine +
                          string.Join(Environment.NewLine, errors));
            }

            // Keep the proven named-DLL load behavior so security software and runtime
            // dependency resolution behave consistently with the existing host.
            var tempDll = Path.Combine(
                Path.GetTempPath(),
                $"wgs_plugin_{safeAssemblyName}_{Guid.NewGuid():N}.dll");

            try
            {
                File.WriteAllBytes(tempDll, pe.ToArray());
            }
            catch (Exception ex)
            {
                return (null, $"Cannot write temp plugin assembly: {ex.Message}");
            }

            Assembly assembly;
            try
            {
                assembly = Assembly.LoadFrom(tempDll);
            }
            catch (Exception ex)
            {
                return (null, $"Cannot load compiled plugin assembly: {ex.Message}");
            }

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                var loader = ex.LoaderExceptions
                    .Where(e => e != null)
                    .Select(e => e!.Message)
                    .Distinct();

                return (null,
                    "Plugin assembly loaded, but one or more types could not be resolved:" +
                    Environment.NewLine +
                    string.Join(Environment.NewLine, loader));
            }

            var pluginType = types.FirstOrDefault(t =>
                typeof(IGamePlugin).IsAssignableFrom(t) &&
                !t.IsAbstract &&
                !t.IsInterface &&
                t.GetConstructor(Type.EmptyTypes) != null);

            if (pluginType == null)
                return (null,
                    "Compiled successfully, but no concrete IGamePlugin implementation " +
                    "with a parameterless constructor was found.");

            try
            {
                var plugin = Activator.CreateInstance(pluginType) as IGamePlugin;
                return plugin == null
                    ? (null, $"Could not create plugin type '{pluginType.FullName}'.")
                    : (plugin, string.Empty);
            }
            catch (Exception ex)
            {
                return (null, $"Failed to create plugin instance: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            return (null, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static List<MetadataReference> BuildMetadataReferences()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Primary source: assemblies trusted by the currently running .NET host.
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string tpa &&
            !string.IsNullOrWhiteSpace(tpa))
        {
            foreach (var path in tpa.Split(
                         Path.PathSeparator,
                         StringSplitOptions.RemoveEmptyEntries))
            {
                AddIfManagedAssembly(paths, path);
            }
        }

        // Loaded WGS/runtime assemblies.
#pragma warning disable IL3000
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic) continue;

            try
            {
                if (!string.IsNullOrWhiteSpace(assembly.Location))
                    AddIfManagedAssembly(paths, assembly.Location);
            }
            catch
            {
                // Bundled/single-file assemblies may not expose Location.
            }
        }
#pragma warning restore IL3000

        // Published plugin-contract metadata copy and normal dev build assembly.
        AddIfManagedAssembly(
            paths,
            Path.Combine(AppContext.BaseDirectory, "WindowsGameServer.PluginContract.dll"));

        AddIfManagedAssembly(
            paths,
            Path.Combine(AppContext.BaseDirectory, "WindowsGameServer.dll"));

        // Other managed dependencies beside WGS (Toolkit/Roslyn/etc.).
        if (Directory.Exists(AppContext.BaseDirectory))
        {
            foreach (var dll in Directory.EnumerateFiles(
                         AppContext.BaseDirectory,
                         "*.dll",
                         SearchOption.TopDirectoryOnly))
            {
                AddIfManagedAssembly(paths, dll);
            }
        }

        // Development fallback only if runtime references were incomplete.
        if (!paths.Any(p =>
                string.Equals(
                    Path.GetFileName(p),
                    "System.Runtime.dll",
                    StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var path in FindReferencePackAssemblies())
                AddIfManagedAssembly(paths, path);
        }

        var references = new List<MetadataReference>();
        foreach (var path in paths)
        {
            try
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }
            catch
            {
                // Skip anything Roslyn cannot consume.
            }
        }

        return references;
    }

    private static void AddIfManagedAssembly(
        HashSet<string> paths,
        string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        var ext = Path.GetExtension(path);
        if (!ext.Equals(".dll", StringComparison.OrdinalIgnoreCase) &&
            !ext.Equals(".exe", StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            AssemblyName.GetAssemblyName(path);
            paths.Add(Path.GetFullPath(path));
        }
        catch
        {
            // Native or unreadable binary.
        }
    }

    private static IEnumerable<string> FindReferencePackAssemblies()
    {
        var roots = new List<string>();

        void AddRoot(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value) &&
                Directory.Exists(value) &&
                !roots.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                roots.Add(value);
            }
        }

        AddRoot(Environment.GetEnvironmentVariable("DOTNET_ROOT"));

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            AddRoot(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "dotnet"));

            AddRoot(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "dotnet"));
        }
        else
        {
            AddRoot("/usr/share/dotnet");
        }

        foreach (var root in roots)
        {
            foreach (var packName in new[]
            {
                "Microsoft.NETCore.App.Ref",
                "Microsoft.WindowsDesktop.App.Ref"
            })
            {
                var packRoot = Path.Combine(root, "packs", packName);
                if (!Directory.Exists(packRoot))
                    continue;

                var versionDir = Directory.GetDirectories(packRoot)
                    .OrderByDescending(d => ParseVersion(Path.GetFileName(d)))
                    .FirstOrDefault();

                if (versionDir == null)
                    continue;

                var refRoot = Path.Combine(versionDir, "ref");
                if (!Directory.Exists(refRoot))
                    continue;

                var tfmDir = Directory.GetDirectories(refRoot)
                    .OrderByDescending(
                        p => Path.GetFileName(p),
                        StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();

                if (tfmDir == null)
                    continue;

                foreach (var dll in Directory.EnumerateFiles(tfmDir, "*.dll"))
                    yield return dll;
            }
        }
    }

    private static Version ParseVersion(string value)
        => Version.TryParse(value.Split('-')[0], out var version)
            ? version
            : new Version(0, 0);

    private static string FormatDiagnostic(Diagnostic diagnostic)
    {
        var span = diagnostic.Location.GetLineSpan();
        if (span.IsValid)
        {
            var line = span.StartLinePosition.Line + 1;
            var column = span.StartLinePosition.Character + 1;
            return $"Line {line}, Col {column}: {diagnostic.GetMessage()}";
        }

        return diagnostic.GetMessage();
    }
}
