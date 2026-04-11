#include "bungee_bridge.h"

#include <algorithm>
#include <bungee/Bungee.h>
#include <bungee/Stream.h>
#include <cmath>
#include <new>

namespace {

bool is_valid_ratio(double value)
{
	return std::isfinite(value) && value > 0.0;
}

} // namespace

struct nakuru_bungee_stream_handle
{
	Bungee::SampleRates sampleRates;
	Bungee::Stretcher<Bungee::Basic> stretcher;
	Bungee::Stream<Bungee::Basic> stream;
	int channels;

	nakuru_bungee_stream_handle(
		int inputSampleRate,
		int outputSampleRate,
		int ch,
		int maxInputFrames)
		: sampleRates{inputSampleRate, outputSampleRate}
		, stretcher(sampleRates, ch)
		, stream(stretcher, maxInputFrames, ch)
		, channels(ch)
	{
	}
};

extern "C" {

nakuru_bungee_stream_handle* nakuru_bungee_stream_create(
	int input_sample_rate,
	int output_sample_rate,
	int channels,
	int max_input_frames)
{
	if (input_sample_rate <= 0 || output_sample_rate <= 0 || channels <= 0 || max_input_frames <= 0)
	{
		return nullptr;
	}

	try
	{
		return new nakuru_bungee_stream_handle(
			input_sample_rate,
			output_sample_rate,
			channels,
			max_input_frames);
	}
	catch (...)
	{
		return nullptr;
	}
}

void nakuru_bungee_stream_destroy(
	nakuru_bungee_stream_handle* handle)
{
	delete handle;
}

int nakuru_bungee_stream_process(
	nakuru_bungee_stream_handle* handle,
	const float* const* input_channels,
	int input_frames,
	float* const* output_channels,
	int output_capacity_frames,
	double speed,
	double pitch)
{
	if (!handle || !input_channels || !output_channels)
	{
		return -1;
	}
	if (input_frames < 0 || output_capacity_frames <= 0 || !is_valid_ratio(speed) || !is_valid_ratio(pitch))
	{
		return -1;
	}

	try
	{
		double outputFrameCountIdeal =
			(static_cast<double>(input_frames) * handle->sampleRates.output) /
			(speed * handle->sampleRates.input);
		// NOTE: Do NOT add extra frames here. Bungee's Stream::process() has an internal
		// 'framesNeeded' float accumulator that retains the leftover (outputFrameCount - actual).
		// Adding constant overshoot per call causes framesNeeded to grow linearly with chunk count,
		// causing an infinite loop on long files (e.g. a 3-minute song at 1152 frames/chunk
		// accumulates ~8000 * overshoot frames by the last chunk).
		outputFrameCountIdeal = std::clamp(
			std::ceil(outputFrameCountIdeal),
			1.0,
			static_cast<double>(output_capacity_frames));

		int actual = handle->stream.process(
			input_channels,
			output_channels,
			input_frames,
			outputFrameCountIdeal,
			pitch);
		if (actual > output_capacity_frames)
		{
			return -3;
		}

		return actual;
	}
	catch (...)
	{
		return -2;
	}
}

int nakuru_bungee_stream_finish(
	nakuru_bungee_stream_handle* handle,
	float* const* output_channels,
	int output_capacity_frames,
	double speed,
	double pitch)
{
	if (!handle || !output_channels)
	{
		return -1;
	}
	if (output_capacity_frames <= 0 || !is_valid_ratio(speed) || !is_valid_ratio(pitch))
	{
		return -1;
	}

	try
	{
		// Feed silence to flush the internal stretcher buffer.
		// Using 0 input frames with large output causes speed=0 and infinite loop.
		// Instead, feed a small amount of silence and request proportional output.
		const int silence_frames = 4096;
		double outputFrameCountIdeal =
			(static_cast<double>(silence_frames) * handle->sampleRates.output) /
			(speed * handle->sampleRates.input);
		outputFrameCountIdeal = std::clamp(
			std::ceil(outputFrameCountIdeal),
			1.0,
			static_cast<double>(output_capacity_frames));

		int actual = handle->stream.process(
			nullptr,
			output_channels,
			silence_frames,
			outputFrameCountIdeal,
			pitch);
		if (actual > output_capacity_frames)
		{
			return -3;
		}

		return actual;
	}
	catch (...)
	{
		return -2;
	}
}

} // extern "C"
