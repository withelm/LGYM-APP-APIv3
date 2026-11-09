"""Validate exercise-only deployment templates without credentials or network writes."""

import argparse
import json
import tomllib
from pathlib import Path
from urllib.parse import urlparse


def validate(allow_examples=False):
    root = Path(__file__).parent
    config = tomllib.loads((root / "wrangler.toml").read_text(encoding="utf-8"))
    assert config["workers_dev"] is False and config["preview_urls"] is False
    assert "r2_buckets" not in config, "No default/public bucket binding is allowed"
    buckets = []
    hosts = []
    for name in ("staging", "production"):
        environment = config["env"][name]
        host = environment["vars"]["EXPECTED_HOST"]
        assert host and "/" not in host and ":" not in host and "*" not in host
        assert environment["routes"] == [{"pattern": host, "custom_domain": True}]
        assert len(environment["r2_buckets"]) == 1
        binding = environment["r2_buckets"][0]
        assert binding["binding"] == "PUBLISHED_EXERCISE_MEDIA"
        assert "published" in binding["bucket_name"] and "report" not in binding["bucket_name"]
        buckets.append(binding["bucket_name"])
        hosts.append(host)
        if not allow_examples:
            assert not host.endswith(".example.com"), "Replace example hostname before deployment"
    assert len(set(buckets)) == 2 and len(set(hosts)) == 2
    cors = json.loads((root / "private-cors.example.json").read_text(encoding="utf-8"))
    assert len(cors["rules"]) == 1
    allowed = cors["rules"][0]["allowed"]
    assert allowed["methods"] == ["PUT"] and allowed["headers"] == ["Content-Type"]
    assert allowed["origins"] and all(urlparse(origin).scheme == "https" and "*" not in origin for origin in allowed["origins"])
    if not allow_examples:
        assert all(not urlparse(origin).hostname.endswith(".example.com") for origin in allowed["origins"])
    lifecycle = json.loads((root / "private-lifecycle.example.json").read_text(encoding="utf-8"))
    rule = lifecycle["rules"][0]
    assert rule["enabled"] is True and rule["conditions"]["prefix"] == ""
    assert rule["deleteObjectsTransition"]["condition"] == {"type": "Age", "maxAge": 172800}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--allow-example-hosts", action="store_true")
    args = parser.parse_args()
    validate(args.allow_example_hosts)
    print("Configuration structure/isolation passed; real provisioning and access behavior remain unverified.")
