FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY shared/ shared/
COPY backend/EProcurement.NotificationService/EProcurement.NotificationService.csproj backend/EProcurement.NotificationService/
RUN dotnet restore backend/EProcurement.NotificationService/EProcurement.NotificationService.csproj
COPY backend/EProcurement.NotificationService/ backend/EProcurement.NotificationService/
RUN dotnet publish backend/EProcurement.NotificationService/EProcurement.NotificationService.csproj -c Release -o /app/publish
FROM mcr.microsoft.com/dotnet/aspnet:7.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "EProcurement.NotificationService.dll"]
