FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY apps/ledger-service/src/Exchange.Ledger.Api/Exchange.Ledger.Api.csproj apps/ledger-service/src/Exchange.Ledger.Api/
COPY libs/dotnet/Exchange.Ledger.Domain/Exchange.Ledger.Domain.csproj libs/dotnet/Exchange.Ledger.Domain/
COPY libs/contracts/dotnet/Exchange.Platform.Contracts/Exchange.Platform.Contracts.csproj libs/contracts/dotnet/Exchange.Platform.Contracts/

RUN dotnet restore apps/ledger-service/src/Exchange.Ledger.Api/Exchange.Ledger.Api.csproj

COPY apps/ledger-service/src/Exchange.Ledger.Api/ apps/ledger-service/src/Exchange.Ledger.Api/
COPY libs/dotnet/Exchange.Ledger.Domain/ libs/dotnet/Exchange.Ledger.Domain/
COPY libs/contracts/dotnet/Exchange.Platform.Contracts/ libs/contracts/dotnet/Exchange.Platform.Contracts/

RUN dotnet publish apps/ledger-service/src/Exchange.Ledger.Api/Exchange.Ledger.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Exchange.Ledger.Api.dll"]
