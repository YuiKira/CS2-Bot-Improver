# Selective HE disable

Source: ed0ard/CS2-Bot-NadeSystem, tag v1.2.3 (archive prefix 7605f27).
CounterStrikeSharp.API: 1.0.378, matching the shipped runtime.

Changes from that tag:
- Set the module version to 1.2.3-nohe.1.
- Exclude HE entries after database type normalization.
- Reject HE before replay, at the central projectile spawn entry, and at the instant grenade entry.
- Enforce the upstream package's native grenade disable switch so native AI cannot bypass the filter. The original plugin still generates flash, smoke, molotov/incendiary and decoy grenades normally.
- All original non-HE strategies, limits, fees, timings, events and commands remain in place. The default plugin mode is normal.
