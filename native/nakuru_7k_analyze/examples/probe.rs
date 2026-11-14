//! Development probe (not shipped).
//!   probe dump <chart.osu> <od>                     -> analyze() JSON
//!   probe batch <list.tsv> <threads> <out.jsonl>    -> one JSON line per chart
//! `list.tsv` holds `md5<TAB>od<TAB>path` per line; output lines are
//! `{"md5": .., "result": .., "ms": ..}` or `{"md5": .., "error": .., "ms": ..}` (`ms`: time for that chart).
use std::io::Write;
use std::sync::atomic::{AtomicUsize, Ordering};
use std::sync::Mutex;
use std::time::Instant;

use analyze_7k::analyze::analyze;
use serde_json::json;

fn main() -> Result<(), Box<dyn std::error::Error>> {
    let args: Vec<String> = std::env::args().skip(1).collect();
    match args.first().map(String::as_str) {
        Some("dump") => {
            let text = std::fs::read_to_string(&args[1])?;
            let od: f64 = args[2].parse()?;
            println!("{}", analyze(&text, &json!({ "od": od }))?);
        }
        Some("batch") => {
            let threads: usize = args[2].parse()?;
            let items: Vec<(String, f64, String)> = std::fs::read_to_string(&args[1])?
                .lines()
                .filter(|l| !l.trim().is_empty())
                .map(|l| {
                    let mut parts = l.splitn(3, '\t');
                    let md5 = parts.next().unwrap().to_string();
                    let od = parts.next().unwrap().parse().unwrap();
                    (md5, od, parts.next().unwrap().to_string())
                })
                .collect();
            let out = Mutex::new(std::io::BufWriter::new(std::fs::File::create(&args[3])?));
            let next = AtomicUsize::new(0);
            let started = Instant::now();
            std::thread::scope(|s| {
                for _ in 0..threads {
                    std::thread::Builder::new()
                        .stack_size(16 << 20)
                        .spawn_scoped(s, || loop {
                            let i = next.fetch_add(1, Ordering::Relaxed);
                            let Some((md5, od, path)) = items.get(i) else { break };
                            let chart_started = Instant::now();
                            let line = match std::fs::read(path)
                                .map_err(|e| e.to_string())
                                .and_then(|b| String::from_utf8(b).map_err(|e| e.to_string()))
                            {
                                Ok(text) => {
                                    let text = text.trim_start_matches('\u{feff}');
                                    match analyze(text, &json!({ "od": od })) {
                                        Ok(v) => json!({ "md5": md5, "result": v }),
                                        Err(e) => json!({ "md5": md5, "error": e }),
                                    }
                                }
                                Err(e) => json!({ "md5": md5, "error": e }),
                            };
                            let mut line = line;
                            line["ms"] = json!(chart_started.elapsed().as_secs_f64() * 1000.0);
                            writeln!(out.lock().unwrap(), "{line}").unwrap();
                        })
                        .unwrap();
                }
            });
            eprintln!("charts={} seconds={:.1}", items.len(), started.elapsed().as_secs_f64());
        }
        _ => return Err("usage: probe dump|batch ...".into()),
    }
    Ok(())
}
