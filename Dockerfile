# ---------------------------------------------------------------------------------------------
# Sabarna.Web in a container.
#
# Build and run locally:
#   docker build -t sabarna .
#   docker run -p 8080:8080 -v sabarna-data:/data sabarna
#
# The volume (/data) is the whole point: SQLite is a file, and a container's own filesystem is
# thrown away on every redeploy. Mount a volume there or the shop forgets its orders.
# ---------------------------------------------------------------------------------------------

# ---- 1. build --------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first, on its own layer, so a code change does not re-download the NuGet packages.
COPY Sabarna.Web.csproj ./
RUN dotnet restore Sabarna.Web.csproj

COPY . .
RUN dotnet publish Sabarna.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---- 2. runtime ------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# /data is the one directory that must survive a redeploy (mount a volume here).
RUN mkdir -p /data && chown app:app /data

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:8080 \
    ConnectionStrings__Default="Data Source=/data/app.db" \
    DOTNET_RUNNING_IN_CONTAINER=true

COPY --from=build /app/publish .

USER app
EXPOSE 8080

ENTRYPOINT ["dotnet", "Sabarna.Web.dll"]
