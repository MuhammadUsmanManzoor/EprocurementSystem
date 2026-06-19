FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY backend/EProcurement.ApiGateway/EProcurement.ApiGateway.csproj backend/EProcurement.ApiGateway/
RUN dotnet restore backend/EProcurement.ApiGateway/EProcurement.ApiGateway.csproj
COPY backend/EProcurement.ApiGateway/ backend/EProcurement.ApiGateway/
RUN dotnet publish backend/EProcurement.ApiGateway/EProcurement.ApiGateway.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:7.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "EProcurement.ApiGateway.dll"]
