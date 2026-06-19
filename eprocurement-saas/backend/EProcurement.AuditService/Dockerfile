FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY shared/ shared/
COPY backend/EProcurement.AuditService/EProcurement.AuditService.csproj backend/EProcurement.AuditService/
RUN dotnet restore backend/EProcurement.AuditService/EProcurement.AuditService.csproj
COPY backend/EProcurement.AuditService/ backend/EProcurement.AuditService/
RUN dotnet publish backend/EProcurement.AuditService/EProcurement.AuditService.csproj -c Release -o /app/publish
FROM mcr.microsoft.com/dotnet/aspnet:7.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "EProcurement.AuditService.dll"]
