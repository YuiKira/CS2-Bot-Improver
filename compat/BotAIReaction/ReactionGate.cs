namespace BotAI;

// Pure timing logic; no engine/native calls, also used by the smoke checks.
internal sealed class ReactionGate
{
    private sealed class Encounter(double now)
    {
        public double FirstSeen = now;
        public double LastSeen = now;
        public bool Released;
    }

    internal readonly record struct Decision(bool Allowed, bool Started, bool Released, double WaitedSeconds);
    private readonly Dictionary<(nint Bot, nint Enemy), Encounter> _encounters = new();
    private readonly object _sync = new();
    private const double LostSightResetSeconds = 0.5;

    internal static int ExtraMilliseconds(float skill) => (skill > 1 ? skill / 100 : skill) >= 0.99f ? 100 : 40;

    internal Decision Evaluate(nint bot, nint enemy, double now, int delayMs)
    {
        if (delayMs <= 0) return new(true, false, false, 0);
        lock (_sync)
        {
            var key = (bot, enemy);
            bool started = !_encounters.TryGetValue(key, out var state)
                || now < state.LastSeen || now - state.LastSeen > LostSightResetSeconds;
            if (started) _encounters[key] = state = new Encounter(now);
            state!.LastSeen = now;
            double waited = now - state.FirstSeen;
            bool allowed = waited + 1e-9 >= delayMs / 1000.0;
            bool released = allowed && !state.Released;
            if (allowed) state.Released = true;
            return new(allowed, started, released, waited);
        }
    }

    internal void Clear()
    {
        lock (_sync) _encounters.Clear();
    }

    internal void KeepVisible(nint bot, nint enemy, double now)
    {
        lock (_sync)
            if (_encounters.TryGetValue((bot, enemy), out var state) && now >= state.LastSeen)
                state.LastSeen = now;
    }
}
