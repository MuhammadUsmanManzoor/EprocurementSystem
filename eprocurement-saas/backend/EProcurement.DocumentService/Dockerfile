FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY shared/ shared/
COPY backend/EProcurement.DocumentService/EProcurement.DocumentService.csproj backend/EProcurement.DocumentService/
RUN dotnet restore backend/EProcurement.DocumentService/EProcurement.DocumentService.csproj
COPY backend/EProcurement.DocumentService/ backend/EProcurement.DocumentService/
RUN dotnet publish backend/EProcurement.DocumentService/EProcurement.DocumentService.csproj -c Release -o /app/publish
FROM mcr.microsoft.com/dotnet/aspnet:7.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "EProcurement.DocumentService.dll"]
