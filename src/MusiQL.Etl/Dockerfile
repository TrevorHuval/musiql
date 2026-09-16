# Build context is the repository root.
# Catalog ETL as a one-shot container:
#   docker run --rm -e MUSIQL_CONNECTION="Host=...;..." ghcr.io/trevorhuval/musiql-etl load --sample
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/MusiQL.Etl/MusiQL.Etl.csproj src/MusiQL.Etl/
COPY src/MusiQL.Core/MusiQL.Core.csproj src/MusiQL.Core/
COPY src/MusiQL.Data/MusiQL.Data.csproj src/MusiQL.Data/
RUN dotnet restore src/MusiQL.Etl/MusiQL.Etl.csproj -a $TARGETARCH

COPY src/MusiQL.Etl/ src/MusiQL.Etl/
COPY src/MusiQL.Core/ src/MusiQL.Core/
COPY src/MusiQL.Data/ src/MusiQL.Data/
RUN dotnet publish src/MusiQL.Etl/MusiQL.Etl.csproj -c Release -a $TARGETARCH -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
# Dump downloads land here when running `download`; mount a volume to keep them.
VOLUME ["/app/.mbdump-cache"]
ENTRYPOINT ["dotnet", "MusiQL.Etl.dll"]
