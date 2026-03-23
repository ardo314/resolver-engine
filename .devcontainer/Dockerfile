FROM mcr.microsoft.com/dotnet/sdk:9.0-bookworm-slim

RUN apt-get update && apt-get install -y \
    git \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Install Docker CLI (uses host Docker via socket mount)
RUN apt-get update && apt-get install -y ca-certificates gnupg \
    && install -m 0755 -d /etc/apt/keyrings \
    && curl -fsSL https://download.docker.com/linux/debian/gpg | gpg --dearmor -o /etc/apt/keyrings/docker.gpg \
    && chmod a+r /etc/apt/keyrings/docker.gpg \
    && echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/debian bookworm stable" \
    > /etc/apt/sources.list.d/docker.list \
    && apt-get update \
    && apt-get install -y docker-ce-cli \
    && rm -rf /var/lib/apt/lists/*

# Install nats-server
ARG NATS_VERSION=v2.12.4
RUN curl -sL "https://github.com/nats-io/nats-server/releases/download/${NATS_VERSION}/nats-server-${NATS_VERSION}-linux-amd64.tar.gz" \
    | tar xz -C /tmp \
    && cp /tmp/nats-server-${NATS_VERSION}-linux-amd64/nats-server /usr/local/bin/ \
    && rm -rf /tmp/nats-server-*
