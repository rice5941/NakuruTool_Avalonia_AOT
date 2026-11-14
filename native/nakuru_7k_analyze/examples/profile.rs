//! Development profiler (not shipped): time each stage of `analyze` on one thread.
//!   profile <list.tsv> [limit]
//! `list.tsv` holds `md5<TAB>od<TAB>path` per line (same as `probe batch`).
use std::time::{Duration, Instant};

use analyze_7k::analyze::{analyze, tendencies, CHART_GOAL, RATE};
use dan_calc::chart_pipeline::classifier::{analyze_chart, ClassifyInput};
use dan_calc::{estimate, Options, PlayedOd};
use ln_skill::{analyze_ln_ssr, LnMap};
use prism::{chart_notes, prism_models, prism_od, prism_rate, PrismPrepared};
use serde_json::json;

#[derive(Default)]
struct Times {
    parse: Duration,
    prism: Duration,
    tendency: Duration,
    ln: Duration,
    estimate: Duration,
    star: Duration,
    chart_base: Duration,
    rice: Duration,
    full: Duration,
}

fn main() -> Result<(), Box<dyn std::error::Error>> {
    let args: Vec<String> = std::env::args().skip(1).collect();
    let limit: usize = args.get(1).map_or(Ok(usize::MAX), |s| s.parse())?;
    let mut t = Times::default();
    let mut n = 0usize;
    for line in std::fs::read_to_string(&args[0])?.lines().filter(|l| !l.trim().is_empty()).take(limit) {
        let mut parts = line.splitn(3, '\t');
        let _md5 = parts.next().unwrap();
        let od: f64 = parts.next().unwrap().parse()?;
        let path = parts.next().unwrap();
        let Ok(text) = std::fs::read_to_string(path) else { continue };
        let text = text.trim_start_matches('\u{feff}');

        let s = Instant::now();
        let map = dan_core::parse_mania_beatmap(text);
        t.parse += s.elapsed();
        if map.key_count != 7 {
            continue;
        }
        let notes = chart_notes(&map);
        let models = prism_models(7).unwrap();

        let s = Instant::now();
        let prepared =
            PrismPrepared::new(&notes, 7, prism_rate(Some(RATE)), prism_od(Some(od), map.od), &models.press, &models.scale, true, true);
        let _ = prepared.rate(Some(CHART_GOAL));
        t.prism += s.elapsed();

        let s = Instant::now();
        let _ = tendencies(&prepared, models.press.goal_exponent);
        t.tendency += s.elapsed();

        let s = Instant::now();
        let _ = analyze_ln_ssr(&LnMap { notes: &notes, key_count: 7, od: map.od }, RATE, Some(od), CHART_GOAL);
        t.ln += s.elapsed();

        let s = Instant::now();
        let star = dan_core::star_rating::calculate_mania_star_rating(&map.notes, 7, RATE);
        t.star += s.elapsed();

        let s = Instant::now();
        let _ = estimate(
            text,
            &Options { rate: RATE, od: Some(PlayedOd::Value(od)), star_rating: Some(star), ..Default::default() },
        );
        t.estimate += s.elapsed();

        let input = ClassifyInput {
            rate: Some(RATE),
            star_rating: Some(star),
            version: map.version.clone(),
            file_speed: Some(1.0),
            total_length: Some(map.total_length / 1000.0),
            ..Default::default()
        };
        let s = Instant::now();
        let _ = analyze_chart(&map, text, &input);
        t.chart_base += s.elapsed();
        prism::clear_prism_cache();

        let s = Instant::now();
        let _ = dan_core::rice_shape::rice_shape(&map.notes, 7.0, map.timing_points.as_deref(), map.bpm);
        t.rice += s.elapsed();

        let s = Instant::now();
        let _ = analyze(text, &json!({ "od": od }));
        t.full += s.elapsed();
        n += 1;
    }

    let ms = |d: Duration| d.as_secs_f64() * 1000.0 / n.max(1) as f64;
    let parts = [
        ("parse", t.parse),
        ("prism (prepare + rate)", t.prism),
        ("tendency", t.tendency),
        ("ln skill", t.ln),
        ("dan estimate", t.estimate),
        ("star rating", t.star),
        ("analyze_chart", t.chart_base),
        ("rice shape", t.rice),
    ];
    let sum: Duration = parts.iter().map(|(_, d)| *d).sum();
    println!("charts={n}");
    for (name, d) in parts {
        println!("{name:<24} {:>8.2} ms  {:>5.1}%", ms(d), d.as_secs_f64() / sum.as_secs_f64() * 100.0);
    }
    println!("{:<24} {:>8.2} ms", "sum of stages", ms(sum));
    println!("{:<24} {:>8.2} ms", "analyze() end to end", ms(t.full));
    Ok(())
}
