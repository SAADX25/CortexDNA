# Read-only health policy

These are transparent observation thresholds, not universal medical or device diagnoses. Individual hardware limits vary. No rule triggers an action.

| Area | Budget | Rule |
| --- | ---: | --- |
| Security | 25 | Only normal active Defender mode is assessed. Explicit disabled antivirus or real-time protection: −15, Error. Otherwise signatures older than 7 days: −5, Attention. Passive/third-party/missing/denied state: unavailable. Firewall and third-party products are outside this initial read-only check. |
| Storage | 20 | System-drive free space below 15%: −2; below 5%: −8 instead. Attention, not Error. Other drives do not change this rule. |
| Startup | 20 | Existing enabled, measured high-impact entries: −4 each, capped at 20. OptimizationAvailable. Incomplete impact coverage or an unloaded/empty catalog: unavailable, no invented penalty. |
| Updates | 15 | Local Windows Update pending-restart marker: −2, Attention. Cached successful installation time is informational; without a restart marker, update completeness remains unavailable. No online search, COM update session, service or installation changes. |
| Maintenance | 10 | Existing cleanup preview is available for user review. Opportunity size is unmeasured in this phase and remains unavailable; no file enumeration, cleanup or fabricated byte estimate. |
| Hardware | 10 | CPU temperature budget 4; GPU temperature budget 3; RAM pressure budget 3. Reported CPU/GPU temperature ≥90 °C: −2 per node, Attention. RAM usage ≥90%: −2, Attention. Missing/non-finite temperatures are unavailable. Partial devices without an observed high temperature remain unavailable rather than all being declared healthy. All GPUs are considered. |
| Network | 0 | Existing download/upload rates are informational. Zero traffic is normal. No internet reachability or connectivity diagnosis is inferred. |

Hardware capacity, memory and network readings older than 30 seconds are unavailable. A scan captures one shared Phase 3 snapshot and never requests fresh readings itself.

Assessed points = sum of weights for non-unavailable observations. Earned points = assessed points − sum of disclosed deductions. Score is earned/assessed, with coverage displayed separately. A /100 score is published only with 100 assessed points. No denominator normalization hides missing information.

Each deduction records its reason, severity, affected area and points. Provider exceptions and invalid provider results become unavailable evidence, never a confirmed problem. Unknown areas have zero deductions and do not enter the denominator.
