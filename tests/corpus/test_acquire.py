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
    partial = dest.with_suffix(".bin.partial")
    partial.write_bytes(b"preexisting partial")
    with patch.object(prepare.urllib.request, "build_opener", return_value=Opener()):
        try:
            prepare.acquire(item, dest)
            raise AssertionError("existing partial was overwritten")
        except FileExistsError:
            assert partial.read_bytes() == b"preexisting partial" and not dest.exists()
    partial.unlink()
    from urllib.error import HTTPError
    for code, expected in ((503, 3), (403, 1)):
        with patch.object(prepare, "_acquire_once", side_effect=HTTPError(URL,code,"test",None,None)) as acquire, patch.object(prepare.time,"sleep") as sleep:
            try:
                prepare.acquire(item,dest)
                raise AssertionError("HTTP error swallowed")
            except HTTPError:
                assert acquire.call_count == expected and sleep.call_count == expected-1
    with patch.object(prepare, "_acquire_once", side_effect=[HTTPError(URL,504,"test",None,None),item["sha256"]]) as acquire, patch.object(prepare.time,"sleep"):
        assert prepare.acquire(item,dest)==item["sha256"] and acquire.call_count==2
catalog = [{"id": "first"}, {"id": "second"}]
assert prepare.select_sources(catalog) == catalog
assert prepare.select_sources(catalog, "second") == [catalog[1]]
for items, only in ((catalog, "missing"), (catalog, ""), (catalog + [catalog[0]], "first"), ([], None)):
    try:
        prepare.select_sources(items, only)
        raise AssertionError("Invalid selection accepted")
    except ValueError:
        pass
print("Streaming acquisition pin, content, retry, cleanup and exact fixture selection tests passed.")
