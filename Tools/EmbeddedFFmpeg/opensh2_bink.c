/* OpenSH2's small C ABI over dynamically linked FFmpeg. See README.md for dependency licensing. */
#include <stdint.h>
#include <stdlib.h>
#include <stdio.h>
#include <string.h>
#include <libavformat/avformat.h>
#include <libavcodec/avcodec.h>
#include <libswscale/swscale.h>

#define API __declspec(dllexport)
typedef struct {
    AVFormatContext *format;
    AVCodecContext *codec;
    AVPacket *packet;
    AVFrame *frame, *rgba;
    struct SwsContext *scale;
    int stream, draining;
    int64_t frame_index;
    double frame_seconds;
} Bink;

static int error_text(int code, char *error, int capacity) {
    if (error && capacity > 0) av_strerror(code, error, capacity);
    return code;
}

API void osh2_bink_close(Bink *b) {
    if (!b) return;
    sws_freeContext(b->scale);
    av_frame_free(&b->frame);
    av_frame_free(&b->rgba);
    av_packet_free(&b->packet);
    avcodec_free_context(&b->codec);
    avformat_close_input(&b->format);
    free(b);
}

/* All functions on a handle must run on the same decoding thread. UTF-8 paths. */
API Bink *osh2_bink_open(const char *path, int *width, int *height, double *frame_seconds,
                        char *error, int capacity) {
    Bink *b = calloc(1, sizeof(Bink));
    if (!b) { error_text(AVERROR(ENOMEM), error, capacity); return NULL; }
    int result = avformat_open_input(&b->format, path, NULL, NULL);
    if (result < 0) goto fail;
    result = avformat_find_stream_info(b->format, NULL);
    if (result < 0) goto fail;
    const AVCodec *decoder = NULL;
    result = av_find_best_stream(b->format, AVMEDIA_TYPE_VIDEO, -1, -1, &decoder, 0);
    if (result < 0) goto fail;
    b->stream = result;
    AVStream *stream = b->format->streams[b->stream];
    /* This prototype intentionally supports silent Bink 1 assets only. */
    if (stream->codecpar->codec_id != AV_CODEC_ID_BINKVIDEO) { result = AVERROR(ENOSYS); goto fail; }
    for (unsigned i = 0; i < b->format->nb_streams; ++i)
        if (b->format->streams[i]->codecpar->codec_type == AVMEDIA_TYPE_AUDIO) {
            if (error && capacity > 0) snprintf(error, capacity, "Audio is outside the silent-menu Bink prototype.");
            osh2_bink_close(b); return NULL;
        }
    b->codec = avcodec_alloc_context3(decoder);
    if (!b->codec) { result = AVERROR(ENOMEM); goto fail; }
    result = avcodec_parameters_to_context(b->codec, stream->codecpar);
    if (result < 0) goto fail;
    b->codec->thread_count = 1;
    result = avcodec_open2(b->codec, decoder, NULL);
    if (result < 0) goto fail;
    b->packet = av_packet_alloc();
    b->frame = av_frame_alloc();
    b->rgba = av_frame_alloc();
    if (!b->packet || !b->frame || !b->rgba) { result = AVERROR(ENOMEM); goto fail; }
    AVRational rate = av_guess_frame_rate(b->format, stream, NULL);
    if (rate.num <= 0 || rate.den <= 0) { result = AVERROR_INVALIDDATA; goto fail; }
    b->frame_seconds = av_q2d(av_inv_q(rate));
    *width = b->codec->width; *height = b->codec->height; *frame_seconds = b->frame_seconds;
    if (*width <= 0 || *height <= 0 || *width > 4096 || *height > 4096) { result = AVERROR_INVALIDDATA; goto fail; }
    b->rgba->format = AV_PIX_FMT_RGBA;
    b->rgba->width = *width; b->rgba->height = *height;
    result = av_frame_get_buffer(b->rgba, 32);
    if (result < 0) goto fail;
    return b;
fail:
    error_text(result, error, capacity);
    osh2_bink_close(b);
    return NULL;
}

/* Returns 1 for a frame, 0 for EOF, negative on failure. RGBA is bottom-up for Unity textures.
   Bink has a constant frame rate; presentation times are derived from its declared frame rate. */
API int osh2_bink_read(Bink *b, uint8_t *rgba, int capacity, double *seconds, char *error, int error_capacity) {
    if (!b || !rgba || capacity < (int64_t)b->codec->width * b->codec->height * 4)
        return error_text(AVERROR(EINVAL), error, error_capacity);
    int result;
    for (;;) {
        result = avcodec_receive_frame(b->codec, b->frame);
        if (result == 0) break;
        if (result == AVERROR_EOF) return 0;
        if (result != AVERROR(EAGAIN)) return error_text(result, error, error_capacity);
        if (b->draining) return error_text(AVERROR_INVALIDDATA, error, error_capacity);
        do {
            av_packet_unref(b->packet);
            result = av_read_frame(b->format, b->packet);
        } while (result >= 0 && b->packet->stream_index != b->stream);
        if (result < 0 && result != AVERROR_EOF) return error_text(result, error, error_capacity);
        b->draining = result == AVERROR_EOF;
        result = avcodec_send_packet(b->codec, b->draining ? NULL : b->packet);
        av_packet_unref(b->packet);
        if (result < 0) return error_text(result, error, error_capacity);
    }
    AVFrame *f = b->frame;
    b->scale = sws_getCachedContext(b->scale, f->width, f->height, (enum AVPixelFormat)f->format,
        f->width, f->height, AV_PIX_FMT_RGBA, SWS_BILINEAR, NULL, NULL, NULL);
    if (!b->scale) return error_text(AVERROR(ENOMEM), error, error_capacity);
    int color_space = f->colorspace == AVCOL_SPC_BT709 ? SWS_CS_ITU709 : SWS_CS_ITU601;
    const int *coefficients = sws_getCoefficients(color_space);
    sws_setColorspaceDetails(b->scale, coefficients, f->color_range == AVCOL_RANGE_JPEG,
        coefficients, 1, 0, 1 << 16, 1 << 16);
    result = sws_scale(b->scale, (const uint8_t *const *)f->data, f->linesize, 0, f->height, b->rgba->data, b->rgba->linesize);
    if (result != f->height) return error_text(AVERROR_INVALIDDATA, error, error_capacity);
    int stride = f->width * 4;
    for (int row = 0; row < f->height; ++row)
        memcpy(rgba + (size_t)(f->height - row - 1) * stride,
               b->rgba->data[0] + (size_t)row * b->rgba->linesize[0], stride);
    /* swscale preserves source alpha; opaque sources receive alpha 255. */
    *seconds = b->frame_index++ * b->frame_seconds;
    av_frame_unref(f);
    return 1;
}

API int osh2_bink_rewind(Bink *b, char *error, int capacity) {
    int result = av_seek_frame(b->format, b->stream, 0, AVSEEK_FLAG_BACKWARD);
    if (result < 0) return error_text(result, error, capacity);
    avcodec_flush_buffers(b->codec);
    av_packet_unref(b->packet);
    av_frame_unref(b->frame);
    b->draining = 0;
    b->frame_index = 0;
    return 0;
}
