FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY apps/gateway-api/src/Exchange.Gateway.Api/Exchange.Gateway.Api.csproj apps/gateway-api/src/Exchange.Gateway.Api/
COPY libs/dotnet/Exchange.Trading.Application/Exchange.Trading.Application.csproj libs/dotnet/Exchange.Trading.Application/
COPY libs/dotnet/Exchange.Trading.Domain/Exchange.Trading.Domain.csproj libs/dotnet/Exchange.Trading.Domain/
COPY libs/dotnet/Exchange.Trading.Infrastructure/Exchange.Trading.Infrastructure.csproj libs/dotnet/Exchange.Trading.Infrastructure/
COPY libs/contracts/dotnet/Exchange.Platform.Contracts/Exchange.Platform.Contracts.csproj libs/contracts/dotnet/Exchange.Platform.Contracts/

RUN dotnet restore apps/gateway-api/src/Exchange.Gateway.Api/Exchange.Gateway.Api.csproj

COPY apps/gateway-api/src/Exchange.Gateway.Api/ apps/gateway-api/src/Exchange.Gateway.Api/
COPY libs/dotnet/Exchange.Trading.Application/ libs/dotnet/Exchange.Trading.Application/
COPY libs/dotnet/Exchange.Trading.Domain/ libs/dotnet/Exchange.Trading.Domain/
COPY libs/dotnet/Exchange.Trading.Infrastructure/ libs/dotnet/Exchange.Trading.Infrastructure/
COPY libs/contracts/dotnet/Exchange.Platform.Contracts/ libs/contracts/dotnet/Exchange.Platform.Contracts/

RUN dotnet publish apps/gateway-api/src/Exchange.Gateway.Api/Exchange.Gateway.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Exchange.Gateway.Api.dll"]
