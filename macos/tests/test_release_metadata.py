import importlib.util
from pathlib import Path
import unittest

SPEC = importlib.util.spec_from_file_location("release_metadata", Path(__file__).parents[1] / "release_metadata.py")
metadata = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(metadata)


class ReleaseMetadataTests(unittest.TestCase):
    def test_mac_build_orders_after_official_version_and_other_mac_builds(self):
        self.assertEqual("1.0.2", metadata.mac_version("0.19.13", "2"))
        self.assertGreater(tuple(map(int, metadata.mac_version("0.19.13", "2").split("."))), (1, 0, 1))

    def test_invalid_versions_and_builds_are_rejected(self):
        for version, build in [("v0.19.13", "1"), ("0.19.13;exit", "1"), ("0.19.13", "0"), ("0.19.13", "-1"), ("0.19.13", "65536"), ("0.19.13", "1\n")]:
            with self.subTest(version=version, build=build), self.assertRaises(ValueError):
                metadata.mac_version(version, build)

    def test_provenance_identifies_the_exact_release_source(self):
        result = metadata.provenance("1.0.2", "v0.19.13", "a" * 40, "b" * 40)
        self.assertEqual("a" * 40, result["upstreamCommit"])
        self.assertEqual("b" * 40, result["forkCommit"])
        self.assertEqual("osx-arm64", result["channel"])

    def test_mismatched_upstream_and_invalid_commit_are_rejected(self):
        for tag, commit in [("v0.19.13-invalid", "a" * 40), ("v0.19.13", "unknown")]:
            with self.subTest(tag=tag), self.assertRaises(ValueError):
                metadata.provenance("1.0.1", tag, commit, "b" * 40)


if __name__ == "__main__":
    unittest.main()
