FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY shared/ shared/
COPY backend/EProcurement.TenantService/EProcurement.TenantService.csproj backend/EProcurement.TenantService/
RUN dotnet restore backend/EProcurement.TenantService/EProcurement.TenantService.csproj
COPY backend/EProcurement.TenantService/ backend/EProcurement.TenantService/
RUN dotnet publish backend/EProcurement.TenantService/EProcurement.TenantService.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:7.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "EProcurement.TenantService.dll"]
