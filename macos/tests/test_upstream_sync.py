import importlib.util
import json
from pathlib import Path
import subprocess
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location("upstream_sync", Path(__file__).parents[1] / "upstream_sync.py")
sync = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(sync)


def git(repo, *args):
    return subprocess.check_output(["git", "-C", str(repo), *args], text=True, stderr=subprocess.PIPE).strip()


class UpstreamSyncTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.official = self.root / "official"
        self.official.mkdir()
        git(self.official, "init", "-b", "main")
        git(self.official, "config", "user.name", "Fixture")
        git(self.official, "config", "user.email", "fixture@example.invalid")
        project = self.official / "ReimaginedLauncher" / "ReimaginedLauncher.csproj"
        project.parent.mkdir()
        project.write_text("<Project><PropertyGroup><Version>1.0.0</Version></PropertyGroup></Project>")
        (self.official / "shared").write_text("base\n")
        git(self.official, "add", ".")
        git(self.official, "commit", "-m", "base")
        git(self.official, "tag", "v1.0.0")
        self.fork = self.root / "fork"
        subprocess.run(["git", "clone", "--quiet", str(self.official), str(self.fork)], check=True)
        git(self.fork, "config", "user.name", "Fixture")
        git(self.fork, "config", "user.email", "fixture@example.invalid")
        git(self.fork, "checkout", "-b", "macos")
        (self.fork / "mac-patch").write_text("preserve\n")
        git(self.fork, "add", ".")
        git(self.fork, "commit", "-m", "Mac patch")
        self.remote = self.root / "fork.git"
        subprocess.run(["git", "clone", "--bare", "--quiet", str(self.fork), str(self.remote)], check=True)
        git(self.fork, "remote", "set-url", "origin", str(self.remote))
        self.base = git(self.fork, "rev-parse", "HEAD")

    def tearDown(self):
        self.temp.cleanup()

    def release(self, conflict=False):
        project = self.official / "ReimaginedLauncher" / "ReimaginedLauncher.csproj"
        project.write_text(project.read_text().replace("1.0.0", "1.0.1"))
        (self.official / "shared").write_text("official\n")
        git(self.official, "add", ".")
        git(self.official, "commit", "-m", "upstream update")
        git(self.official, "tag", "v1.0.1")
        if conflict:
            (self.fork / "shared").write_text("Mac conflict\n")
            git(self.fork, "add", ".")
            git(self.fork, "commit", "-m", "conflicting Mac fix")

    def prepare(self, tag="v1.0.1", do_sync=True):
        return sync.prepare(self.fork, str(self.official), tag, "2", self.root / "output", do_sync)

    def test_already_merged_stable_release_is_noop(self):
        result = self.prepare("v1.0.0")
        self.assertFalse(result["publish"])
        self.assertEqual(self.base, git(self.remote, "rev-parse", "macos"))

    def test_release_cli_works_from_a_fresh_fork_without_official_tags(self):
        git(self.fork, "tag", "-d", "v1.0.0")
        result = subprocess.run(["python3", str(Path(sync.__file__).resolve()), "prepare", "--mode", "release",
                                 "--build", "2", "--output", str(self.root / "output"),
                                 "--upstream-url", str(self.official)], cwd=self.fork, text=True, capture_output=True)
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual("v1.0.0", json.loads(result.stdout)["upstreamTag"])

    def test_clean_candidate_retains_patch_without_advancing_remote(self):
        self.release()
        result = self.prepare()
        self.assertTrue(result["publish"])
        self.assertEqual("1.0.2", result["version"])
        self.assertEqual("preserve", git(self.fork, "show", result["candidateCommit"] + ":mac-patch"))
        self.assertEqual(self.base, git(self.remote, "rev-parse", "macos"))
        self.assertTrue((self.root / "output" / "candidate.bundle").is_file())

    def test_conflict_does_not_advance_remote_or_export_candidate(self):
        self.release(conflict=True)
        with self.assertRaises(RuntimeError):
            self.prepare()
        self.assertEqual(self.base, git(self.remote, "rev-parse", "macos"))
        self.assertFalse((self.root / "output" / "candidate.bundle").exists())

    def test_failed_verification_cannot_publish(self):
        self.release()
        result = self.prepare()
        with self.assertRaises(RuntimeError):
            sync.publish_branch(self.fork, result, verified=False)
        self.assertEqual(self.base, git(self.remote, "rev-parse", "macos"))

    def test_verified_candidate_can_publish(self):
        self.release()
        result = self.prepare()
        sync.publish_branch(self.fork, result, verified=True)
        self.assertEqual(result["candidateCommit"], git(self.remote, "rev-parse", "macos"))

    def test_concurrent_remote_commit_is_preserved(self):
        self.release()
        result = self.prepare()
        other = self.root / "other"
        subprocess.run(["git", "clone", "--quiet", "-b", "macos", str(self.remote), str(other)], check=True)
        git(other, "config", "user.name", "Fixture")
        git(other, "config", "user.email", "fixture@example.invalid")
        (other / "new-fix").write_text("keep\n")
        git(other, "add", ".")
        git(other, "commit", "-m", "concurrent fix")
        git(other, "push", "origin", "macos")
        expected = git(other, "rev-parse", "HEAD")
        with self.assertRaises(RuntimeError):
            sync.publish_branch(self.fork, result, verified=True)
        self.assertEqual(expected, git(self.remote, "rev-parse", "macos"))


if __name__ == "__main__":
    unittest.main()
