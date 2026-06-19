FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY shared/ shared/
COPY backend/EProcurement.BiddingService/EProcurement.BiddingService.csproj backend/EProcurement.BiddingService/
RUN dotnet restore backend/EProcurement.BiddingService/EProcurement.BiddingService.csproj
COPY backend/EProcurement.BiddingService/ backend/EProcurement.BiddingService/
RUN dotnet publish backend/EProcurement.BiddingService/EProcurement.BiddingService.csproj -c Release -o /app/publish
FROM mcr.microsoft.com/dotnet/aspnet:7.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "EProcurement.BiddingService.dll"]
