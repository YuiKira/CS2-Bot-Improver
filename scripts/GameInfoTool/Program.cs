using CS2BotTools;

if (args.Length != 2) throw new ArgumentException("Usage: GameInfoTool <official-gameinfo.gi> <stage>");
var original = File.ReadAllText(args[0]);
var online = GameInfoConfig.SetBotMode(original, false);
var bots = GameInfoConfig.SetBotMode(online, true);
if (GameInfoConfig.SetBotMode(bots, true) != bots)
    throw new Exception("Bot mode must be idempotent");
if (GameInfoConfig.SetBotMode(GameInfoConfig.SetBotMode(bots, false), true) != bots)
    throw new Exception("Mode round-trip failed");
GameInfoConfig.ValidateLayers(bots, args[1]);
if (online != original) throw new Exception("Official baseline must not already contain Bot paths");
var sample = "\"GameInfo\" { // SearchPaths { ignored comment\n\"FileSystem\" { \"SearchPaths\" { Game csgo Game core } } }";
var sampleBots = GameInfoConfig.SetBotMode(sample, true);
if (GameInfoConfig.SetBotMode(sampleBots, true) != sampleBots)
    throw new Exception("Quoted keys/comment parsing failed");
bool rejected = false;
try { GameInfoConfig.ValidateLayers("GameInfo { LayeredOnMod missing-test-layer }", args[1]); }
catch (InvalidDataException) { rejected = true; }
if (!rejected) throw new Exception("Missing layered-mod dependency must be rejected");
try
{
    GameInfoConfig.SetBotMode("GameInfo { FileSystem { } }", true);
    throw new Exception("Missing SearchPaths block must be rejected");
}
catch (InvalidDataException) { }
foreach (var file in new[] { "gameinfo.gi", "backup/WithBots/gameinfo.gi", "backup/Online/gameinfo.gi" })
{
    var destination = Path.Combine(args[1], file);
    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
    File.WriteAllText(destination, file.Contains("Online") ? online : bots);
}
Console.WriteLine("GameInfo mode generation and round-trip checks passed.");
