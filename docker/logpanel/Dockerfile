# syntax=docker/dockerfile:1
#
# lgym-logpanel - all-in-one log viewer image.
#
# Bundles inside a SINGLE container:
#   * nginx  -> OIDC-first auth gate with explicit break-glass basic auth
#              on /admin-login, plus rate limiting and TLS
#   * Elasticsearch 8.17.0 (single-node, security ON with TLS) -> log store
#   * Kibana 8.17.0                                         -> log viewer
# All three processes are supervised by supervisord.
#
# This replaces the separate docker-compose.logging.yml + nginx gate with one
# deployable image. ES/Kibana ports (9200/5601) are NOT published to the host;
# only 8443 (HTTPS) and 8080 (HTTP->HTTPS redirect) are exposed. nginx is the
# only externally reachable entrypoint; the primary browser path is Kibana's
# Google OIDC login and the proxy basic-auth route is reserved for break-glass.
#
# ---------------------------------------------------------------------------
# Runtime volume / env overrides:
#
#   # Persist Elasticsearch data across container restarts:
#   docker run -d --name lgym-logpanel \
#     -p 8443:8443 -p 8080:8080 \
#     -v logpanel-data:/usr/share/elasticsearch/data \
#     -v /host/logpanel-certs/http:/opt/elasticsearch/config/certs/http:ro \
#     -v /host/logpanel-certs/transport:/opt/elasticsearch/config/certs/transport:ro \
#     -v /host/logpanel-certs/http:/opt/kibana/config/certs/elasticsearch:ro \
#     -e ES_JAVA_OPTS="-Xms1g -Xmx1g" \
#     -e LOGPANEL_USER=admin \
#     -e LOGPANEL_PASSWORD='<strong-password>' \
#     lgym-logpanel:latest
#
#   ES_JAVA_OPTS        : JVM heap for Elasticsearch (default -Xms1g -Xmx1g).
#   LOGPANEL_USER       : break-glass basic-auth username (default admin).
#   LOGPANEL_PASSWORD   : break-glass basic-auth password (required; no image default).
#
#   The Elasticsearch HTTP and transport certificates are runtime-mounted only.
#   Do not bake certificate private keys or OIDC client secrets into the image.
#
#   HOST PREREQUISITE: Elasticsearch needs vm.max_map_count >= 262144.
#   On Linux hosts set it once: sudo sysctl -w vm.max_map_count=262144
#   (or run the container with --sysctl vm.max_map_count=262144).
#   Docker Desktop (Windows/macOS) sets this automatically.
# ---------------------------------------------------------------------------

FROM ubuntu:22.04

ENV DEBIAN_FRONTEND=noninteractive \
    ES_VERSION=8.17.0 \
    KIBANA_VERSION=8.17.0 \
    ES_JAVA_OPTS="-Xms1g -Xmx1g" \
    LOGPANEL_USER=admin

# --- Base system packages ---------------------------------------------------
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        apache2-utils \
        ca-certificates \
        curl \
        nginx \
        openjdk-17-jre-headless \
        openssl \
        supervisor \
        tzdata \
    && rm -rf /var/lib/apt/lists/*

# --- Install Elasticsearch from official tarball ----------------------------
ADD https://artifacts.elastic.co/downloads/elasticsearch/elasticsearch-${ES_VERSION}-linux-x86_64.tar.gz /tmp/elasticsearch.tar.gz
ADD https://artifacts.elastic.co/downloads/kibana/kibana-${KIBANA_VERSION}-linux-x86_64.tar.gz /tmp/kibana.tar.gz

RUN tar -xzf /tmp/elasticsearch.tar.gz -C /opt \
    && mv "/opt/elasticsearch-${ES_VERSION}" /opt/elasticsearch \
    && tar -xzf /tmp/kibana.tar.gz -C /opt \
    && mv "/opt/kibana-${KIBANA_VERSION}" /opt/kibana \
    && rm -f /tmp/elasticsearch.tar.gz /tmp/kibana.tar.gz \
    && useradd -r -s /usr/sbin/nologin elasticsearch \
    && useradd -r -s /usr/sbin/nologin kibana \
    && mkdir -p /usr/share/elasticsearch/data \
    && chown -R elasticsearch:elasticsearch /opt/elasticsearch /usr/share/elasticsearch \
    && chown -R kibana:kibana /opt/kibana

# --- Elasticsearch + Kibana configuration -----------------------------------
COPY docker/logpanel/elasticsearch.yml /opt/elasticsearch/config/elasticsearch.yml
COPY docker/logpanel/kibana.yml /opt/kibana/config/kibana.yml

# --- Supervisor + nginx config ----------------------------------------------
# NOTE: build context is the repo root (see .github/workflows/logpanel-image.yml),
# so COPY sources are relative to the repo root, i.e. docker/logpanel/...
COPY docker/logpanel/supervisord.conf /etc/supervisor/conf.d/supervisord.conf
COPY docker/logpanel/nginx.conf /etc/nginx/nginx.conf
RUN mkdir -p /var/log/supervisor /var/lib/nginx /var/lib/nginx/body \
    && chown -R www-data:www-data /var/lib/nginx \
    && rm -f /etc/nginx/sites-enabled/default

# --- Entrypoint -------------------------------------------------------------
COPY docker/logpanel/entrypoint.sh /usr/local/bin/entrypoint.sh
RUN chmod +x /usr/local/bin/entrypoint.sh

EXPOSE 8443 8080

ENTRYPOINT ["/usr/local/bin/entrypoint.sh"]
