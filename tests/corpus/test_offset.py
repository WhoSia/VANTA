"""Offset fixture regression; synthetic, not evidence of BBB or native support."""
import pathlib
import tempfile
from prepare import normalize, probe, run

with tempfile.TemporaryDirectory(prefix="vanta-offset-test-") as d:
    d = pathlib.Path(d)
    source, dest = d / "offset.mp4", d / "normalized.mp4"
    run("ffmpeg", "-v", "error", "-xerror", "-nostdin", "-n", "-f", "lavfi", "-i",
        "sine=frequency=440:sample_rate=48000:duration=7", "-itsoffset", "15.75",
        "-f", "lavfi", "-i", "testsrc2=s=320x180:r=24:d=7.375", "-map", "1:v", "-map", "0:a",
        "-c:v", "libx264", "-preset", "ultrafast", "-threads", "2", "-c:a", "aac", str(source))
    before = source.read_bytes()
    raw, fixed = normalize(dict(width=320, height=180, fps=24, seconds=7), source, dest)
    video = next(s for s in raw["streams"] if s["codec_type"] == "video")
    assert abs(float(video["start_time"]) - 15.75) < .002
    v = next(s for s in fixed["streams"] if s["codec_type"] == "video")
    a = next(s for s in fixed["streams"] if s["codec_type"] == "audio")
    assert int(v["nb_frames"]) == 168 and v["avg_frame_rate"] == "24/1"
    assert abs(float(v["start_time"])) < .002 and abs(float(a["start_time"])) < .002
    assert abs(float(v["duration"]) - 7) < .002 and abs(float(a["duration"]) - 7) < .025
    run("ffmpeg", "-v", "error", "-xerror", "-nostdin", "-i", str(dest), "-f", "null", "-")
    assert source.read_bytes() == before
    print("Offset normalization: 168 frames, seven seconds, audio and strict decode passed.")

# Deliberately inject a backwards timestamp inside the filter graph, then
# reassign ordinal time before any muxer sees it. Not a malformed Ogg replay.
with tempfile.TemporaryDirectory(prefix="vanta-ordinal-test-") as d:
    dest = pathlib.Path(d) / "ordinal.mp4"
    run("ffmpeg", "-v", "error", "-xerror", "-nostdin", "-n", "-f", "lavfi", "-i",
        "testsrc2=s=320x180:r=24:d=7", "-an", "-vf",
        "settb=1/24,setpts='if(eq(N,0),378,N)',settb=1/24,setpts=N,setsar=1",
        "-frames:v", "168", "-r", "24", "-fps_mode", "cfr", "-c:v", "libx264",
        "-preset", "veryfast", "-crf", "18", "-g", "24", "-keyint_min", "24",
        "-sc_threshold", "0", "-x264-params", "open-gop=0", "-threads", "4",
        "-pix_fmt", "yuv420p", "-video_track_timescale", "90000", str(dest))
    v = next(s for s in probe(dest)["streams"] if s["codec_type"] == "video")
    assert int(v["nb_frames"]) == 168 and v["avg_frame_rate"] == "24/1"
    assert abs(float(v["start_time"])) < .002 and abs(float(v["duration"]) - 7) < .002
    run("ffmpeg", "-v", "error", "-xerror", "-nostdin", "-i", str(dest), "-f", "null", "-")
    print("Ordinal clock filter regression: 168 frames, 24 fps, seven seconds, strict decode passed.")
