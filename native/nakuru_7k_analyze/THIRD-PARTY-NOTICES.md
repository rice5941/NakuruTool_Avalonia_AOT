# Third-Party Notices (nakuru_7k_analyze / analyze_7k.dll)

analyze_7k.dll links the following crates (normal dependencies; build-only crates and
proc-macros are not included in the binary). Generated with `cargo tree -e normal` on 2026-11-14.

## Calculation (Mania Tracker / mania-hub @ 1339369851234d6ed928aa1bd8149a4b558378e6)

- **License**: MIT — Copyright (c) 2026 aleju03 (`licenses/LICENSE-MANIA-TRACKER`)
- **Repository**: https://github.com/aleju03/mania-hub
- Crates: prism 0.26.0, ln-skill, chart-parser, dan-core, jsmath, dan-calc, chart-pipeline,
  leoblack-estimators, leoblack-interlude, leoblack-patterns, leoblack-rework

## leoblack-parser (local patch)

- **License**: MIT — Copyright (c) 2026 aleju03 / Copyright (c) 2026 Leo_Black
  (`vendor/leoblack-parser/LICENSE`, `vendor/leoblack-parser/LICENSE-LEOBLACK`)
- **Source**: mania-hub `algorithms/dan-calc/crates/leoblack-parser`, with LeoBlack's stable time sort fix
  (https://github.com/LeoBlackMT/osumania_map_analyser/commit/5fac939e28e85f414c63a1827eddca9f3997f40b)

## leoblack-ett (local stand-in)

- `vendor/leoblack-ett/` is written for this project (MIT, same as this crate). It replaces mania-hub's
  `leoblack-ett` so that no MinaCalc build (WebAssembly) and no WebAssembly runtime is linked.
  See UPSTREAM.md.

## Adapter structure (huuugeosu)

- **License**: MIT — Copyright (c) 2026 YGetArtstr / Copyright (c) 2021 cmyui (`licenses/LICENSE-HUUUGEOSU`)

## Other crates

| Crate | Version | License |
|---|---|---|
| serde_json | 1.0.154 | MIT OR Apache-2.0 |
| serde_core | 1.0.229 | MIT OR Apache-2.0 |
| itoa | 1.0.19 | MIT OR Apache-2.0 |
| memchr | 2.8.3 | Unlicense OR MIT |
| zmij | 1.0.23 | MIT |

Build-time only (not shipped): csbindgen and proc-macro crates (serde_derive, syn, quote, proc-macro2, …).
