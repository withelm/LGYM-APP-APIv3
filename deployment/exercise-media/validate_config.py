"""Validate secrets-free exercise-only templates; no Cloudflare provisioning or live proof."""

import argparse
import json
import re
import tomllib
from pathlib import Path
from urllib.parse import urlparse

ROOT = Path(__file__).parent
BUCKETS = {"dev": "lgym-exercises-dev", "production": "lgym-exercises-production"}
PLACEHOLDERS = {
    "dev": "REPLACE_WITH_VERIFIED_DEV_WORKER_HOST",
    "production": "REPLACE_WITH_VERIFIED_PRODUCTION_WORKER_HOST",
}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def validate(config=None, policy=None, allow_examples=False, environment="dev", public_base_url=None):
    config = config if config is not None else tomllib.loads((ROOT / "wrangler.toml").read_text(encoding="utf-8"))
    policy = policy if policy is not None else json.loads((ROOT / "bucket-policy.example.json").read_text(encoding="utf-8"))
    require(config["workers_dev"] is False and config["preview_urls"] is False, "Invalid exercise-media configuration")
    require("r2_buckets" not in config and not config.get("routes"), "No default public bucket/route")
    require(set(config["env"]) == set(BUCKETS), "Only existing DEV and future production templates")
    configured_hosts = set()
    for name, bucket in BUCKETS.items():
        entry = config["env"][name]
        require(entry["name"] == f"lgym-exercise-media-{name}", "Invalid exercise-media configuration")
        require(entry.get("preview_urls", False) is False, "Invalid exercise-media configuration")
        require(entry["r2_buckets"] == [{"binding": "EXERCISE_MEDIA", "bucket_name": bucket}], "Exactly one exercise-only binding")
        host = entry["vars"]["EXPECTED_HOST"]
        if host == PLACEHOLDERS[name]:
            require(not entry["workers_dev"] and entry["routes"] == [], "Placeholder must expose no public route")
            require(allow_examples or name != environment, "Replace verified Worker hostname before deployment")
        else:
            require(re.fullmatch(r"(?=.{1,253}$)(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}", host), "Exact DNS hostname required")
            require(host not in ("r2.dev", "r2.cloudflarestorage.com") and not host.endswith((".r2.dev", ".r2.cloudflarestorage.com", ".example.com", ".invalid")), "No raw R2 or example public origin")
            require(host not in configured_hosts, "DEV and production Worker hosts must be distinct")
            configured_hosts.add(host)
            if host.endswith(".workers.dev"):
                require(name == "dev" and entry["workers_dev"] is True and entry["routes"] == [], "Worker workers.dev is DEV-only")
            else:
                require(entry["workers_dev"] is False, "Invalid exercise-media configuration")
                require(entry["routes"] == [{"pattern": host, "custom_domain": True}], "Worker route and EXPECTED_HOST must agree")
        bucket_policy = policy["dev" if name == "dev" else "productionTemplateOnly"]
        require(bucket_policy["bucket"] == bucket, "Invalid exercise-media configuration")
        require(bucket_policy["publicDevelopmentUrlEnabled"] is False, "Raw bucket r2.dev must remain disabled")
        require(bucket_policy["directBucketCustomDomains"] == [], "Invalid exercise-media configuration")
        require(bucket_policy["corsRules"] == [], "Clients upload to API, no direct R2 PUT CORS")
        require(bucket_policy["objectDeletionLifecycleRules"] == [], "No TTL may delete published/retained bytes")
    if public_base_url is not None:
        origin = urlparse(public_base_url)
        host = config["env"][environment]["vars"]["EXPECTED_HOST"]
        require(host != PLACEHOLDERS[environment], "Verify the host before comparing API settings")
        require(origin.scheme == "https" and origin.netloc == host and origin.path in ("", "/"), "Invalid exercise-media configuration")
        require(not origin.query and not origin.fragment, "API PublicBaseUrl must be the same Worker HTTPS origin")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--allow-example-hosts", action="store_true", help="Validate fail-closed templates only")
    parser.add_argument("--env", choices=BUCKETS, default="dev")
    parser.add_argument("--public-base-url", help="Nonsecret configured API Worker origin")
    args = parser.parse_args()
    validate(allow_examples=args.allow_example_hosts, environment=args.env, public_base_url=args.public_base_url)
    print("Local structure/isolation passed; real bucket, token, privacy, DNS and deployment remain unverified.")
