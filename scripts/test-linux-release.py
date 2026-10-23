"""Check the actual TAR/DEB payload, without installing into the host OS."""
from pathlib import Path
import hashlib
import io
import json
import re
import subprocess
import sys
import tarfile
import tempfile
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
version = ET.parse(root / "Directory.Build.props").findtext("PropertyGroup/Version")
payload = root / f"artifacts/linux-x64-{version}"
subprocess.run([sys.executable, str(root / "scripts/test-bitwarden-component.py"), str(payload), "linux-x64"], check=True)
expected = {p.relative_to(payload).as_posix(): p for p in payload.rglob("*") if p.is_file()}
deb = root / f"artifacts/serverdesk_{version}_amd64.deb"
assert subprocess.check_output(["dpkg-deb", "-f", str(deb), "Version"], text=True).strip() == version
assert subprocess.check_output(["dpkg-deb", "-f", str(deb), "Architecture"], text=True).strip() == "amd64"

def check(archive, prefix):
    actual = {}
    for member in archive.getmembers():
        name = member.name.removeprefix("./")
        assert not member.issym() and not member.islnk(), name
        assert not re.search(r"(^|/)(\.tools|credentials|logs|\.git)/|integration_key|\.(db|trx|pem|pfx|pdb)$", name), name
        if member.isfile() and name.startswith(prefix):
            relative = name[len(prefix):]
            actual[relative] = member
            assert relative in expected, relative
            stream = archive.extractfile(member)
            assert hashlib.file_digest(stream, "sha256").digest() == hashlib.sha256(expected[relative].read_bytes()).digest(), relative
    assert set(actual) == set(expected), set(expected) - set(actual)
    assert actual["ServerDesk.App"].mode & 0o111
    assert actual["components/bitwarden/bw"].mode & 0o111

with tarfile.open(root / f"artifacts/ServerDesk-{version}-linux-x64.tar.gz") as archive:
    check(archive, "")
    print(f"PASS TAR: every one of {len(expected)} payload files matches; executable permissions retained")

with tempfile.TemporaryFile() as content:
    subprocess.run(["dpkg-deb", "--fsys-tarfile", str(deb)], stdout=content, check=True)
    content.seek(0)
    with tarfile.open(fileobj=content) as archive:
        check(archive, "opt/serverdesk/")
        assert archive.getmember("./usr/bin/serverdesk").mode & 0o111
        launcher = archive.extractfile("./usr/bin/serverdesk").read().decode()
        assert 'exec /opt/serverdesk/ServerDesk.App "$@"' in launcher
        desktop = archive.extractfile("./usr/share/applications/serverdesk.desktop").read().decode()
        assert "Exec=/opt/serverdesk/ServerDesk.App" in desktop
        assert "StartupWMClass=ServerDesk.App" in desktop
        assert all(m.uid == 0 and m.gid == 0 for m in archive.getmembers())
print(f"PASS DEB: version {version}, amd64, all payload bytes, root ownership, launcher and desktop entry")
