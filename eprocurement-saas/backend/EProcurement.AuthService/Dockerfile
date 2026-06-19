FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY shared/ shared/
COPY backend/EProcurement.AuthService/EProcurement.AuthService.csproj backend/EProcurement.AuthService/
RUN dotnet restore backend/EProcurement.AuthService/EProcurement.AuthService.csproj
COPY backend/EProcurement.AuthService/ backend/EProcurement.AuthService/
RUN dotnet publish backend/EProcurement.AuthService/EProcurement.AuthService.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:7.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "EProcurement.AuthService.dll"]
