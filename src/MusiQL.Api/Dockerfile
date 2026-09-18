# Build context is the repository root.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/MusiQL.Api/MusiQL.Api.csproj src/MusiQL.Api/
COPY src/MusiQL.Core/MusiQL.Core.csproj src/MusiQL.Core/
COPY src/MusiQL.Data/MusiQL.Data.csproj src/MusiQL.Data/
RUN dotnet restore src/MusiQL.Api/MusiQL.Api.csproj -a $TARGETARCH

COPY src/MusiQL.Api/ src/MusiQL.Api/
COPY src/MusiQL.Core/ src/MusiQL.Core/
COPY src/MusiQL.Data/ src/MusiQL.Data/
RUN dotnet publish src/MusiQL.Api/MusiQL.Api.csproj -c Release -a $TARGETARCH -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "MusiQL.Api.dll"]
