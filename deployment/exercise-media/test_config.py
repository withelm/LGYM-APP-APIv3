"""Operator values and negative guards; no Cloudflare account access."""

import copy
import subprocess
import sys
import tomllib
import unittest

from validate_config import ROOT, validate


class ConfigTests(unittest.TestCase):
    def setUp(self):
        self.config = tomllib.loads((ROOT / "wrangler.toml").read_text(encoding="utf-8"))

    def configured(self, bucket="exercise-assets-alpha", host="media.operator.test", name="exercise-worker"):
        self.config["name"] = name
        self.config["vars"]["EXPECTED_HOST"] = host
        self.config["r2_buckets"][0]["bucket_name"] = bucket
        self.config["routes"] = [{"pattern": host, "custom_domain": True}]
        return self.config

    def test_example_is_explicit_and_fail_closed(self):
        validate(self.config, allow_placeholders=True)
        with self.assertRaises(ValueError):
            validate(self.config)
        self.config["workers_dev"] = True
        with self.assertRaises(ValueError):
            validate(self.config, allow_placeholders=True)

    def test_two_independent_operator_buckets_and_worker_names(self):
        for bucket, name in [("exercise-assets-alpha", "media-worker-one"), ("assets-client-02", "media-worker-two")]:
            with self.subTest(bucket=bucket, name=name):
                validate(self.configured(bucket=bucket, name=name), expected_bucket=bucket, public_base_url="https://media.operator.test")

    def test_bucket_syntax_not_developer_allowlist(self):
        baseline = copy.deepcopy(self.configured())
        for bucket in ["", "ab", "a" * 64, "-assets", "assets-", "Assets", "assets_name", "assets.name"]:
            self.config = copy.deepcopy(baseline)
            self.config["r2_buckets"][0]["bucket_name"] = bucket
            with self.subTest(bucket=bucket), self.assertRaises(ValueError):
                validate(self.config)
        self.config = baseline
        self.config["r2_buckets"][0]["bucket_name"] = "reports-and-exercises"
        validate(self.config)  # Name fragments do not establish storage ownership.

    def test_backend_bucket_and_report_isolation(self):
        self.configured()
        with self.assertRaises(ValueError):
            validate(self.config, expected_bucket="another-operator-bucket")
        with self.assertRaises(ValueError):
            validate(self.config, report_bucket="exercise-assets-alpha")
        validate(self.config, report_bucket="private-photos")

    def test_wrong_or_extra_binding_and_preview_bucket_fail(self):
        baseline = copy.deepcopy(self.configured())
        for bindings in [[], [{"binding": "REPORTS", "bucket_name": "exercise-assets-alpha"}], baseline["r2_buckets"] * 2, [{"binding": "EXERCISE_MEDIA", "bucket_name": "exercise-assets-alpha", "preview_bucket_name": "other-bucket"}]]:
            self.config["r2_buckets"] = bindings
            with self.subTest(bindings=bindings), self.assertRaises(ValueError):
                validate(self.config)

    def test_worker_host_and_api_origin_match(self):
        self.configured()
        validate(self.config, public_base_url="https://media.operator.test:443/")
        for origin in ["http://media.operator.test", "https://wrong.operator.test", "https://media.operator.test:8443", "https://media.operator.test/path", "https://media.operator.test?x=1", "https://media.operator.test#part", "https://user@media.operator.test"]:
            with self.subTest(origin=origin), self.assertRaises(ValueError):
                validate(self.config, public_base_url=origin)

    def test_raw_r2_and_invalid_hosts_fail(self):
        for host in ["r2.dev", "r2.cloudflarestorage.com", "raw.r2.dev", "account.r2.cloudflarestorage.com", "*.operator.test", "host.invalid", "host:443"]:
            self.configured(host=host)
            with self.subTest(host=host), self.assertRaises(ValueError):
                validate(self.config)

    def test_workers_dev_is_operator_selected_not_bucket_derived(self):
        self.configured(bucket="independent-assets", host="exercise-worker.account.workers.dev")
        self.config["workers_dev"] = True
        self.config["routes"] = []
        validate(self.config, public_base_url="https://exercise-worker.account.workers.dev")
        self.config["workers_dev"] = False
        with self.assertRaises(ValueError):
            validate(self.config)

    def test_wrong_route_entrypoint_and_preview_fail(self):
        self.configured()
        self.config["routes"][0]["pattern"] = "wrong.operator.test"
        with self.assertRaises(ValueError):
            validate(self.config)
        self.configured()
        self.config["main"] = "unreviewed.mjs"
        with self.assertRaises(ValueError):
            validate(self.config)
        self.config["main"] = "worker.mjs"
        self.config["preview_urls"] = True
        with self.assertRaises(ValueError):
            validate(self.config)

    def test_existing_named_environment_uses_its_explicit_binding(self):
        self.configured()
        selected = {key: self.config[key] for key in ("name", "vars", "r2_buckets", "routes")}
        self.config["env"] = {"operator": selected}
        validate(self.config, environment="operator")
        del selected["r2_buckets"]
        with self.assertRaises(ValueError):
            validate(self.config, environment="operator")

    def test_optimized_python_cannot_bypass_guards(self):
        result = subprocess.run([sys.executable, "-B", "-O", str(ROOT / "validate_config.py")], capture_output=True, text=True, check=False)
        self.assertNotEqual(result.returncode, 0)
        self.assertNotIn("Local configuration passed", result.stdout)


if __name__ == "__main__":
    unittest.main()
