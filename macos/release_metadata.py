#!/usr/bin/env python3
"""Validate Mac release versions and record exact source provenance."""
import argparse
import json
import re
import xml.etree.ElementTree as ET


def mac_version(upstream_version, build):
    if not re.fullmatch(r"(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)", upstream_version):
        raise ValueError("Expected a three-component official version")
    if not re.fullmatch(r"[1-9][0-9]*", str(build)):
        raise ValueError("Mac build number must be a positive integer")
    version = "1.0." + str(build)
    if int(build) > 65535 or any(int(part) > 65535 for part in upstream_version.split(".")):
        raise ValueError("Version components must fit the .NET assembly version")
    return version


def provenance(version, upstream_tag, upstream_commit, fork_commit):
    upstream_version = upstream_tag.removeprefix("v")
    if not upstream_tag.startswith("v") or mac_version(upstream_version, version.rsplit(".", 1)[-1]) != version:
        raise ValueError("Invalid Mac version or official upstream tag")
    if any(not re.fullmatch(r"[0-9a-f]{40}", commit) for commit in (upstream_commit, fork_commit)):
        raise ValueError("Source provenance requires full commit hashes")
    return dict(version=version, upstreamVersion=upstream_version, upstreamTag=upstream_tag, upstreamCommit=upstream_commit,
                forkCommit=fork_commit, channel="osx-arm64", architecture="arm64",
                bundleIdentifier="com.d2r-reimagined.launcher", signing="ad-hoc/unnotarized")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    commands = parser.add_subparsers(dest="command", required=True)
    version_parser = commands.add_parser("version")
    version_parser.add_argument("project")
    version_parser.add_argument("build")
    provenance_parser = commands.add_parser("provenance")
    for name in ("version", "upstream_tag", "upstream_commit", "fork_commit"):
        provenance_parser.add_argument(name)
    args = parser.parse_args()
    if args.command == "version":
        print(mac_version(ET.parse(args.project).getroot().findtext("PropertyGroup/Version"), args.build))
    else:
        print(json.dumps(provenance(args.version, args.upstream_tag, args.upstream_commit, args.fork_commit), indent=2))
