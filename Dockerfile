# Use uma imagem base compatível com 32-bit libs
FROM debian:bullseye

ENV DEBIAN_FRONTEND=noninteractive

USER root

RUN apt-get update --fix-missing \
  && apt-get install -y --no-install-recommends \
  sudo \
  dnsutils \
  curl \
  git-all \
  ca-certificates=20210119 \
  lib32z1=1:1.2.11.dfsg-2+deb11u2 \
  wget=1.21-1+deb11u1 \
  locales \
  lib32gcc-s1 \
  lib32stdc++6 \
  screen \
  tar \
  bash \
  && sed -i -e 's/# en_US.UTF-8 UTF-8/en_US.UTF-8 UTF-8/' /etc/locale.gen \
  && dpkg-reconfigure --frontend=noninteractive locales \
  && rm -rf /var/lib/apt/lists/*


WORKDIR /app

COPY components /app/components
COPY scripts /app/scripts
#COPY steamcmd /app/steamcmd
COPY .env /app/.env
COPY .steam /app/.steam

COPY start.sh /app/start.sh
RUN chmod +x /app/start.sh
RUN mkdir -p /root/.steam && \
    cp -r /app/.steam /root/.steam


CMD ["/app/start.sh"]
