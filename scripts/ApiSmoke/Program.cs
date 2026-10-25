using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;

var stage = args[0];
var css = Path.Combine(stage, "addons/counterstrikesharp");
AssemblyLoadContext.Default.Resolving += (_, name) => {
    var files = Directory.GetFiles(css, name.Name + ".dll", SearchOption.AllDirectories);
    var file = files.OrderBy(f => f.Contains("/api/") ? 0 : f.Contains("/shared/") ? 1 : 2).FirstOrDefault();
    return file == null ? null : AssemblyLoadContext.Default.LoadFromAssemblyPath(file);
};
var count = 0;
foreach (var file in Directory.GetFiles(Path.Combine(css, "plugins"), "*.dll", SearchOption.AllDirectories)) {
    using var stream = File.OpenRead(file);
    using var pe = new PEReader(stream);
    if (!pe.HasMetadata) continue;
    var reader = pe.GetMetadataReader();
    Assembly assembly = null;
    foreach (var handle in reader.MemberReferences) {
        var member = reader.GetMemberReference(handle);
        if (member.Parent.Kind != HandleKind.TypeReference) continue;
        var type = reader.GetTypeReference((TypeReferenceHandle)member.Parent);
        if (type.ResolutionScope.Kind != HandleKind.AssemblyReference) continue;
        var reference = reader.GetAssemblyReference((AssemblyReferenceHandle)type.ResolutionScope);
        var name = reader.GetString(reference.Name);
        if (name != "BotControllerApi" && name != "BotHiderApi" && name != "CounterStrikeSharp.API" && name != "MapChooserAPI") continue;
        assembly ??= AssemblyLoadContext.Default.Assemblies.FirstOrDefault(a => a.GetName().Name == Path.GetFileNameWithoutExtension(file)) ?? AssemblyLoadContext.Default.LoadFromAssemblyPath(file);
        var resolved = assembly.ManifestModule.ResolveMember(System.Reflection.Metadata.Ecma335.MetadataTokens.GetToken(handle));
        if (resolved == null) throw new Exception("Unresolved API member");
        count++;
    }
}
if (count == 0) throw new Exception("No API references tested");
Console.WriteLine($"PASS: {count} direct managed CSS/Bot/map API member references resolve against packaged APIs. Native calls were not executed.");
var policy = AssemblyLoadContext.Default.Assemblies
    .FirstOrDefault(a => a.GetName().Name == "NadeSystem")
    ?.GetType("NadeSystem.GrenadePolicy");
if (policy != null) {
    var blocks = policy.GetMethod("Blocks", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new Exception("Missing grenade policy method");
    var cases = new (string Type, bool Blocked)[] {
        ("he", true), ("HE", true), ("hegrenade", true), ("weapon_hegrenade", true),
        ("flash", false), ("smoke", false), ("molotov", false),
        ("incendiary", false), ("decoy", false), (null, false)
    };
    foreach (var test in cases)
        if ((bool)blocks.Invoke(null, new object[] {test.Type}) != test.Blocked)
            throw new Exception($"Incorrect grenade policy for {test.Type}");
    Console.WriteLine($"PASS: {cases.Length} compiled HE/non-HE grenade policy cases.");
}
