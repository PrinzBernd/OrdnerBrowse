# syntax=docker/dockerfile:1

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

COPY ["WebUI.Web/WebUI.Web.csproj", "WebUI.Web/"]
COPY ["WebUI.Infrastructure/WebUI.Infrastructure.csproj", "WebUI.Infrastructure/"]
RUN dotnet restore "WebUI.Web/WebUI.Web.csproj" -a "$TARGETARCH"

COPY . .
WORKDIR "/src/WebUI.Web"
RUN dotnet publish "WebUI.Web.csproj" \
    -c Release \
    -a "$TARGETARCH" \
    -o /app/publish \
    /p:UseAppHost=false

# Öffentliche Lizenz- und Drittanbieterhinweise werden mitgeführt, sobald sie im Quellstand vorhanden sind.
RUN set -eu; \
    if [ -f /src/LICENSE ]; then cp /src/LICENSE /app/publish/LICENSE; fi; \
    if [ -f /src/THIRD_PARTY_NOTICES.md ]; then cp /src/THIRD_PARTY_NOTICES.md /app/publish/THIRD_PARTY_NOTICES.md; fi; \
    if [ -f /src/THIRD_PARTY_NOTICES.en.md ]; then cp /src/THIRD_PARTY_NOTICES.en.md /app/publish/THIRD_PARTY_NOTICES.en.md; fi; \
    if [ -d /src/LICENSES ]; then cp -R /src/LICENSES /app/publish/LICENSES; fi

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0 \
    HOME=/tmp

EXPOSE 8080
USER 1654
ENTRYPOINT ["dotnet", "WebUI.Web.dll"]
