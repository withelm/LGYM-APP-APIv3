# syntax=docker/dockerfile:1
#
# lgym-logpanel - all-in-one log viewer image.
#
# Bundles inside a SINGLE container:
#   * nginx  -> auth gate (basic-auth + rate limiting on login + TLS)
#   * Elasticsearch 8.17.0 (single-node, security OFF)  -> log store
#   * Kibana 8.17.0                                  -> log viewer
# All three processes are supervised by supervisord.
#
# This replaces the separate docker-compose.logging.yml + nginx gate with one
# deployable image. ES/Kibana ports (9200/5601) are NOT published to the host;
# only 8443 (HTTPS) and 8080 (HTTP->HTTPS redirect) are exposed. nginx is the
# only externally reachable entrypoint and it enforces basic-auth + rate limits.
#
# ---------------------------------------------------------------------------
# Runtime volume / env overrides:
#
#   # Persist Elasticsearch data across container restarts:
#   docker run -d --name lgym-logpanel \
#     -p 8443:8443 -p 8080:8080 \
#     -v logpanel-data:/usr/share/elasticsearch/data \
#     -e ES_JAVA_OPTS="-Xms1g -Xmx1g" \
#     -e LOGPANEL_USER=admin \
#     -e LOGPANEL_PASSWORD='<strong-password>' \
#     lgym-logpanel:latest
#
#   ES_JAVA_OPTS        : JVM heap for Elasticsearch (default -Xms1g -Xmx1g).
#   LOGPANEL_USER       : basic-auth username (default admin).
#   LOGPANEL_PASSWORD   : basic-auth password (default admin12345 - INSECURE).
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
    LOGPANEL_USER=admin \
    LOGPANEL_PASSWORD=admin12345

# --- Base system packages ---------------------------------------------------
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        openjdk-17-jre-headless \
        nginx \
        supervisor \
        apache2-utils \
        openssl \
        curl \
        ca-certificates \
        tzdata \
    && rm -rf /var/lib/apt/lists/*

# --- Install Elasticsearch from official tarball ----------------------------
RUN curl -fsSL "https://artifacts.elastic.co/downloads/elasticsearch/elasticsearch-${ES_VERSION}-linux-x86_64.tar.gz" \
        -o /tmp/elasticsearch.tar.gz \
    && tar -xzf /tmp/elasticsearch.tar.gz -C /opt \
    && mv "/opt/elasticsearch-${ES_VERSION}" /opt/elasticsearch \
    && rm -f /tmp/elasticsearch.tar.gz \
    && useradd -r -s /usr/sbin/nologin elasticsearch \
    && mkdir -p /usr/share/elasticsearch/data \
    && chown -R elasticsearch:elasticsearch /opt/elasticsearch /usr/share/elasticsearch

# --- Install Kibana from official tarball -----------------------------------
RUN curl -fsSL "https://artifacts.elastic.co/downloads/kibana/kibana-${KIBANA_VERSION}-linux-x86_64.tar.gz" \
        -o /tmp/kibana.tar.gz \
    && tar -xzf /tmp/kibana.tar.gz -C /opt \
    && mv "/opt/kibana-${KIBANA_VERSION}" /opt/kibana \
    && rm -f /tmp/kibana.tar.gz \
    && useradd -r -s /usr/sbin/nologin kibana \
    && chown -R kibana:kibana /opt/kibana

# --- Elasticsearch configuration --------------------------------------------
# Single-node, security OFF (nginx is the auth gate), memory_lock OFF to avoid
# container cap failures, data on the volume-mounted path.
RUN echo 'discovery.type: single-node' >> /opt/elasticsearch/config/elasticsearch.yml \
    && echo 'xpack.security.enabled: false' >> /opt/elasticsearch/config/elasticsearch.yml \
    && echo 'bootstrap.memory_lock: false' >> /opt/elasticsearch/config/elasticsearch.yml \
    && echo 'http.host: 0.0.0.0' >> /opt/elasticsearch/config/elasticsearch.yml \
    && echo 'path.data: /usr/share/elasticsearch/data' >> /opt/elasticsearch/config/elasticsearch.yml

# --- Kibana configuration ---------------------------------------------------
RUN echo 'server.host: "0.0.0.0"' >> /opt/kibana/config/kibana.yml \
    && echo 'elasticsearch.hosts: ["http://localhost:9200"]' >> /opt/kibana/config/kibana.yml \
    && echo 'xpack.security.enabled: false' >> /opt/kibana/config/kibana.yml \
    && echo 'server.publicBaseUrl: ""' >> /opt/kibana/config/kibana.yml

# --- Supervisor + nginx config ----------------------------------------------
# NOTE: build context is the repo root (see .github/workflows/logpanel-image.yml),
# so COPY sources are relative to the repo root, i.e. docker/logpanel/...
COPY docker/logpanel/supervisord.conf /etc/supervisor/conf.d/supervisord.conf
COPY docker/logpanel/nginx.conf /etc/nginx/nginx.conf
RUN mkdir -p /var/log/supervisor /var/lib/nginx /var/lib/nginx/body \
    && chown -R nginx:nginx /var/lib/nginx \
    && rm -f /etc/nginx/sites-enabled/default

# --- Entrypoint -------------------------------------------------------------
COPY docker/logpanel/entrypoint.sh /usr/local/bin/entrypoint.sh
RUN chmod +x /usr/local/bin/entrypoint.sh

EXPOSE 8443 8080

ENTRYPOINT ["/usr/local/bin/entrypoint.sh"]
