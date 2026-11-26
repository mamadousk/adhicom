# =====================================================
# Adhicom — image multi-étapes (ASP.NET Core 10 + PostgreSQL requis)
# Build :  docker build -f deployment/Dockerfile -t adhicom .
# Run   :  docker run -e ConnectionStrings__DefaultConnection="Host=...;Database=adhicom;Username=adhicom;Password=..." -p 8080:8080 adhicom
# =====================================================

# ---------- Étape build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restauration séparée (cache des couches)
COPY Adhicom.sln ./
COPY src/Adhicom.Domain/Adhicom.Domain.csproj src/Adhicom.Domain/
COPY src/Adhicom.Application/Adhicom.Application.csproj src/Adhicom.Application/
COPY src/Adhicom.Infrastructure/Adhicom.Infrastructure.csproj src/Adhicom.Infrastructure/
COPY src/Adhicom.Web/Adhicom.Web.csproj src/Adhicom.Web/
RUN dotnet restore src/Adhicom.Web/Adhicom.Web.csproj

# Compilation
COPY src/ src/
RUN dotnet build src/Adhicom.Web/Adhicom.Web.csproj -c Release --no-restore

# ---------- Étape publish ----------
FROM build AS publish
RUN dotnet publish src/Adhicom.Web/Adhicom.Web.csproj -c Release -o /app/publish --no-build /p:UseAppHost=false

# ---------- Étape finale ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Sécurité : utilisateur non-root
RUN groupadd --system adhicom && useradd --system --gid adhicom adhicom \
    && mkdir -p /app/wwwroot/uploads && chown -R adhicom:adhicom /app
USER adhicom

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

COPY --from=publish --chown=adhicom:adhicom /app/publish .

EXPOSE 8080
# Sonde de santé : GET /health (utilisable par Nginx, Cloudflare ou le compose)

ENTRYPOINT ["dotnet", "Adhicom.Web.dll"]
