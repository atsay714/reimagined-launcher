#!/usr/bin/env python3
"""Prepare an upstream merge without publishing; publish only after verification."""
import argparse
import json
from pathlib import Path
import re
import subprocess
import sys
import urllib.request
import xml.etree.ElementTree as ET

sys.path.insert(0, str(Path(__file__).resolve().parent))
from release_metadata import mac_version, provenance

OFFICIAL = "https://github.com/D2R-Reimagined/reimagined-launcher.git"


def git(repo, *args):
    result = subprocess.run(["git", "-C", str(repo), *args], text=True, capture_output=True)
    if result.returncode:
        raise RuntimeError(result.stderr.strip() or result.stdout.strip() or "Git command failed")
    return result.stdout.strip()


def ancestor(repo, older, newer):
    return subprocess.run(["git", "-C", str(repo), "merge-base", "--is-ancestor", older, newer],
                          stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL).returncode == 0


def prepare(repo, upstream_url, upstream_tag, build, output, do_sync=True):
    repo, output = Path(repo).resolve(), Path(output).resolve()
    if not re.fullmatch(r"v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)", upstream_tag):
        raise ValueError("Only stable official version tags can be synchronized")
    if git(repo, "status", "--porcelain", "--untracked-files=no"):
        raise RuntimeError("Prepare requires a clean source checkout")
    base = git(repo, "rev-parse", "HEAD")
    git(repo, "fetch", "--no-tags", upstream_url, "refs/tags/" + upstream_tag + ":refs/tags/" + upstream_tag)
    official = git(repo, "rev-parse", upstream_tag + "^{commit}")
    already_merged = ancestor(repo, official, base)
    if do_sync and not already_merged:
        git(repo, "merge", "--no-ff", "-m", "Merge official launcher " + upstream_tag, official)
    elif not do_sync and not already_merged:
        raise RuntimeError("Release source does not contain the requested official tag")
    candidate = git(repo, "rev-parse", "HEAD")
    upstream_version = ET.parse(repo / "ReimaginedLauncher/ReimaginedLauncher.csproj").getroot().findtext("PropertyGroup/Version")
    if upstream_tag != "v" + upstream_version:
        raise RuntimeError("Official tag and project version do not match")
    version = mac_version(upstream_version, build)
    data = provenance(version, upstream_tag, official, candidate)
    data.update(baseCommit=base, candidateCommit=candidate, publish=not do_sync or not already_merged,
                releaseTag="macos-v" + version)
    output.mkdir(parents=True, exist_ok=True)
    if data["publish"]:
        git(repo, "bundle", "create", str(output / "candidate.bundle"), "HEAD", "refs/tags/" + upstream_tag)
    (output / "source.json").write_text(json.dumps(data, indent=2) + "\n")
    return data


def publish_branch(repo, data, verified=False):
    if not verified or not data.get("publish"):
        raise RuntimeError("Only a successfully verified release candidate may be published")
    base, candidate = data["baseCommit"], data["candidateCommit"]
    if any(not re.fullmatch(r"[0-9a-f]{40}", commit) for commit in (base, candidate)):
        raise ValueError("Invalid candidate commit")
    if not ancestor(repo, base, candidate):
        raise RuntimeError("Candidate does not preserve the original fork history")
    remote = git(repo, "ls-remote", "origin", "refs/heads/macos").split()
    if not remote or remote[0] not in (base, candidate):
        raise RuntimeError("The fork changed while this build ran; rerun against its new head")
    if remote[0] != candidate:
        git(repo, "push", "origin", candidate + ":refs/heads/macos")


def latest_official_tag():
    request = urllib.request.Request(
        "https://api.github.com/repos/D2R-Reimagined/reimagined-launcher/releases/latest",
        headers={"Accept": "application/vnd.github+json", "User-Agent": "Reimagined-Mac-upstream-sync"})
    with urllib.request.urlopen(request, timeout=30) as response:
        release = json.load(response)
    if release.get("draft") or release.get("prerelease"):
        raise RuntimeError("Official release is not stable")
    return release["tag_name"]


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    commands = parser.add_subparsers(dest="command", required=True)
    prepare_parser = commands.add_parser("prepare")
    prepare_parser.add_argument("--mode", choices=("sync", "release"), required=True)
    prepare_parser.add_argument("--build", required=True)
    prepare_parser.add_argument("--output", required=True)
    publish_parser = commands.add_parser("publish")
    publish_parser.add_argument("metadata")
    publish_parser.add_argument("--verified", action="store_true")
    args = parser.parse_args()
    if args.command == "prepare":
        tag = latest_official_tag() if args.mode == "sync" else git(".", "describe", "--tags", "--match", "v[0-9]*", "--abbrev=0")
        print(json.dumps(prepare(".", OFFICIAL, tag, args.build, args.output, args.mode == "sync")))
    else:
        publish_branch(".", json.loads(Path(args.metadata).read_text()), args.verified)
