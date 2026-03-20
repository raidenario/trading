FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY apps/query-api/src/Exchange.Query.Api/Exchange.Query.Api.csproj apps/query-api/src/Exchange.Query.Api/
COPY libs/contracts/dotnet/Exchange.Platform.Contracts/Exchange.Platform.Contracts.csproj libs/contracts/dotnet/Exchange.Platform.Contracts/

RUN dotnet restore apps/query-api/src/Exchange.Query.Api/Exchange.Query.Api.csproj

COPY apps/query-api/src/Exchange.Query.Api/ apps/query-api/src/Exchange.Query.Api/
COPY libs/contracts/dotnet/Exchange.Platform.Contracts/ libs/contracts/dotnet/Exchange.Platform.Contracts/

RUN dotnet publish apps/query-api/src/Exchange.Query.Api/Exchange.Query.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Exchange.Query.Api.dll"]
