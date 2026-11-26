"""Acquisition stream hash checks without network."""
import hashlib
import io
import pathlib
import tempfile
from unittest.mock import patch
import prepare

URL = "https://upload.wikimedia.org/wikipedia/commons/test"
class Response(io.BytesIO):
    def geturl(self):
        return URL
class Opener:
    def open(self, request, timeout):
        return Response(payload)
payload = b"source-byte-test" * 100000
item = {"url": URL, "sha256": hashlib.sha256(payload).hexdigest(),
        "sha1": hashlib.sha1(payload).hexdigest()}
with tempfile.TemporaryDirectory() as directory:
    dest = pathlib.Path(directory) / "raw.bin"
    with patch.object(prepare.urllib.request, "build_opener", return_value=Opener()):
        assert prepare.acquire(item, dest) == item["sha256"]
        assert prepare.digest(dest) == item["sha256"]
        assert dest.read_bytes() == payload
        dest.unlink()
        for bad in (dict(item, sha256="0"*64), dict(item, sha1="0"*40)):
            try:
                prepare.acquire(bad, dest)
                raise AssertionError("bad pin accepted")
            except ValueError:
                assert not dest.exists() and not dest.with_suffix(".bin.partial").exists()
        with patch.object(prepare, "MAX_BYTES", 1024):
            try:
                prepare.acquire(item, dest)
                raise AssertionError("oversize source accepted")
            except ValueError:
                assert not dest.exists() and not dest.with_suffix(".bin.partial").exists()
print("Streaming acquisition pin, content and cleanup tests passed.")
