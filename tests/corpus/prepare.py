"""Bounded, external corpus acquisition. No media is committed or packaged."""
import argparse
import hashlib
import json
import pathlib
import subprocess
import sys
import tempfile
import urllib.request
import urllib.parse

ROOT = pathlib.Path(__file__).resolve().parent
HOSTS = {"upload.wikimedia.org", "svs.gsfc.nasa.gov"}
MAX_BYTES = 140 * 1024 * 1024

def digest(path, algorithm="sha256"):
    h = hashlib.new(algorithm)
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()

def run(*args):
    result = subprocess.run(args, capture_output=True, text=True, timeout=240)
    if result.returncode or result.stderr.strip():
        raise RuntimeError(f"{args[0]}: {result.stderr[-2000:]}")
    return result.stdout

def probe(path):
    return json.loads(run("ffprobe", "-v", "error", "-show_format", "-show_streams", "-of", "json", str(path)))

def require_url(url):
    p = urllib.parse.urlparse(url)
    if p.scheme != "https" or p.hostname not in HOSTS or p.username or p.password or p.port not in (None, 443):
        raise ValueError("Unapproved media URL")

class SafeRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        require_url(newurl)
        return super().redirect_request(req, fp, code, msg, headers, newurl)

def acquire(item, path):
    require_url(item["url"])
    if path.exists():
        raise FileExistsError(path)
    request = urllib.request.Request(item["url"], headers={"User-Agent": "VANTA-Corpus/0.4 (https://github.com/WhoSia/VANTA; codec research)"})
    opener = urllib.request.build_opener(SafeRedirect())
    temp = path.with_suffix(path.suffix + ".partial")
    try:
        with opener.open(request, timeout=30) as response, temp.open("xb") as output:
            require_url(response.geturl())
            size = 0
            while True:
                chunk = response.read(1024 * 1024)
                if not chunk:
                    break
                size += len(chunk)
                if size > MAX_BYTES:
                    raise ValueError("Media exceeds download limit")
                output.write(chunk)
        if not size:
            raise ValueError("Empty source")
        if item.get("sha1") and digest(temp, "sha1") != item["sha1"]:
            raise ValueError("Published SHA-1 mismatch")
        if item.get("sha256") and digest(temp) != item["sha256"]:
            raise ValueError("Pinned SHA-256 mismatch")
        temp.rename(path)
    finally:
        temp.unlink(missing_ok=True)

def normalize(item, raw, dest):
    meta = probe(raw)
    videos = [s for s in meta["streams"] if s["codec_type"] == "video" and not s.get("disposition", {}).get("attached_pic")]
    if len(videos) != 1:
        raise ValueError(f"Raw input must contain one non-cover video: {len(videos)}")
    video = videos[0]
    # Never upscale; preserve portrait orientation within the bounding dimensions.
    w = min(item["width"], video["width"])
    h = min(item["height"], video["height"])
    fps = item["fps"]
    vf = f"setpts=PTS-STARTPTS,scale={w}:{h}:force_original_aspect_ratio=decrease:force_divisible_by=2,fps={fps},setsar=1"
    # An offset input may lose all video under a shared output duration limit
    # (observed on the pinned BBB Ogg input in FFmpeg 9.1). Prepare the
    # explicitly modified fixture streams independently; never use this in
    # the production audio-copy path.
    start = video.get("start_time")
    if start not in (None, "N/A") and abs(float(start)) > .002:
        with tempfile.TemporaryDirectory(prefix="vanta-offset-", dir=dest.parent) as tmp:
            tmp = pathlib.Path(tmp)
            vid, aud = tmp / "video.mp4", tmp / "audio.m4a"
            run("ffmpeg", "-v", "error", "-xerror", "-nostdin", "-n", "-i", str(raw),
                "-map", f"0:{video['index']}", "-an", "-vf", vf, "-frames:v", str(item["seconds"] * fps),
                "-r", str(fps), "-fps_mode", "cfr", "-c:v", "libx264", "-preset", "veryfast", "-crf", "18",
                "-g", str(fps), "-keyint_min", str(fps), "-sc_threshold", "0", "-x264-params", "open-gop=0",
                "-threads", "4", "-pix_fmt", "yuv420p", "-map_metadata", "-1", "-map_chapters", "-1",
                "-video_track_timescale", "90000", str(vid))
            video_only = probe(vid)
            video_streams = [s for s in video_only["streams"] if s["codec_type"] == "video"]
            if len(video_streams) != 1:
                raise ValueError(f"Offset video-only encode yielded {len(video_streams)} video streams")
            vmeta = video_streams[0]
            duration = int(vmeta["nb_frames"]) / fps
            if duration <= 0:
                raise ValueError("Offset video normalization yielded no frames")
            if any(s["codec_type"] == "audio" for s in meta["streams"]):
                run("ffmpeg", "-v", "error", "-xerror", "-nostdin", "-n", "-i", str(raw),
                    "-map", "0:a:0", "-vn", "-af", f"aresample=48000,asetpts=N/SR/TB,atrim=duration={duration:.9f}",
                    "-c:a", "aac", "-ac", "2", "-ar", "48000", "-b:a", "128k",
                    "-map_metadata", "-1", "-map_chapters", "-1", str(aud))
                run("ffmpeg", "-v", "error", "-xerror", "-nostdin", "-n", "-i", str(vid), "-i", str(aud),
                    "-map", "0:v:0", "-map", "1:a:0", "-c", "copy", "-video_track_timescale", "90000", str(dest))
            else:
                vid.rename(dest)
    else:
        run("ffmpeg", "-v", "error", "-xerror", "-nostdin", "-n", "-i", str(raw), "-t", str(item["seconds"]),
            "-map", f"0:{video['index']}", "-map", "0:a:0?", "-vf", vf, "-af", "aresample=48000,asetpts=N/SR/TB",
            "-r", str(fps), "-fps_mode", "cfr",
            "-c:v", "libx264", "-preset", "veryfast", "-crf", "18", "-g", str(fps), "-keyint_min", str(fps),
            "-sc_threshold", "0", "-x264-params", "open-gop=0", "-threads", "4", "-pix_fmt", "yuv420p",
            "-c:a", "aac", "-ac", "2", "-ar", "48000", "-b:a", "128k", "-map_metadata", "-1",
            "-map_chapters", "-1", "-video_track_timescale", "90000", str(dest))
    normalized = probe(dest)
    videos = [s for s in normalized['streams'] if s['codec_type'] == 'video']
    if len(videos) != 1:
        raise ValueError(f"Normalized video missing or ambiguous; streams={[(s.get('index'), s.get('codec_type')) for s in normalized['streams']]}; raw_video_start={video.get('start_time')}; raw_video_duration={video.get('duration')}")
    v = videos[0]
    from fractions import Fraction
    if int(v['nb_frames']) < 1 or Fraction(v['avg_frame_rate']) != fps or abs(float(v['duration']) - int(v['nb_frames']) / fps) > .002:
        raise ValueError('Normalized frame rate or duration inconsistent')
    for audio in (s for s in normalized["streams"] if s["codec_type"] == "audio"):
        lines = run("ffprobe", "-v", "error", "-select_streams", str(audio["index"]), "-show_entries", "packet=dts", "-of", "csv=p=0", str(dest)).splitlines()
        dts = [int(line.split(",")[0]) for line in lines if line.strip()]
        if not dts or any(a >= b for a, b in zip(dts, dts[1:])):
            raise ValueError("Normalized audio DTS is not strictly increasing")
    return meta, normalized

def diagnose_video(raw):
    """Bounded diagnostic, not a replacement source or an acceptance bypass."""
    result = subprocess.run(
        ["ffmpeg", "-v", "error", "-xerror", "-nostdin", "-i", str(raw),
         "-map", "0:V:0", "-an", "-frames:v", "300", "-fps_mode", "passthrough",
         "-f", "framemd5", "-"],
        capture_output=True, text=True, timeout=60)
    frames = [line for line in result.stdout.splitlines() if line.strip() and not line.startswith("#")]
    return {"exit_code": result.returncode, "decoded_frames_up_to_300": len(frames),
            "first_frame": frames[0] if frames else None,
            "last_frame": frames[-1] if frames else None,
            "error": result.stderr[-2000:]}

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--work", type=pathlib.Path, required=True)
    parser.add_argument("--report", type=pathlib.Path, required=True)
    parser.add_argument("--local", type=pathlib.Path, help="Explicit local replacement inputs for harness smoke, never public-corpus evidence")
    args = parser.parse_args()
    args.work.mkdir(parents=True, exist_ok=False)
    args.report.mkdir(parents=True, exist_ok=True)
    records = []
    for item in json.loads((ROOT / "sources.json").read_text(encoding="utf-8")):
        raw = args.work / (item["id"] + pathlib.Path(item["file"]).suffix)
        dest = args.work / (item["id"] + "-normalized.mp4")
        record = dict(item, status="failed", origin="local-replacement" if args.local else "public", raw=str(raw.resolve()), fixture=str(dest.resolve()))
        try:
            if args.local:
                raw = args.local / item["file"]
                record["raw"] = str(raw.resolve())
            else:
                acquire(item, raw)
            record["raw_sha256"] = digest(raw)
            print("CORPUS_RAW", item["id"], record["raw_sha256"], flush=True)
            record["raw_bytes"] = raw.stat().st_size
            record["raw_probe"] = probe(raw)
            _, record["fixture_probe"] = normalize(item, raw, dest)
            record["fixture_sha256"] = digest(dest)
            if digest(raw) != record["raw_sha256"]:
                raise ValueError("Source changed")
            record["status"] = "ready"
        except Exception as exc:
            record["error"] = f"{type(exc).__name__}: {exc}"
            if "raw_sha256" in record and raw.is_file():
                try:
                    record["raw_decode_diagnostic"] = diagnose_video(raw)
                    print("CORPUS_DIAGNOSTIC", item["id"], json.dumps(record["raw_decode_diagnostic"]), flush=True)
                except Exception as diagnostic_error:
                    record["raw_decode_diagnostic"] = {"error": f"{type(diagnostic_error).__name__}: {diagnostic_error}"}
                    print("CORPUS_DIAGNOSTIC", item["id"], json.dumps(record["raw_decode_diagnostic"]), flush=True)
        records.append(record)
        (args.report / "intake.json").write_text(json.dumps(records, ensure_ascii=False, indent=2), encoding="utf-8")
        print(item["id"], record["status"], record.get("error", ""), flush=True)
    return 0 if records and all(x["status"] == "ready" for x in records) else 1

if __name__ == "__main__":
    sys.exit(main())
