"""Test DEB dependencies with APT simulation and isolated synthetic package state.

Does not install packages, use host package state or refresh network indexes.
--fixture uses fake executables to check metadata, not application behavior.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import tarfile
import tempfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
ICU = (70, 72, 73, 74, 75, 76, 77, 78)


def field(deb, name):
    return subprocess.check_output(["dpkg-deb", "-f", str(deb), name], text=True).strip()


def resolve(deb, directory, icu, ssl="libssl3"):
    directory.mkdir()
    for name in ("lists/partial", "cache/archives/partial", "etc/apt.conf.d", "etc/sources.list.d"):
        (directory / name).mkdir(parents=True, exist_ok=True)
    (directory / "etc/sources.list").touch()
    (directory / "etc/apt.conf").touch()
    packages = []
    for group in field(deb, "Depends").split(","):
        choices = [re.match(r"\s*([a-z0-9+.-]+)", part).group(1) for part in group.split("|")]
        if not any(p.startswith("libicu") for p in choices):
            packages.append(ssl if "libssl3" in choices else choices[0])
    packages += [f"libicu{version}" for version in icu]
    (directory / "status").write_text("\n".join(
        f"Package: {name}\nStatus: install ok installed\nArchitecture: amd64\nVersion: 999.0\nDescription: synthetic dependency fixture\n"
        for name in packages), encoding="utf-8")
    config = directory / "fixture.conf"
    config.write_text(f'''Dir::State "{directory}";
Dir::State::status "{directory / 'status'}";
Dir::State::extended_states "{directory / 'extended_states'}";
Dir::State::lists "{directory / 'lists'}";
Dir::Cache "{directory / 'cache'}";
Dir::Cache::pkgcache "";
Dir::Cache::srcpkgcache "";
Dir::Etc "{directory / 'etc'}";
Dir::Etc::main "apt.conf";
Dir::Etc::parts "apt.conf.d";
Dir::Etc::sourcelist "sources.list";
Dir::Etc::sourceparts "sources.list.d";
APT::Architecture "amd64";
APT::Architectures {{ "amd64"; }};
Debug::NoLocking "true";
''', encoding="utf-8")
    return subprocess.run(["apt-get", "--simulate", "--no-install-recommends", "install", str(deb.resolve())],
                          env=dict(os.environ, APT_CONFIG=str(config), LC_ALL="C"),
                          text=True, capture_output=True, timeout=30)


def payload_hashes(deb):
    with tempfile.TemporaryFile() as stream:
        subprocess.run(["dpkg-deb", "--fsys-tarfile", str(deb)], stdout=stream, check=True)
        stream.seek(0)
        with tarfile.open(fileobj=stream) as archive:
            result = {}
            for entry in archive:
                assert not entry.issym() and not entry.islnk(), entry.name
                if entry.isfile():
                    result[entry.name] = (hashlib.file_digest(archive.extractfile(entry), "sha256").hexdigest(),
                                          entry.mode, entry.uid, entry.gid)
            return result


def check(deb, root, previous=None):
    assert field(deb, "Package") == "serverdesk" and field(deb, "Architecture") == "amd64"
    for version in ICU:
        result = resolve(deb, root / f"icu-{version}", (version,))
        assert result.returncode == 0, f"ICU {version}: {result.stdout}\n{result.stderr}"
    for name, providers, ssl in (("multiple", (74, 78), "libssl3"), ("t64", (78,), "libssl3t64")):
        result = resolve(deb, root / name, providers, ssl)
        assert result.returncode == 0, result.stdout + result.stderr
    for name, providers in (("missing", ()), ("unsupported", (69,))):
        result = resolve(deb, root / name, providers)
        assert result.returncode != 0 and "libicu" in result.stdout + result.stderr, result.stdout + result.stderr
    print("PASS 12 APT simulations: ICU 70/72/73/74/75/76/77/78, multiple providers, OpenSSL t64; absent/unsupported ICU rejected")
    if previous:
        result = resolve(previous, root / "previous-icu-78", (78,))
        assert result.returncode != 0 and "libicu" in result.stdout + result.stderr, "Old package did not reproduce the ICU failure"
        original = payload_hashes(previous)
        assert original == payload_hashes(deb), "Installed file bytes, modes or ownership changed"
        subprocess.run(["dpkg", "--compare-versions", field(deb, "Version"), "gt", field(previous, "Version")], check=True)
        print(f"PASS repair: original ICU-78 failure reproduced; {len(original)} installed files unchanged; revision upgrades the old DEB")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("deb", type=Path, nargs="?")
    parser.add_argument("--previous", type=Path)
    parser.add_argument("--fixture", action="store_true")
    args = parser.parse_args()
    if bool(args.deb) == args.fixture:
        parser.error("provide either a DEB path or --fixture")
    with tempfile.TemporaryDirectory(prefix="serverdesk-deb-dependencies-") as temporary:
        root = Path(temporary)
        if args.fixture:
            version = ET.parse(ROOT / "Directory.Build.props").findtext("PropertyGroup/Version")
            payload = root / "payload"
            (payload / "components/bitwarden").mkdir(parents=True)
            for name in ("ServerDesk.App", "components/bitwarden/bw"):
                (payload / name).write_text("#!/bin/sh\nexit 0\n", encoding="utf-8")
            (payload / "RELEASE.json").write_text(json.dumps({"version": version, "runtime": "linux-x64"}), encoding="utf-8")
            output = root / "output"
            script = str(ROOT / "scripts/package-deb.sh")
            subprocess.run(["bash", script, version, str(payload), "--revision", "1", "--output-dir", str(output), "--deb-only"], check=True)
            deb = output / f"serverdesk_{version}-1_amd64.deb"
            assert field(deb, "Version") == version + "-1" and not tuple(output.glob("*.tar.gz"))
            regular = root / "regular"
            subprocess.run(["bash", script, version, str(payload), "--output-dir", str(regular)], check=True)
            assert field(regular / f"serverdesk_{version}_amd64.deb", "Version") == version
            assert (regular / f"ServerDesk-{version}-linux-x64.tar.gz").exists()
            for options in (("--revision", "0"), ("--revision", ""), ("--output-dir",),
                            ("--output-dir", str(ROOT / "artifacts/releases/fixture-forbidden"))):
                failure = subprocess.run(["bash", script, version, str(payload), *options], capture_output=True, text=True)
                assert failure.returncode != 0, options
            print("PASS packager: normal/revised versions, DEB-only output, invalid arguments and frozen-release protection")
        else:
            deb = args.deb.resolve()
        check(deb, root, args.previous.resolve() if args.previous else None)


if __name__ == "__main__":
    main()
