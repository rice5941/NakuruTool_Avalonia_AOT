#ifndef NAKURU_BUNGEE_BRIDGE_H
#define NAKURU_BUNGEE_BRIDGE_H

#ifdef __cplusplus
extern "C" {
#endif

typedef struct nakuru_bungee_stream_handle nakuru_bungee_stream_handle;

/// ストリーム作成。失敗時 NULL。
nakuru_bungee_stream_handle* nakuru_bungee_stream_create(
    int input_sample_rate,
    int output_sample_rate,
    int channels,
    int max_input_frames);

/// ストリーム破棄。handle が NULL なら何もしない。
void nakuru_bungee_stream_destroy(
    nakuru_bungee_stream_handle* handle);

/// 入力チャンクを処理し、実際の出力フレーム数を返す。
/// 負数はエラー。
/// input_channels: チャンネルごとのポインタ配列 (planar)
/// output_channels: チャンネルごとのポインタ配列 (planar, 書き込み先)
int nakuru_bungee_stream_process(
    nakuru_bungee_stream_handle* handle,
    const float* const* input_channels,
    int input_frames,
    float* const* output_channels,
    int output_capacity_frames,
    double speed,
    double pitch);

/// 末尾ドレイン。出力フレーム数を返す。0=完了。負数はエラー。
/// input に NULL を渡してドレインする。
int nakuru_bungee_stream_finish(
    nakuru_bungee_stream_handle* handle,
    float* const* output_channels,
    int output_capacity_frames,
    double speed,
    double pitch);

#ifdef __cplusplus
}
#endif

#endif
