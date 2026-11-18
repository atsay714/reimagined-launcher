#!/usr/bin/env python3
"""Resume draft uploads safely; never replace a published release or newer latest."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile

sys.path.insert(0, str(Path(__file__).resolve().parent))
from upstream_sync import git, publish_branch

REPOSITORY = "atsay714/reimagined-launcher-macos"


def write_checksums(root):
    root = Path(root)
    files = sorted(p for p in root.iterdir() if p.name != "SHA256SUMS")
    if any(not p.is_file() or p.is_symlink() or "\n" in p.name for p in files):
        raise RuntimeError("Release assets must be regular files")
    (root / "SHA256SUMS").write_text("".join(hashlib.sha256(p.read_bytes()).hexdigest() + "  " + p.name + "\n" for p in files))


def version_tuple(tag):
    match = re.fullmatch(r"macos-v(1)\.(0)\.([1-9][0-9]*)", tag)
    return tuple(map(int, match.groups())) if match else None


class GitHub:
    def __init__(self, data):
        self.data = data

    def command(self, *arguments, missing_ok=False):
        result = subprocess.run(["gh", *arguments], text=True, capture_output=True)
        if result.returncode:
            if missing_ok and "HTTP 404" in result.stderr:
                return None
            raise RuntimeError(result.stderr.strip() or "GitHub command failed")
        return result.stdout

    def get(self, tag):
        result = self.command("api", "repos/" + REPOSITORY + "/releases/tags/" + tag, missing_ok=True)
        if result is not None:
            return json.loads(result)
        pages = json.loads(self.command("api", "--paginate", "--slurp", "repos/" + REPOSITORY + "/releases?per_page=100"))
        return next((release for page in pages for release in page if release["tag_name"] == tag), None)

    def published_versions(self):
        pages = json.loads(self.command("api", "--paginate", "--slurp", "repos/" + REPOSITORY + "/releases?per_page=100"))
        return [version for page in pages for release in page
                if not release["draft"] and not release["prerelease"]
                for version in [version_tuple(release["tag_name"]) ] if version]

    def create(self, tag, commit, notes):
        self.command("release", "create", tag, "--repo", REPOSITORY, "--target", commit,
                     "--draft", "--title", "Mac Launcher " + self.data["version"], "--notes-file", str(notes))

    def upload(self, tag, assets):
        paths = [str(path) for path in sorted(Path(assets).iterdir())]
        self.command("release", "upload", tag, *paths, "--repo", REPOSITORY, "--clobber")

    def verify(self, tag, assets):
        ref = git(".", "ls-remote", "origin", "refs/tags/" + tag).split()
        if not ref or ref[0] != self.data["candidateCommit"]:
            raise RuntimeError("Existing release tag does not match the tested source")
        release = self.get(tag)
        if release is None:
            raise RuntimeError("The uploaded draft is not yet readable; rerun publication to resume it safely")
        local = {p.name: p for p in Path(assets).iterdir()}
        remote = {item["name"]: item for item in release["assets"]}
        if set(local) != set(remote):
            raise RuntimeError("Uploaded assets do not match the complete verified release")
        for name, path in local.items():
            digest = "sha256:" + hashlib.sha256(path.read_bytes()).hexdigest()
            if remote[name]["size"] != path.stat().st_size:
                raise RuntimeError("Uploaded asset size mismatch: " + name)
            if remote[name].get("digest"):
                if remote[name]["digest"].lower() != digest:
                    raise RuntimeError("Uploaded checksum mismatch: " + name)
            else:
                with tempfile.TemporaryDirectory() as temp:
                    self.command("release", "download", tag, "--repo", REPOSITORY, "--pattern", name, "--dir", temp)
                    if hashlib.sha256((Path(temp) / name).read_bytes()).hexdigest() != digest[7:]:
                        raise RuntimeError("Downloaded asset checksum mismatch: " + name)

    def promote(self, tag):
        self.command("release", "edit", tag, "--repo", REPOSITORY, "--draft=false", "--latest")


def publish(data, assets, notes, github, publish_refs):
    tag, version = data["releaseTag"], version_tuple(data["releaseTag"])
    if version is None or tag != "macos-v" + data["version"]:
        raise ValueError("Invalid Mac release identity")
    existing = github.get(tag)
    if existing is not None and not existing["draft"]:
        github.verify(tag, assets)
        print("Matching release already published; no changes made.")
        return
    if any(newer > version for newer in github.published_versions()):
        raise RuntimeError("A newer Mac release is already public; refusing to publish this stale run")
    publish_refs()
    if existing is None:
        github.create(tag, data["candidateCommit"], notes)
    github.upload(tag, assets)
    github.verify(tag, assets)
    if any(newer > version for newer in github.published_versions()):
        raise RuntimeError("A newer release appeared while uploading; retaining this build as a draft")
    publish_refs()
    github.promote(tag)
    print("Published https://github.com/" + REPOSITORY + "/releases/tag/" + tag)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    commands = parser.add_subparsers(dest="command", required=True)
    checksum_parser = commands.add_parser("checksums")
    checksum_parser.add_argument("directory")
    release_parser = commands.add_parser("publish")
    for name in ("source", "assets", "notes"):
        release_parser.add_argument(name)
    args = parser.parse_args()
    if args.command == "checksums":
        write_checksums(args.directory)
    else:
        data = json.loads(Path(args.source).read_text())
        allow_workflows = os.environ.get("SYNC_WORKFLOW_PERMISSION") == "true"
        publish(data, Path(args.assets), Path(args.notes), GitHub(data),
                lambda: publish_branch(".", data, verified=True, allow_workflow_changes=allow_workflows))
