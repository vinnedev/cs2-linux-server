FROM debian:bullseye

ENV DEBIAN_FRONTEND=noninteractive
ENV CS2_IN_DOCKER=1
ENV PYTHONUNBUFFERED=1
ENV FORCE_COLOR=1

USER root

RUN apt-get update --fix-missing \
  && apt-get install -y --no-install-recommends \
  sudo \
  dnsutils \
  curl \
  git-all \
  ca-certificates \
  lib32z1 \
  wget \
  locales \
  lib32gcc-s1 \
  lib32stdc++6 \
  screen \
  tar \
  bash \
  python3 \
  python3-minimal \
  && sed -i -e 's/# en_US.UTF-8 UTF-8/en_US.UTF-8 UTF-8/' /etc/locale.gen \
  && dpkg-reconfigure --frontend=noninteractive locales \
  && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY components /app/components
COPY scripts /app/scripts
COPY .env /app/.env
COPY start.py /app/start.py
COPY install.py /app/install.py

RUN chmod +x /app/start.py /app/install.py /app/scripts/*.py \
 && mkdir -p /root/.steam/sdk32 /root/.steam/sdk64

CMD ["python3", "/app/start.py"]
