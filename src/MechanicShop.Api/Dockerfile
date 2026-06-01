# ══════════════════════════════════════════════════
# STAGE 1: Build
# ══════════════════════════════════════════════════
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["src/MechanicShop.Api/MechanicShop.Api.csproj", "src/MechanicShop.Api/"]
COPY ["src/MechanicShop.Application/MechanicShop.Application.csproj", "src/MechanicShop.Application/"]
COPY ["src/MechanicShop.Infrastructure/MechanicShop.Infrastructure.csproj", "src/MechanicShop.Infrastructure/"]
COPY ["src/MechanicShop.Domain/MechanicShop.Domain.csproj", "src/MechanicShop.Domain/"]

RUN dotnet restore "src/MechanicShop.Api/MechanicShop.Api.csproj" \
    --runtime linux-x64

COPY . .

WORKDIR "/src/src/MechanicShop.Api"
RUN dotnet build "MechanicShop.Api.csproj" \
    -c $BUILD_CONFIGURATION \
    --runtime linux-x64 \
    --no-restore \
    -o /app/build

# ══════════════════════════════════════════════════
# STAGE 2: Publish
# ══════════════════════════════════════════════════
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "MechanicShop.Api.csproj" \
    -c $BUILD_CONFIGURATION \
    --runtime linux-x64 \
    --self-contained false \
    --no-restore \
    -o /app/publish \
    /p:UseAppHost=false

# ══════════════════════════════════════════════════
# STAGE 3: Final
# ══════════════════════════════════════════════════
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app
COPY --from=publish /app/publish .


USER app

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

ENTRYPOINT ["dotnet", "MechanicShop.Api.dll"]