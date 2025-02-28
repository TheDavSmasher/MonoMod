FROM ubuntu:24.04

# First, packages
RUN apt-get update && apt-get upgrade -y \
    # for add-apt-repository
    && apt-get install --no-install-recommends -y apt-transport-https software-properties-common \
    && add-apt-repository ppa:dotnet/backports \
    && apt-get install --no-install-recommends -y \
            mono-runtime git git-lfs curl wget bash \
    && source /etc/os-release \
    && wget -q https://packages.microsoft.com/config/ubuntu/$VERSION_ID/packages-microsoft-prod.deb \
    && dpkg -i packages-microsoft-prod.deb \
    && rm packages-microsoft-prod.deb \
    && curl -fsSL https://deb.nodesource.com/setup_23.x | bash \
    # pretty sure we need nodejs for stock github actions
    && apt-get install --no-install-recommends -y \
            nodejs \
            dotnet-runtime-9.0 \
            powershell \
    && apt-get remove -y apt-transport-https software-properties-common \
    && apt-get autoremove -y \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Then, user
RUN useradd -rm -d /home/runner -s /bin/bash -g root -G sudo -u 1001 runner
USER runner
WORKDIR /home/runner
