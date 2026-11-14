//! Chart analysis for NakuruTool: one native 7K chart at 1.0x, Prism at the 96.5%
//! base goal and the chart dan estimate. The calculation crates are used unchanged;
//! the only formula added here is the genre tendency (see `tendencies`).

use dan_calc::chart_pipeline::classifier::{analyze_chart, ClassifyInput};
use dan_calc::{estimate, Dan, Options, PlayedOd};
use dan_core::ManiaBeatmap;
use ln_skill::{analyze_ln_ssr, published_ln_rating, LnMap, LN_SKILL_VERSION};
use prism::params::{PRISM_SKILLSET_FLOOR, PRISM_SKILLSET_SHAPE};
use prism::{
    chart_notes, clear_prism_cache, prism_engine_version, prism_ln_lead_values, prism_models, prism_od, prism_rate,
    prism_strain_at_goal, PrismPrepared, PRISM_BASE_GOAL,
};
use serde_json::{json, Map, Value};

pub const UPSTREAM_COMMIT: &str = "1339369851234d6ed928aa1bd8149a4b558378e6";
/// Bump when this adapter changes the values it returns (cached results are keyed by it).
pub const ADAPTER_VERSION: u32 = 3;
/// Charts are analyzed at their own speed only.
pub const RATE: f64 = 1.0;
/// Prism's base goal: a chart's reference difficulty is the rating needed for 96.5%.
pub const CHART_GOAL: f64 = PRISM_BASE_GOAL;

/// Upstream `prism::SKILLSET_SHARE_CAP` (private): the section share over the
/// skillset reference is capped at 2 in `skillset_demand`.
const SKILLSET_SHARE_CAP: f64 = 2.0;

/// Upstream `prism::SKILLSET_SOURCES` (private), in the order of `PrismPrepared::skillsets`:
/// (Prism value name, index into `PRISM_SKILLSET_SHAPE` / the section pattern mix).
/// `Stamina` has no demand and is always 0. A test checks this order against Prism's values.
const SKILLSET_SOURCES: [(&str, Option<usize>); 8] = [
    ("Stream", Some(0)),
    ("JumpstreamOnly", Some(1)),
    ("Handstream", Some(2)),
    ("Stamina", None),
    ("JackSpeed", Some(3)),
    ("ChordjackOnly", Some(4)),
    ("Technical", Some(6)),
    ("Speed", Some(7)),
];

/// The five displayed genres: (Prism value name, Prism skillsets merged into it).
/// Prism merges Stream/JumpstreamOnly into Jumpstream and JackSpeed/ChordjackOnly into
/// Chordjack by taking the larger value; tendencies are merged the same way.
pub const GENRES: [(&str, &[&str]); 5] = [
    ("Jumpstream", &["Stream", "JumpstreamOnly"]),
    ("Handstream", &["Handstream"]),
    ("Speed", &["Speed"]),
    ("Technical", &["Technical"]),
    ("Chordjack", &["JackSpeed", "ChordjackOnly"]),
];

pub fn engine_version() -> String {
    format!(
        "{}:ln-{}:{}:analyze7k-{}",
        prism_engine_version(),
        LN_SKILL_VERSION,
        UPSTREAM_COMMIT,
        ADAPTER_VERSION
    )
}

/// Clears Prism's per-thread cache of prepared charts when one analysis ends (also on
/// errors and panics). Prism keeps up to 2000 prepared charts per thread; the cache only
/// helps within one chart (the dan estimate rates the same notes again), and over a long
/// run of different charts it grows until every analysis on that thread is several
/// times slower. Clearing it does not change any result.
struct PrismCacheScope;

impl Drop for PrismCacheScope {
    fn drop(&mut self) {
        clear_prism_cache();
    }
}

/// Request: `{"od": 8.0}` (the chart's OD).
pub fn analyze(osu: &str, request: &Value) -> Result<Value, String> {
    let _cache = PrismCacheScope;
    let od = request.get("od").and_then(Value::as_f64).ok_or("od is required")?;
    if !od.is_finite() || !(0.0..=10.0).contains(&od) {
        return Err("invalid OD".into());
    }
    let map = dan_core::parse_mania_beatmap(osu);
    if map.key_count != 7 {
        return Err("only 7K supported".into());
    }
    let notes = chart_notes(&map);
    let prism = prism_chart(&map, &notes, od)?;
    let dan = dan_verdict(&map, osu, od)?;
    Ok(json!({ "prism": prism, "dan": dan }))
}

/// Prism at 1.0x and the base goal: Overall, the five genre values (as Mania Tracker and
/// huuugeosu publish them, LN lead included), their tendencies and the LN rating.
fn prism_chart(map: &ManiaBeatmap, notes: &[dan_core::ManiaNote], od: f64) -> Result<Value, String> {
    let models = prism_models(map.key_count).ok_or("unsupported chart")?;
    // What `analyze_prism` does (`rate_with` -> `prepare_cached`), kept here so the
    // tendency reads the same prepared sections.
    let prepared = PrismPrepared::new(
        notes,
        map.key_count,
        prism_rate(Some(RATE)),
        prism_od(Some(od), map.od),
        &models.press,
        &models.scale,
        true,
        true,
    );
    let result = prepared.rate(Some(CHART_GOAL));
    let overall = result.overall();
    let tendency = tendencies(&prepared, models.press.goal_exponent)?;

    let mut values: Vec<(String, f64)> = result.values.iter().map(|(k, v)| (k.to_string(), *v)).collect();
    let ln_map = LnMap { notes, key_count: map.key_count, od: map.od };
    let mut ln = 0.0;
    if let Some(skill) = analyze_ln_ssr(&ln_map, RATE, Some(od), CHART_GOAL) {
        ln = published_ln_rating(&skill, Some(overall));
        prism_ln_lead_values(map.key_count, &mut values, ln, skill.hold_ratio);
    }
    let value_of = |name: &str| values.iter().find(|(k, _)| k == name).map(|(_, v)| *v);

    let mut genres = Map::new();
    let mut genre_tendency = Map::new();
    for (name, sources) in GENRES {
        let value = value_of(name).ok_or("invalid Prism output")?;
        let merged = sources
            .iter()
            .map(|s| tendency.iter().find(|(k, _)| k == s).map_or(0.0, |(_, c)| *c))
            .fold(0.0, f64::max);
        genres.insert(name.into(), finite(value)?);
        genre_tendency.insert(name.into(), finite(merged)?);
    }
    Ok(json!({
        "overall": finite(overall)?,
        "ln": finite(ln)?,
        "genres": genres,
        "tendency": genre_tendency,
    }))
}

fn finite(value: f64) -> Result<Value, String> {
    if value.is_finite() && value >= 0.0 {
        Ok(json!(value))
    } else {
        Err("non-finite rating".into())
    }
}

/// The largest skillset multiplier Prism can give a section, over all skillsets:
/// `FLOOR + (1 - FLOOR) * CAP^exponent` with the largest exponent (Speed, 1.2).
pub fn tendency_top() -> f64 {
    SKILLSET_SOURCES
        .iter()
        .filter_map(|&(_, source)| source)
        .map(|source| {
            PRISM_SKILLSET_FLOOR + (1.0 - PRISM_SKILLSET_FLOOR) * SKILLSET_SHARE_CAP.powf(PRISM_SKILLSET_SHAPE[source].exponent)
        })
        .fold(0.0, f64::max)
}

/// Genre tendency, 0-100, for each Prism skillset, on one scale shared by all genres.
/// NakuruTool shows each genre's share of the five tendencies as the genre concentration (sums to 100).
///
/// Prism rates a skillset by re-weighting every section's demand with
/// `FLOOR + (1 - FLOOR) * min(CAP, share / reference)^exponent`, where `share` is the
/// skillset's part of the section's work, and solving the same goal equation. That
/// solution scales linearly with the demand, so for each skillset
/// `r = strain(skillset demand) / strain(overall demand)` is at least `FLOOR` (the skillset
/// is in no section) and at most [`tendency_top`]. The tendency maps
/// `[FLOOR, tendency_top()]` to 0-100 with the same line for every skillset, so within a
/// chart the tendencies keep the order of Prism's skillset values (value = Overall * strain /
/// top strain; the LN lead scales all of them alike). 100 is a chart saturated with Speed in
/// every section; other skillsets top out lower because Prism's curve for them is flatter.
/// Both strains are solved without mash routes, as Prism solves skillsets.
pub fn tendencies(prepared: &PrismPrepared, goal_exponent: f64) -> Result<Vec<(&'static str, f64)>, String> {
    let skillsets = prepared.skillsets.as_ref().ok_or("Prism skillsets missing")?;
    let base = prism_strain_at_goal(&prepared.overall, &prepared.weight, CHART_GOAL, goal_exponent);
    let top = tendency_top();
    let mut out = Vec::with_capacity(SKILLSET_SOURCES.len());
    for (i, &(name, source)) in SKILLSET_SOURCES.iter().enumerate() {
        let (Some(_), Some(Some(demand))) = (source, skillsets.get(i)) else {
            out.push((name, 0.0));
            continue;
        };
        if !(base > 0.0) {
            out.push((name, 0.0));
            continue;
        }
        let strain = prism_strain_at_goal(demand, &prepared.weight, CHART_GOAL, goal_exponent);
        let r = strain / base;
        let value = ((r - PRISM_SKILLSET_FLOOR) / (top - PRISM_SKILLSET_FLOOR)).clamp(0.0, 1.0) * 100.0;
        out.push((name, if value.is_finite() { value } else { 0.0 }));
    }
    Ok(out)
}

fn dan_json(value: &Option<Dan>) -> Value {
    value.as_ref().map_or(Value::Null, |d| json!({ "raw_dan": d.raw_dan }))
}

/// The chart dan estimate at 1.0x and the chart's OD, plus the native-chart features the
/// dan genre tiles are read from (huuugeosu's `bucketFeatures`, version 1).
fn dan_verdict(map: &ManiaBeatmap, osu: &str, od: f64) -> Result<Value, String> {
    // `estimate` computes this same value (same parser, 1.0x) when it is not given one.
    let star = dan_core::star_rating::calculate_mania_star_rating(&map.notes, 7, RATE);
    let result = estimate(
        osu,
        &Options { rate: RATE, od: Some(PlayedOd::Value(od)), star_rating: Some(star), ..Default::default() },
    )
    .map_err(|e| e.to_string())?;
    if result.key_count != 7 || !result.supported {
        return Err("only 7K supported".into());
    }
    // One chart analysis without the played OD serves both the genre tiles (which read the
    // native chart) and the eligible / vibro flags: those come from the notes and the rate
    // only (`inspect_chart_dan_eligibility`, `detect_*_vibro`), never from `od_flag`.
    let input = ClassifyInput {
        rate: Some(RATE),
        od_flag: None,
        star_rating: Some(star),
        version: map.version.clone(),
        file_speed: Some(1.0),
        total_length: Some(map.total_length / 1000.0),
        ..Default::default()
    };
    let base = analyze_chart(map, osu, &input);
    let clusters: Vec<Value> = base
        .clusters
        .as_ref()
        .map(|report| {
            report
                .top_five_clusters()
                .iter()
                .map(|c| {
                    json!({
                        "label": c.format(1.0), "pattern": c.pattern.as_str(),
                        "importance": c.importance(), "mixed": c.mixed,
                    })
                })
                .collect()
        })
        .unwrap_or_default();
    let shape = dan_core::rice_shape::rice_shape(&map.notes, 7.0, map.timing_points.as_deref(), map.bpm);
    let patterns: Vec<Value> =
        base.patterns.patterns.iter().map(|p| json!({ "id": p.id.as_str(), "score": p.score })).collect();
    Ok(json!({
        "regular": dan_json(&result.rice),
        "ln": dan_json(&result.ln),
        "primary_family": result.primary.as_ref().map(|d| if matches!(d.family, dan_calc::Family::Ln) { "ln" } else { "regular" }),
        "eligible": base.dan_eligibility.eligible,
        "vibro": base.vibro || base.ln_vibro,
        "bucketFeatures": {
            "version": 1,
            "lnRatio": result.ln_ratio,
            "patterns": patterns,
            "clusters": clusters,
            "clusterCategory": base.clusters.as_ref().map(|r| &r.category),
            "riceShape": shape.map(|s| json!({
                "twoBack": s.two_back, "techSections": s.tech_sections, "motifRepeat": s.motif_repeat,
                "speedPeak": s.speed_peak, "rollSections": s.roll_sections,
            })),
        },
    }))
}

#[cfg(test)]
mod tests {
    use super::*;
    use prism::{analyze_prism, PrismOptions};

    /// A 7K chart of single notes rolling across the columns: no jacks, no chords.
    fn roll_chart() -> String {
        let mut text = String::from(
            "osu file format v14\n\n[General]\nMode: 3\n\n[Difficulty]\nCircleSize:7\nOverallDifficulty:8\n\n\
             [TimingPoints]\n0,250,4,2,0,100,1,0\n\n[HitObjects]\n",
        );
        let columns = [0, 1, 2, 3, 4, 5, 6, 5, 4, 3, 2, 1];
        for i in 0..2400 {
            let column = columns[i % columns.len()];
            let x = (column * 512 + 256) / 7;
            text.push_str(&format!("{x},192,{},1,0,0:0:0:0:\n", 1000 + i * 90));
        }
        text
    }

    #[test]
    fn prepared_path_matches_analyze_prism() {
        let text = roll_chart();
        let map = dan_core::parse_mania_beatmap(&text);
        let notes = chart_notes(&map);
        let models = prism_models(7).unwrap();
        let prepared =
            PrismPrepared::new(&notes, 7, prism_rate(Some(RATE)), prism_od(Some(8.0), map.od), &models.press, &models.scale, true, true);
        let ours = prepared.rate(Some(CHART_GOAL));
        let upstream = analyze_prism(
            &notes,
            7,
            map.od,
            &PrismOptions { rate: Some(RATE), goal: Some(CHART_GOAL), od: Some(8.0), ..Default::default() },
        )
        .unwrap();
        assert_eq!(ours.values, upstream.values);
    }

    #[test]
    fn skillset_order_matches_prism_values() {
        // genre value = Overall * strain / top strain, so the strain ratios must match.
        let text = roll_chart();
        let map = dan_core::parse_mania_beatmap(&text);
        let notes = chart_notes(&map);
        let models = prism_models(7).unwrap();
        let prepared =
            PrismPrepared::new(&notes, 7, 1.0, prism_od(Some(8.0), map.od), &models.press, &models.scale, true, true);
        let result = prepared.rate(Some(CHART_GOAL));
        let ge = models.press.goal_exponent;
        let skillsets = prepared.skillsets.as_ref().unwrap();
        let raw: Vec<f64> = skillsets
            .iter()
            .map(|d| d.as_ref().map_or(0.0, |d| prism_strain_at_goal(d, &prepared.weight, CHART_GOAL, ge)))
            .collect();
        let top = raw.iter().cloned().fold(0.0, f64::max);
        for (i, &(name, _)) in SKILLSET_SOURCES.iter().enumerate() {
            let expected = result.overall() * raw[i] / top;
            let actual = result.value(name).unwrap();
            assert!((expected - actual).abs() < 1e-9, "{name}: {expected} vs {actual}");
        }
    }

    #[test]
    fn tendency_top_is_speed_saturated() {
        // Speed has the steepest curve (exponent 1.2): 0.6 + 0.4 * 2^1.2
        let speed = PRISM_SKILLSET_SHAPE[7];
        assert_eq!(speed.exponent, 1.2);
        assert!((tendency_top() - (0.6 + 0.4 * 2f64.powf(1.2))).abs() < 1e-12);
    }

    #[test]
    fn tendencies_keep_prism_value_order() {
        let text = roll_chart();
        let map = dan_core::parse_mania_beatmap(&text);
        let notes = chart_notes(&map);
        let models = prism_models(7).unwrap();
        let prepared =
            PrismPrepared::new(&notes, 7, 1.0, prism_od(Some(8.0), map.od), &models.press, &models.scale, true, true);
        let result = prepared.rate(Some(CHART_GOAL));
        let tendency = tendencies(&prepared, models.press.goal_exponent).unwrap();
        let value = |name: &str| result.value(name).unwrap();
        let tend = |name: &str| tendency.iter().find(|(k, _)| *k == name).unwrap().1;
        let names: Vec<&str> = SKILLSET_SOURCES.iter().filter(|(_, s)| s.is_some()).map(|(n, _)| *n).collect();
        for a in &names {
            for b in &names {
                if value(a) > value(b) + 1e-9 {
                    assert!(tend(a) > tend(b), "{a} {} > {b} {} but tendency {} <= {}", value(a), value(b), tend(a), tend(b));
                }
            }
        }
    }

    #[test]
    fn absent_genres_read_zero() {
        let text = roll_chart();
        let value = analyze(&text, &json!({ "od": 8.0 }));
        let prism = match value {
            Ok(v) => v["prism"].clone(),
            // The dan estimate may reject a synthetic chart; check Prism on its own then.
            Err(_) => {
                let map = dan_core::parse_mania_beatmap(&text);
                prism_chart(&map, &chart_notes(&map), 8.0).unwrap()
            }
        };
        let jack = prism["tendency"]["Chordjack"].as_f64().unwrap();
        let bracket = prism["tendency"]["Handstream"].as_f64().unwrap();
        assert!(jack < 1e-6, "jack {jack}");
        assert!(bracket < 1e-6, "bracket {bracket}");
        for (name, _) in GENRES {
            let c = prism["tendency"][name].as_f64().unwrap();
            assert!((0.0..=100.0).contains(&c), "{name}: {c}");
        }
        assert!(prism["overall"].as_f64().unwrap() > 0.0);
    }

    #[test]
    fn rejects_other_key_counts() {
        let text = roll_chart().replace("CircleSize:7", "CircleSize:4");
        assert!(analyze(&text, &json!({ "od": 8.0 })).is_err());
        assert!(analyze(&roll_chart(), &json!({})).is_err());
    }
}
