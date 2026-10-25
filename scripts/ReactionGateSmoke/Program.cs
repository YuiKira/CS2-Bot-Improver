using BotAI;

int checks = 0;
void Check(bool condition, string description)
{
    checks++;
    if (!condition) throw new Exception(description);
}
var gate = new ReactionGate();
Check(ReactionGate.ExtraMilliseconds(1) == 100, "Pro normalized skill");
Check(ReactionGate.ExtraMilliseconds(100) == 100, "Pro percent skill");
Check(ReactionGate.ExtraMilliseconds(.49f) == 40, "Rank normalized skill");
Check(ReactionGate.ExtraMilliseconds(49) == 40, "Rank percent skill");
Check(!gate.Evaluate(1, 10, 0, 100).Allowed, "Pro first observation blocked");
Check(!gate.Evaluate(1, 10, .099, 100).Allowed, "Pro blocked before 100ms");
var pro = gate.Evaluate(1, 10, .1, 100);
Check(pro.Allowed && pro.Released && Math.Abs(pro.WaitedSeconds - .1) < 1e-9, "Pro released at 100ms");
var next = gate.Evaluate(1, 10, .11, 100);
Check(next.Allowed && !next.Released, "No extra delay between subsequent shots");
Check(!gate.Evaluate(2, 10, 0, 40).Allowed, "Rank independent first observation");
Check(!gate.Evaluate(2, 10, .039, 40).Allowed, "Rank blocked before 40ms");
Check(gate.Evaluate(2, 10, .04, 40).Allowed, "Rank released at 40ms");
Check(!gate.Evaluate(1, 11, .12, 100).Allowed, "New enemy gets own encounter timer");
Check(gate.Evaluate(1, 10, .2, 100).Allowed, "Existing enemy remains released");
Check(!gate.Evaluate(1, 10, .8, 100).Allowed, "Long sight gap resets encounter");
Check(gate.Evaluate(1, 10, .9, 100).Allowed, "Reacquired enemy waits full delay");
Check(!gate.Evaluate(1, 10, .5, 100).Allowed, "Clock reset starts a new encounter");
gate.Clear();
Check(!gate.Evaluate(2, 10, 2, 40).Allowed, "Round reset clears released encounters");
Check(gate.Evaluate(3, 10, 0, 0).Allowed, "Zero delay leaves original behavior unchanged");
gate.Evaluate(4, 10, 0, 100);
gate.Evaluate(4, 10, .1, 100);
gate.KeepVisible(4, 10, .4);
gate.KeepVisible(4, 10, .8);
Check(gate.Evaluate(4, 10, 1, 100).Allowed, "Continuously visible enemy is not delayed a second time");
Console.WriteLine($"PASS: {checks} runtime reaction gate checks, including 100ms/40ms boundaries and independent encounters.");
