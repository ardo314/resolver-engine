FROM alpine:3.21

ARG VERSION=latest
ENV VERSION=${VERSION}

RUN apk add --no-cache curl jq bash

WORKDIR /app

COPY install-apps.sh ./
RUN chmod +x install-apps.sh

CMD ["./install-apps.sh"]

