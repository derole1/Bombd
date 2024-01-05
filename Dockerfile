# Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /build

COPY ["Bombd/Bombd.csproj", "Bombd/"]
RUN dotnet restore Bombd --use-current-runtime
COPY . .

RUN dotnet publish Bombd -c Release -o /build/publish/ --no-restore --no-self-contained

FROM mcr.microsoft.com/dotnet/runtime:8.0.0-bookworm-slim AS runtime

# Setup workspace dependencies
RUN set -eux
RUN apt update
RUN apt install -y gosu wget build-essential zlib1g-dev
RUN rm -rf /var/lib/apt/lists/*
RUN gosu nobody true


# Download OpenSSL 
ENV OPENSSL_VERSION="3.2.0"
RUN wget --quiet --no-check-certificate "https://www.openssl.org/source/openssl-${OPENSSL_VERSION}.tar.gz"
RUN tar -xf openssl-${OPENSSL_VERSION}.tar.gz
RUN rm openssl-${OPENSSL_VERSION}.tar.gz
WORKDIR /openssl-${OPENSSL_VERSION}
# Build and configure OpenSSL for SSLv3
RUN ./Configure enable-weak-ssl-ciphers enable-ssl3 enable-ssl3-method --libdir=lib
RUN make
RUN make install
# Destroy build files
WORKDIR /
RUN rm -rf /openssl-${OPENSSL_VERSION}

# Add non-root user
RUN groupadd -g 1001 bombd
RUN useradd -m --home /bombd -u 1001 -g bombd bombd
RUN mkdir -p /bombd/app

# Finalize distribution
COPY --from=build /build/publish /bombd/app
COPY --from=build /build/docker-entrypoint.sh /bombd
COPY --from=build /build/docker-openssl.cnf /bombd/ssl.cnf

RUN chown -R bombd:bombd /bombd
RUN chmod +x /bombd/docker-entrypoint.sh

ENTRYPOINT ["/bombd/docker-entrypoint.sh"]