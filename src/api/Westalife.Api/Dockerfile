# ============ BUILD ============
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore katmani (csproj degismedikce cache kullanilir)
# ONEMLI: klasor yapiasi yereldeki ile birebir ayni olmali (project reference goreceli yollar icin)
COPY ["src/domain/Westalife.Domain/Westalife.Domain.csproj", "src/domain/Westalife.Domain/"]
COPY ["src/application/Westalife.Application/Westalife.Application.csproj", "src/application/Westalife.Application/"]
COPY ["src/infrastructure/Westalife.Infrastructure/Westalife.Infrastructure.csproj", "src/infrastructure/Westalife.Infrastructure/"]
COPY ["src/api/Westalife.Api/Westalife.Api.csproj", "src/api/Westalife.Api/"]
RUN dotnet restore "src/api/Westalife.Api/Westalife.Api.csproj"

COPY ./ ./
RUN dotnet publish "src/api/Westalife.Api/Westalife.Api.csproj" -c Release -o /app/publish --nologo

# Migration bundle: prod'da veritabani guncellemesi icin (dotnet SDK gerektirmeden calisir)
RUN dotnet tool install --global dotnet-ef
ENV PATH="${PATH}:/root/.dotnet/tools"
RUN dotnet ef migrations bundle \
    --project src/infrastructure/Westalife.Infrastructure/Westalife.Infrastructure.csproj \
    --startup-project src/api/Westalife.Api/Westalife.Api.csproj \
    -o /app/publish/migrate

# ============ RUNTIME ============
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# HEALTHCHECK icin wget kurulumu (root iken yapilir, sonrasinda non-root'a gecilir)
RUN apt-get update \
    && apt-get install -y --no-install-recommends wget \
    && rm -rf /var/lib/apt/lists/*

# Publish ciktilari + migration bundle
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD wget -qO- http://localhost:8080/health || exit 1

USER app

ENTRYPOINT ["dotnet", "Westalife.Api.dll"]
