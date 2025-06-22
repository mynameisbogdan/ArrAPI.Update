FROM mcr.microsoft.com/dotnet/sdk:8.0.411-alpine3.22 AS build
ARG TARGETARCH
WORKDIR /source
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

COPY src ./src
RUN dotnet publish -c Release --no-self-contained -p:PublishDir=/source/build -a $TARGETARCH src/*.sln

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0.17-alpine3.22
EXPOSE 5000
WORKDIR /app
COPY --from=build /source/build/. ./

ENTRYPOINT ["dotnet", "ServarrAPI.dll"]
