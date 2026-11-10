# Image runtime pour l'egg "BONELAB Fusion Dedicated"
# Build : docker build -t fusion-dedicated-yolk:latest .
FROM ubuntu:24.04

ENV DEBIAN_FRONTEND=noninteractive \
    DOTNET_ROOT=/usr/share/dotnet \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1

RUN dpkg --add-architecture i386 \
 && apt-get update \
 && apt-get install -y --no-install-recommends software-properties-common ca-certificates curl \
 && add-apt-repository -y multiverse \
 && apt-get update \
 && echo "steam steam/question select I AGREE" | debconf-set-selections \
 && echo "steam steam/license note ''" | debconf-set-selections \
 && apt-get install -y --no-install-recommends \
        xvfb x11vnc openbox xauth dbus-x11 procps iproute2 locales \
        steam-installer libgl1 libgl1-mesa-dri libgl1:i386 libgl1-mesa-dri:i386 \
 && rm -rf /var/lib/apt/lists/*

# .NET 9 runtime
RUN curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
 && bash /tmp/dotnet-install.sh --channel 9.0 --runtime dotnet --install-dir /usr/share/dotnet \
 && ln -s /usr/share/dotnet/dotnet /usr/local/bin/dotnet \
 && rm /tmp/dotnet-install.sh

# Utilisateur attendu par Pterodactyl
RUN userdel -r ubuntu 2>/dev/null || true \
 && useradd -m -d /home/container -s /bin/bash container

COPY run.sh /opt/fusion-run.sh
COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /opt/fusion-run.sh /entrypoint.sh

USER container
ENV USER=container HOME=/home/container PATH=/usr/games:$PATH
WORKDIR /home/container

CMD ["/bin/bash", "/entrypoint.sh"]
