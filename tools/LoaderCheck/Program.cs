using Mono.Cecil;
// Loads a DLL exactly as ScriptEngine.LoadDLL does: ReadAssembly with ReadSymbols = true.
foreach (var path in args)
{
    try
    {
        var r = new DefaultAssemblyResolver();
        var a = AssemblyDefinition.ReadAssembly(path, new ReaderParameters { AssemblyResolver = r, ReadSymbols = true });
        Console.WriteLine($"OK   {Path.GetFileName(Path.GetDirectoryName(path))}/{Path.GetFileName(path)}: {a.Name.Name}, symbols {a.MainModule.HasSymbols}");
    }
    catch (Exception e) { Console.WriteLine($"FAIL {path}: {e.GetType().Name}: {e.Message}"); }
}
