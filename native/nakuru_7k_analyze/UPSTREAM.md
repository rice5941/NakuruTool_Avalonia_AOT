# nakuru_7k_analyze の出典

7K 譜面解析の計算本体は、すべて上流の Mania Tracker（mania-hub）を固定コミットで使う。
このクレートは C FFI のつなぎ（`src/lib.rs`、`src/analyze.rs`）と、ジャンル傾向の計算だけを持つ。
ビルド結果は `analyze_7k.dll`。

| 対象 | 出典 | ライセンス |
|---|---|---|
| prism / ln-skill / chart-parser / dan-core / jsmath / dan-calc / chart-pipeline / leoblack-* | https://github.com/aleju03/mania-hub @ `1339369851234d6ed928aa1bd8149a4b558378e6`（Cargo の git 依存） | MIT（Copyright (c) 2026 aleju03）`licenses/LICENSE-MANIA-TRACKER` |
| `vendor/leoblack-parser/` | 上記コミットの `algorithms/dan-calc/crates/leoblack-parser/` に、LeoBlack の時刻順修正（[5fac939](https://github.com/LeoBlackMT/osumania_map_analyser/commit/5fac939e28e85f414c63a1827eddca9f3997f40b)）を当てたもの | MIT（aleju03 / Leo_Black）`vendor/leoblack-parser/LICENSE*` |
| `vendor/leoblack-ett/` | このプロジェクトで作成した代替クレート（下記） | MIT |

C# 側（`Features/Analyze7k/`）の段位ジャンル判定（`DanBuckets`）と段位の表示名（`DanLabels`）は、
Mania Tracker の TypeScript を C# へ移植したもの。

## MinaCalc を外している理由（`vendor/leoblack-ett/`）

mania-hub の `leoblack-ett` は MinaCalc（Etterna）を WebAssembly として同梱し、wasmtime で実行する。
`chart-pipeline` の `compute_msd` が MinaCalc を呼ぶのは Prism の対象外のキー数（4K・5K・9K 以上）だけで、
7K は Prism の経路に入る。NakuruTool は 7K だけを解析するため、`[patch]` で `leoblack-ett` を
同じ API を持つ代替クレートに差し替え、MinaCalc の呼び出しは常に失敗させている。

- 段位推定・Prism・LN の値が変わらないことは、差し替え前の版で計算した等速の全譜面と比較して確認する（下記）。
- これにより wasmtime（Apache-2.0 WITH LLVM-exception）と MinaCalc の wasm（出典・ライセンス未確認）が配布物から外れる。
- DLL は約 6.0MB から約 2.4MB になり、ビルドに必要だった 32MiB スタックの設定も不要になった。

## 解析の範囲

- 等速・精度 96.5% だけを解析する（rate / goal の指定はない）。
- `PrismPrepared::new` を直接作って `analyze_prism` と同じ値を出し、同じ準備データからジャンル傾向を計算する。
- 出力は段位推定の数値（`raw_dan`）・主分類・対象判定・縦連打判定・段位ジャンルの判定材料（`bucketFeatures`）だけ。
  ラベル・信頼度・パターンの説明文などは出力しない。

## 速度のための工夫（値は変えない）

- **Prism のキャッシュを1譜面ごとに消す**（`analyze` の `PrismCacheScope`）。Prism はスレッドごとに準備済みの譜面を
  最大 2000 件キャッシュする（`PREPARED_KEPT`）。役に立つのは1譜面の中だけ（段位推定が同じノーツを Prism で計算し直す）で、
  同じスレッドで別の譜面を続けて解析するとキャッシュが膨らみ、1譜面あたりの時間が数倍に伸びる
  （43,673 譜面・16 スレッドで、最初の約 60ms から最後には約 500ms）。
- **譜面分析（`analyze_chart`）は OD 指定なしの1回だけ**。対象判定（`inspect_chart_dan_eligibility`）と縦連打判定
  （`detect_*_vibro`）はノーツとレートだけで決まり、`od_flag` を使わないため、段位ジャンルの判定材料と同じ分析から取る。
- **スター値を1回だけ計算して `estimate` に渡す**（渡さないと `estimate` が同じパーサー・同じレートで同じ値を計算する）。
- 3つの変更の前後で、等速の全譜面の出力が完全に一致することを確認した（`docs/ANALYZE_7K_SPEC.md` §9.3）。

## ジャンル傾向

Prism はジャンル（skillset）ごとに、区間の負荷へ `0.6 + 0.4 × min(2, 割合 ÷ 基準値)^指数` を掛けてから
同じ精度の式で強さを解く。その解は負荷に比例するため、`r = ジャンルの強さ ÷ Overall の強さ`（どちらも連打経路なし）は
`0.6`（どの区間にも含まれない）以上、`0.6 + 0.4 × 2^指数` 以下に収まる。
`0.6`〜全ジャンルで最大の上限（Speed の `0.6 + 0.4 × 2^1.2`）を、全ジャンル共通で 0〜100 に写したものをジャンル傾向とする
（出力キー `tendency`。1譜面の中での大小が Prism のジャンル値と一致する）。
画面のジャンル濃度は、C# 側でこの傾向を5ジャンルの合計で割った内訳（%）。上流の非公開定数（`SKILLSET_SHARE_CAP = 2`、`SKILLSET_SOURCES` の並び）を
写しているため、`src/analyze.rs` のテストで Prism の値と突き合わせている。

## 確認済みの一致

- 差し替え前（2026-11-13）: Windows で同じ固定コミットを使う CLI（`jit`）と DLL（`aot`）の出力を、
  ランダムな実譜面 60 件 × rate 2 種で比較し、120 件すべてで段位・Prism・LN の値が完全に一致した。
- 差し替え後: 「検証」の節（`docs/ANALYZE_7K_SPEC.md`）を参照。

## ビルド

- Rust 1.96.0（`rust-toolchain.toml` で固定）。
- C# 側は段位エンジンの再帰に余裕を持たせるため、16MiB のスタックのスレッドから呼ぶ。

上流を更新するときは、各 `rev` と `analyze::UPSTREAM_COMMIT` を揃えて上げ、比較スクリプトで回帰を確認する。
LeoBlack の修正が上流に取り込まれたら、回帰比較のうえで `[patch]` の leoblack-parser を外す。
