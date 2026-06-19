FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY shared/ shared/
COPY backend/EProcurement.ContractService/EProcurement.ContractService.csproj backend/EProcurement.ContractService/
RUN dotnet restore backend/EProcurement.ContractService/EProcurement.ContractService.csproj
COPY backend/EProcurement.ContractService/ backend/EProcurement.ContractService/
RUN dotnet publish backend/EProcurement.ContractService/EProcurement.ContractService.csproj -c Release -o /app/publish
FROM mcr.microsoft.com/dotnet/aspnet:7.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "EProcurement.ContractService.dll"]
