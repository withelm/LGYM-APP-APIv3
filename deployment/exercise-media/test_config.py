"""Negative deployment guards; synthetic config only, no Cloudflare account access."""

import copy
import json
import subprocess
import sys
import tomllib
import unittest

from validate_config import ROOT, validate


class ConfigTests(unittest.TestCase):
    def setUp(self):
        self.config = tomllib.loads((ROOT / "wrangler.toml").read_text(encoding="utf-8"))
        self.policy = json.loads((ROOT / "bucket-policy.example.json").read_text(encoding="utf-8"))

    def check(self, **kwargs):
        validate(self.config, self.policy, **kwargs)

    def owned_host(self, host="exercise-media-dev.owner.test"):
        dev = self.config["env"]["dev"]
        dev["vars"]["EXPECTED_HOST"] = host
        dev["routes"] = [{"pattern": host, "custom_domain": True}]

    def test_optimized_python_cannot_bypass_guards(self):
        result = subprocess.run([sys.executable, "-B", "-O", str(ROOT / "validate_config.py"), "--env", "dev", "--public-base-url", "https://wrong.owner.test"], capture_output=True, text=True, check=False)
        self.assertNotEqual(result.returncode, 0)
        self.assertNotIn("Local structure/isolation passed", result.stdout)

    def test_templates_pass_only_explicit_local_mode(self):
        self.check(allow_examples=True)
        with self.assertRaises(ValueError):
            self.check()

    def test_owned_host_must_match_api_origin(self):
        self.owned_host()
        self.check(public_base_url="https://exercise-media-dev.owner.test")
        for origin in ["http://exercise-media-dev.owner.test", "https://wrong.owner.test", "https://exercise-media-dev.owner.test:8443", "https://exercise-media-dev.owner.test/path", "https://exercise-media-dev.owner.test?x=1", "https://user@exercise-media-dev.owner.test"]:
            with self.subTest(origin=origin), self.assertRaises(ValueError):
                self.check(public_base_url=origin)

    def test_dev_workers_dev_alternative_is_explicit(self):
        dev = self.config["env"]["dev"]
        dev["vars"]["EXPECTED_HOST"] = "lgym-exercise-media-dev.owner.workers.dev"
        dev["workers_dev"] = True
        self.check(public_base_url="https://lgym-exercise-media-dev.owner.workers.dev")
        dev["workers_dev"] = False
        with self.assertRaises(ValueError):
            self.check()

    def test_production_cannot_use_workers_dev(self):
        prod = self.config["env"]["production"]
        prod["vars"]["EXPECTED_HOST"] = "lgym-exercise-media-production.owner.workers.dev"
        prod["workers_dev"] = True
        with self.assertRaises(ValueError):
            self.check(allow_examples=True)

    def test_raw_bucket_or_wrong_domain_cannot_be_public_origin(self):
        for host in ["r2.dev", "r2.cloudflarestorage.com", "raw.r2.dev", "account.r2.cloudflarestorage.com", "*.owner.test", "exercises.example.com", "host.invalid", "owner.test:443"]:
            with self.subTest(host=host), self.assertRaises(ValueError):
                self.owned_host(host)
                self.check()

    def test_environments_cannot_share_public_host(self):
        self.owned_host()
        prod = self.config["env"]["production"]
        prod["vars"]["EXPECTED_HOST"] = "exercise-media-dev.owner.test"
        prod["routes"] = [{"pattern": "exercise-media-dev.owner.test", "custom_domain": True}]
        with self.assertRaises(ValueError):
            self.check()

    def test_wrong_or_split_or_report_binding_fails(self):
        baseline = copy.deepcopy(self.config)
        for bucket in ["lgym-exercises-staging", "lgym-exercises-production", "report-photos", "lgym-exercise-private-dev", "lgym-exercise-published-dev"]:
            self.config = copy.deepcopy(baseline)
            self.config["env"]["dev"]["r2_buckets"][0]["bucket_name"] = bucket
            with self.subTest(bucket=bucket), self.assertRaises(ValueError):
                self.check(allow_examples=True)
        self.config = baseline
        self.config["env"]["dev"]["r2_buckets"].append({"binding": "PRIVATE", "bucket_name": "other"})
        with self.assertRaises(ValueError):
            self.check(allow_examples=True)

    def test_third_environment_and_default_binding_fail(self):
        self.config["env"]["staging"] = copy.deepcopy(self.config["env"]["dev"])
        with self.assertRaises(ValueError):
            self.check(allow_examples=True)
        del self.config["env"]["staging"]
        self.config["r2_buckets"] = []
        with self.assertRaises(ValueError):
            self.check(allow_examples=True)

    def test_host_route_mismatch_and_placeholder_route_fail(self):
        self.config["env"]["dev"]["routes"] = [{"pattern": "wrong.owner.test", "custom_domain": True}]
        with self.assertRaises(ValueError):
            self.check(allow_examples=True)
        self.owned_host()
        self.config["env"]["dev"]["routes"][0]["pattern"] = "wrong.owner.test"
        with self.assertRaises(ValueError):
            self.check()

    def test_raw_public_bucket_cors_and_expiry_fail(self):
        for name, value in [("publicDevelopmentUrlEnabled", True), ("directBucketCustomDomains", ["origin.owner.test"]), ("corsRules", [{"allowed": {"methods": ["PUT"]}}]), ("objectDeletionLifecycleRules", [{"prefix": "", "maxAge": 172800}]), ("objectDeletionLifecycleRules", [{"prefix": "exercises/", "maxAge": 172800}]), ("objectDeletionLifecycleRules", [{"prefix": "staging/"}]), ("objectDeletionLifecycleRules", [{"prefix": "verified/"}])]:
            baseline = copy.deepcopy(self.policy)
            self.policy["dev"][name] = value
            with self.subTest(setting=name, value=value), self.assertRaises(ValueError):
                self.check(allow_examples=True)
            self.policy = baseline


if __name__ == "__main__":
    unittest.main()
