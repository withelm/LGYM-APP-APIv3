"""Validate one operator-supplied Worker configuration; no Cloudflare access."""

import argparse
import re
import tomllib
from pathlib import Path
from urllib.parse import urlparse

ROOT = Path(__file__).parent
PLACEHOLDERS = {"name": "YOUR_WORKER_NAME", "host": "media.example.invalid", "bucket": "YOUR_EXERCISE_MEDIA_BUCKET"}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def valid_bucket(value):
    return isinstance(value, str) and re.fullmatch(r"[a-z0-9][a-z0-9-]{1,61}[a-z0-9]", value) is not None


def validate(config=None, allow_placeholders=False, environment=None, public_base_url=None, expected_bucket=None, report_bucket=None):
    config = config if config is not None else tomllib.loads((ROOT / "wrangler.toml").read_text(encoding="utf-8"))
    if environment is not None:
        require(environment in config.get("env", {}), "Requested Wrangler environment is missing")
        selected = config["env"][environment]
    else:
        selected = config
    require(selected.get("main", config.get("main")) == "worker.mjs", "Use the reviewed Worker entrypoint")
    require(selected.get("preview_urls", config.get("preview_urls")) is False, "Preview URLs must stay disabled")
    name = selected.get("name", config.get("name"))
    require(isinstance(name, str) and name.strip(), "Worker name is required")
    bindings = selected.get("r2_buckets", [])
    require(len(bindings) == 1 and bindings[0].get("binding") == "EXERCISE_MEDIA", "Exactly one exercise-only binding is required")
    binding = bindings[0]
    require("preview_bucket_name" not in binding, "Use the selected backend's single bucket")
    bucket = binding.get("bucket_name")
    host = selected.get("vars", {}).get("EXPECTED_HOST")
    require(isinstance(host, str) and host, "EXPECTED_HOST is required")
    workers_dev = selected.get("workers_dev", config.get("workers_dev"))
    routes = selected.get("routes", config.get("routes", []))
    incomplete = name == PLACEHOLDERS["name"] or bucket == PLACEHOLDERS["bucket"] or host == PLACEHOLDERS["host"]
    require(valid_bucket(bucket) or allow_placeholders and bucket == PLACEHOLDERS["bucket"], "Bucket must have 3-63 lowercase alphanumeric/hyphen characters and alphanumeric ends")
    require(not incomplete or allow_placeholders, "Replace example values with operator configuration")
    if incomplete:
        require(workers_dev is False and routes == [], "Incomplete examples must expose no public route")
    else:
        require(re.fullmatch(r"(?=.{1,253}$)(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}", host), "Exact lower-case DNS hostname is required")
        require(host not in ("r2.dev", "r2.cloudflarestorage.com") and not host.endswith((".r2.dev", ".r2.cloudflarestorage.com", ".invalid")), "Do not expose a raw R2 or placeholder origin")
        if host.endswith(".workers.dev"):
            require(workers_dev is True and routes == [], "Use the assigned Worker workers.dev route consistently")
        else:
            require(workers_dev is False and routes == [{"pattern": host, "custom_domain": True}], "Custom Worker route must match EXPECTED_HOST")
    if expected_bucket is not None:
        require(valid_bucket(expected_bucket) and expected_bucket == bucket, "Worker bucket must match backend ExerciseMediaStorage:Bucket")
    if report_bucket is not None:
        require(bucket != report_bucket, "Private report-photo storage must use a separate bucket")
    if public_base_url is not None:
        origin = urlparse(public_base_url)
        require(not incomplete, "Complete Worker configuration before comparing the API origin")
        require(origin.scheme == "https" and origin.hostname == host and origin.port in (None, 443), "API PublicBaseUrl must use the same Worker HTTPS host")
        require(origin.username is None and origin.password is None and origin.path in ("", "/") and not origin.query and not origin.fragment, "API PublicBaseUrl must be a credential-free origin")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", type=Path, default=ROOT / "wrangler.toml")
    parser.add_argument("--env", help="Optional existing Wrangler environment, with its own vars and binding")
    parser.add_argument("--allow-placeholders", action="store_true", help="Lint the fail-closed example only")
    parser.add_argument("--public-base-url", help="Nonsecret configured API public origin")
    parser.add_argument("--bucket", help="Nonsecret configured API bucket")
    parser.add_argument("--report-bucket", help="Optional existing private report bucket for isolation comparison")
    args = parser.parse_args()
    config = tomllib.loads(args.config.read_text(encoding="utf-8"))
    validate(config, args.allow_placeholders, args.env, args.public_base_url, args.bucket, args.report_bucket)
    print("Local configuration passed. Live bucket privacy, token scope, ownership and routing are not verified.")
