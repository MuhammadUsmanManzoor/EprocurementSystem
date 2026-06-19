FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY shared/ shared/
COPY backend/EProcurement.ProcurementService/EProcurement.ProcurementService.csproj backend/EProcurement.ProcurementService/
RUN dotnet restore backend/EProcurement.ProcurementService/EProcurement.ProcurementService.csproj
COPY backend/EProcurement.ProcurementService/ backend/EProcurement.ProcurementService/
RUN dotnet publish backend/EProcurement.ProcurementService/EProcurement.ProcurementService.csproj -c Release -o /app/publish
FROM mcr.microsoft.com/dotnet/aspnet:7.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "EProcurement.ProcurementService.dll"]
