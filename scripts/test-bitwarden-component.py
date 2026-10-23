"""Validate the pinned, separately licensed OSS payload without running it."""
import hashlib
import json
import sys
import tarfile
from pathlib import Path, PurePosixPath

repo = Path(__file__).resolve().parents[1]
manifest = json.loads((repo / "third-party/bitwarden/manifest.json").read_text())
directory = Path(sys.argv[1]) / "components/bitwarden"
runtime = sys.argv[2]

def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()

for name in ["README.md", "manifest.json", "LICENSE.txt", "LICENSE_GPL.txt"]:
    assert (directory / name).read_bytes() == (repo / "third-party/bitwarden" / name).read_bytes(), name
assert digest(directory / manifest["runtimes"][runtime]["file"]) == manifest["runtimes"][runtime]["sha256"]
assert digest(directory / "SOURCE.tar.gz") == manifest["sourceSha256"]
for notice in manifest["notices"]:
    assert digest(directory / notice["file"]) == notice["sha256"]
with tarfile.open(directory / "SOURCE.tar.gz") as source:
    names = {m.name for m in source.getmembers()}
    root = "clients-" + manifest["commit"]
    for name in names:
        path = PurePosixPath(name)
        assert not path.is_absolute() and ".." not in path.parts
        assert path.parts[0] == root
        assert len(path.parts) < 2 or path.parts[1] != "bitwarden_license", name
    for name in ["LICENSE.txt", "LICENSE_GPL.txt", "package.json", "package-lock.json",
                 "apps/cli/src/register-oss-programs.ts"]:
        assert root + "/" + name in names, name
print("PASS Bitwarden OSS: pinned binary/source, exact upstream notices, build inputs and commercial subtree exclusion")
