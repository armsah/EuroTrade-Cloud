FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/EuroTrade.Api/EuroTrade.Api.csproj", "src/EuroTrade.Api/"]
COPY ["src/EuroTrade.Application/EuroTrade.Application.csproj", "src/EuroTrade.Application/"]
COPY ["src/EuroTrade.Domain/EuroTrade.Domain.csproj", "src/EuroTrade.Domain/"]
COPY ["src/EuroTrade.Infrastructure/EuroTrade.Infrastructure.csproj", "src/EuroTrade.Infrastructure/"]

RUN dotnet restore "src/EuroTrade.Api/EuroTrade.Api.csproj"

COPY . .

RUN dotnet publish "src/EuroTrade.Api/EuroTrade.Api.csproj" \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "EuroTrade.Api.dll"]