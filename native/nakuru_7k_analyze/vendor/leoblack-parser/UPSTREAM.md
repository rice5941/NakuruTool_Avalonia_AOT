# Local parser patch

Source: mania-hub@1339369851234d6ed928aa1bd8149a4b558378e6,
`algorithms/dan-calc/crates/leoblack-parser/src/`.
Both upstream and LeoBlack MIT licenses are retained.

Only numerical-path change: `pattern_parser.rs::parse_hit_objects()` stably
sorts objects by start time, matching LeoBlack's fix:
https://github.com/LeoBlackMT/osumania_map_analyser/commit/5fac939e28e85f414c63a1827eddca9f3997f40b

The existing jsmath::js_sort_by preserves JavaScript sort semantics, including
the relative order of equal timestamps. Source .osu files are never rewritten.
Cargo.toml replaces workspace metadata and pins jsmath to the same git revision.
The engine uses this crate through Cargo's local patch; shared Cargo checkout
files remain unchanged. No other engine, Sunny coefficients or pp formula changes.

Verification: `cargo test --offline --manifest-path tools/mania-engine/Cargo.toml -p leoblack-parser`.
When upstream includes the same fix, compare regression results before removing
this patch and updating the pinned revision.
