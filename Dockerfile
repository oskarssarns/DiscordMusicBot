# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY LavaLinkLouieBot.csproj ./
RUN dotnet restore LavaLinkLouieBot.csproj

COPY . ./
RUN dotnet publish LavaLinkLouieBot.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app

ENV DOTNET_EnableDiagnostics=0

COPY --from=build /app/publish ./

USER $APP_UID
ENTRYPOINT ["dotnet", "LavaLinkLouieBot.dll"]
