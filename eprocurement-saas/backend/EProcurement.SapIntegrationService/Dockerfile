FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY shared/ shared/
COPY backend/EProcurement.SapIntegrationService/EProcurement.SapIntegrationService.csproj backend/EProcurement.SapIntegrationService/
RUN dotnet restore backend/EProcurement.SapIntegrationService/EProcurement.SapIntegrationService.csproj
COPY backend/EProcurement.SapIntegrationService/ backend/EProcurement.SapIntegrationService/
RUN dotnet publish backend/EProcurement.SapIntegrationService/EProcurement.SapIntegrationService.csproj -c Release -o /app/publish
FROM mcr.microsoft.com/dotnet/aspnet:7.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "EProcurement.SapIntegrationService.dll"]
