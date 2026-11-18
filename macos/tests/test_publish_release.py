import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import json

SPEC = importlib.util.spec_from_file_location("publish_release", Path(__file__).parents[1] / "publish_release.py")
publish = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(publish)


class FakeGitHub:
    def __init__(self):
        self.release = None
        self.versions = []
        self.fail_upload = True
        self.created = 0
        self.promoted = 0

    def get(self, tag):
        return self.release

    def published_versions(self):
        return self.versions

    def create(self, tag, commit, notes):
        self.created += 1
        self.release = {"draft": True}

    def upload(self, tag, assets):
        if self.fail_upload:
            raise RuntimeError("interrupted upload")

    def verify(self, tag, assets):
        pass

    def promote(self, tag):
        self.promoted += 1
        self.release["draft"] = False


class PublishReleaseTests(unittest.TestCase):
    def setUp(self):
        self.data = dict(releaseTag="macos-v1.0.2", version="1.0.2", candidateCommit="a" * 40)
        self.github = FakeGitHub()
        self.refs = []

    def run_publish(self):
        publish.publish(self.data, Path("unused"), Path("unused"), self.github, lambda: self.refs.append("verified refs"))

    def test_failed_upload_resumes_the_same_draft_on_retry(self):
        with self.assertRaises(RuntimeError):
            self.run_publish()
        self.github.fail_upload = False
        self.run_publish()
        self.assertEqual(1, self.github.created)
        self.assertEqual(1, self.github.promoted)

    def test_completed_release_is_idempotent(self):
        self.github.release = {"draft": False}
        self.run_publish()
        self.assertEqual([], self.refs)
        self.assertEqual(0, self.github.promoted)

    def test_stale_run_cannot_replace_a_newer_public_release(self):
        self.github.versions = [(1, 0, 3)]
        with self.assertRaises(RuntimeError):
            self.run_publish()
        self.assertEqual([], self.refs)

    def test_draft_lookup_falls_back_to_authenticated_release_list(self):
        client = publish.GitHub(self.data)
        draft = dict(tag_name=self.data["releaseTag"], draft=True, assets=[])
        with patch.object(client, "command", side_effect=[None, json.dumps([[draft]])]):
            self.assertEqual(draft, client.get(self.data["releaseTag"]))

    def test_asset_manifest_covers_all_release_files(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            for name in ("Portable.zip", "RELEASES-osx-arm64", "provenance.json"):
                (root / name).write_text(name)
            publish.write_checksums(root)
            manifest = (root / "SHA256SUMS").read_text()
            self.assertIn("RELEASES-osx-arm64", manifest)
            self.assertNotIn("  SHA256SUMS", manifest)


if __name__ == "__main__":
    unittest.main()
