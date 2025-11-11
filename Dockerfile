# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:9.0.307-alpine3.22 AS build

ARG TARGETARCH
ARG VERSION=0.0.0
ARG BRANCH=unknown

WORKDIR /source
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

COPY src ./src
RUN dotnet publish -c Release \
    --no-self-contained \
    -p:PublishDir=/source/build \
    -p:AssemblyVersion=$VERSION \
    -p:AssemblyConfiguration=$BRANCH \
    -a $TARGETARCH \
    src/*.sln

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0.11-alpine3.22
EXPOSE 5000
WORKDIR /app
COPY --from=build /source/build/. ./

ENTRYPOINT ["dotnet", "ServarrAPI.dll"]
