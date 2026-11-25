"""Bounded, external corpus acquisition. No media is committed or packaged."""
import argparse
import hashlib
import json
import pathlib
import subprocess
import sys
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
    video = next(s for s in meta["streams"] if s["codec_type"] == "video" and not s.get("disposition", {}).get("attached_pic"))
    # Never upscale; preserve portrait orientation within the bounding dimensions.
    w = min(item["width"], video["width"])
    h = min(item["height"], video["height"])
    fps = item["fps"]
    vf = f"scale={w}:{h}:force_original_aspect_ratio=decrease:force_divisible_by=2,fps={fps},setsar=1,setpts=PTS-STARTPTS"
    run("ffmpeg", "-v", "error", "-xerror", "-nostdin", "-n", "-i", str(raw), "-t", str(item["seconds"]),
        "-map", "0:V:0", "-map", "0:a:0?", "-vf", vf, "-af", "asetpts=PTS-STARTPTS",
        "-r", str(fps), "-fps_mode", "cfr",
        "-c:v", "libx264", "-preset", "veryfast", "-crf", "18", "-g", str(fps), "-keyint_min", str(fps),
        "-sc_threshold", "0", "-x264-params", "open-gop=0", "-threads", "4", "-pix_fmt", "yuv420p",
        "-c:a", "aac", "-ac", "2", "-ar", "48000", "-b:a", "128k", "-map_metadata", "-1",
        "-map_chapters", "-1", "-video_track_timescale", "90000", str(dest))
    normalized = probe(dest)
    v = next(s for s in normalized['streams'] if s['codec_type'] == 'video')
    from fractions import Fraction
    if Fraction(v['avg_frame_rate']) != fps or abs(float(v['duration']) - int(v['nb_frames']) / fps) > .002:
        raise ValueError('Normalized frame rate or duration inconsistent')
    return meta, normalized

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
            record["raw_bytes"] = raw.stat().st_size
            record["raw_probe"], record["fixture_probe"] = normalize(item, raw, dest)
            record["fixture_sha256"] = digest(dest)
            if digest(raw) != record["raw_sha256"]:
                raise ValueError("Source changed")
            record["status"] = "ready"
        except Exception as exc:
            record["error"] = f"{type(exc).__name__}: {exc}"
        records.append(record)
        (args.report / "intake.json").write_text(json.dumps(records, ensure_ascii=False, indent=2), encoding="utf-8")
        print(item["id"], record["status"], record.get("error", ""), flush=True)
    return 0 if records and all(x["status"] == "ready" for x in records) else 1

if __name__ == "__main__":
    sys.exit(main())
