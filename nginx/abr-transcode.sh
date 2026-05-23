#!/bin/sh
# ─── StreamBG ABR Live Transcoding ───────────────────────────────────────────
# Called by nginx-rtmp exec directive on every new stream publish.
# Produces three HLS quality variants + a master playlist.
#
# nginx.conf usage:  exec /etc/nginx/abr-transcode.sh $name;
#
# Outputs (relative to /tmp/hls/<stream_key>/):
#   master.m3u8          ABR master playlist
#   720p/index.m3u8      2800 kbps, 1280×720
#   480p/index.m3u8      1400 kbps,  854×480
#   360p/index.m3u8       700 kbps,  640×360

STREAM_KEY="$1"
HLS_BASE="/tmp/hls/${STREAM_KEY}"

mkdir -p "${HLS_BASE}/720p" "${HLS_BASE}/480p" "${HLS_BASE}/360p"

# Write master playlist immediately so HLS players can start negotiating
# quality before the first segments arrive.
cat > "${HLS_BASE}/master.m3u8" <<'MASTER'
#EXTM3U
#EXT-X-VERSION:3
#EXT-X-STREAM-INF:BANDWIDTH=3000000,RESOLUTION=1280x720,CODECS="avc1.4d401f,mp4a.40.2",NAME="720p"
720p/index.m3u8
#EXT-X-STREAM-INF:BANDWIDTH=1500000,RESOLUTION=854x480,CODECS="avc1.42e01e,mp4a.40.2",NAME="480p"
480p/index.m3u8
#EXT-X-STREAM-INF:BANDWIDTH=800000,RESOLUTION=640x360,CODECS="avc1.42e01e,mp4a.40.2",NAME="360p"
360p/index.m3u8
MASTER

# Brief pause so the RTMP stream is fully established before FFmpeg subscribes.
sleep 1

# Transcode: split the video into three scaled tracks, encode each independently.
# keyint=48 → 2-second GOP at 24 fps — keeps segment boundaries aligned across
# all three qualities so the HLS player can switch seamlessly.
exec ffmpeg \
  -loglevel warning \
  -i "rtmp://127.0.0.1:1935/live/${STREAM_KEY}" \
  -filter_complex \
    "[0:v]split=3[v720][v480][v360];\
[v720]scale=w=1280:h=720:force_original_aspect_ratio=decrease,pad=1280:720:(ow-iw)/2:(oh-ih)/2:black[s720];\
[v480]scale=w=854:h=480:force_original_aspect_ratio=decrease,pad=854:480:(ow-iw)/2:(oh-ih)/2:black[s480];\
[v360]scale=w=640:h=360:force_original_aspect_ratio=decrease,pad=640:360:(ow-iw)/2:(oh-ih)/2:black[s360]" \
  \
  -map "[s720]" -map "0:a?" \
    -c:v libx264 -preset veryfast -tune zerolatency \
    -b:v 2800k -maxrate 3000k -bufsize 6000k \
    -g 48 -keyint_min 48 -sc_threshold 0 \
    -c:a aac -b:a 128k -ar 44100 -ac 2 \
    -f hls -hls_time 2 -hls_list_size 5 \
    -hls_flags delete_segments+independent_segments \
    -hls_segment_type mpegts \
    -hls_segment_filename "${HLS_BASE}/720p/%06d.ts" \
    "${HLS_BASE}/720p/index.m3u8" \
  \
  -map "[s480]" -map "0:a?" \
    -c:v libx264 -preset veryfast -tune zerolatency \
    -b:v 1400k -maxrate 1500k -bufsize 3000k \
    -g 48 -keyint_min 48 -sc_threshold 0 \
    -c:a aac -b:a 96k -ar 44100 -ac 2 \
    -f hls -hls_time 2 -hls_list_size 5 \
    -hls_flags delete_segments+independent_segments \
    -hls_segment_type mpegts \
    -hls_segment_filename "${HLS_BASE}/480p/%06d.ts" \
    "${HLS_BASE}/480p/index.m3u8" \
  \
  -map "[s360]" -map "0:a?" \
    -c:v libx264 -preset veryfast -tune zerolatency \
    -b:v 700k -maxrate 800k -bufsize 1600k \
    -g 48 -keyint_min 48 -sc_threshold 0 \
    -c:a aac -b:a 64k -ar 44100 -ac 2 \
    -f hls -hls_time 2 -hls_list_size 5 \
    -hls_flags delete_segments+independent_segments \
    -hls_segment_type mpegts \
    -hls_segment_filename "${HLS_BASE}/360p/%06d.ts" \
    "${HLS_BASE}/360p/index.m3u8"
