FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY shared/ shared/
COPY backend/EProcurement.TenderService/EProcurement.TenderService.csproj backend/EProcurement.TenderService/
RUN dotnet restore backend/EProcurement.TenderService/EProcurement.TenderService.csproj
COPY backend/EProcurement.TenderService/ backend/EProcurement.TenderService/
RUN dotnet publish backend/EProcurement.TenderService/EProcurement.TenderService.csproj -c Release -o /app/publish
FROM mcr.microsoft.com/dotnet/aspnet:7.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "EProcurement.TenderService.dll"]
