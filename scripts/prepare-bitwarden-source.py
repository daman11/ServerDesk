"""Create deterministic OSS-only corresponding source; never extract or execute it."""
import gzip
import sys
import tarfile
from pathlib import Path, PurePosixPath

source, destination = map(Path, sys.argv[1:])
with source.open("rb") as raw, tarfile.open(fileobj=raw, mode="r:gz") as upstream:
    members = upstream.getmembers()
    roots = {PurePosixPath(m.name).parts[0] for m in members}
    if len(roots) != 1:
        raise ValueError("Unexpected upstream source roots")
    with destination.open("wb") as output, gzip.GzipFile(filename="", mode="wb", fileobj=output, mtime=0) as compressed:
        with tarfile.open(fileobj=compressed, mode="w", format=tarfile.PAX_FORMAT) as target:
            for member in members:
                parts = PurePosixPath(member.name).parts
                if member.name.startswith("/") or ".." in parts:
                    raise ValueError("Unsafe source member")
                # Upstream explicitly identifies this folder as commercially licensed.
                if len(parts) > 1 and parts[1] == "bitwarden_license":
                    continue
                target.addfile(member, upstream.extractfile(member) if member.isfile() else None)
print("Prepared OSS corresponding source without commercial modules")
