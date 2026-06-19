FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY shared/ shared/
COPY backend/EProcurement.VendorService/EProcurement.VendorService.csproj backend/EProcurement.VendorService/
RUN dotnet restore backend/EProcurement.VendorService/EProcurement.VendorService.csproj
COPY backend/EProcurement.VendorService/ backend/EProcurement.VendorService/
RUN dotnet publish backend/EProcurement.VendorService/EProcurement.VendorService.csproj -c Release -o /app/publish
FROM mcr.microsoft.com/dotnet/aspnet:7.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "EProcurement.VendorService.dll"]
