# syntax=docker/dockerfile:1

ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src

COPY src/Hagale.Domain/Hagale.Domain.csproj src/Hagale.Domain/
COPY src/Hagale.Application/Hagale.Application.csproj src/Hagale.Application/
COPY src/Hagale.Infrastructure/Hagale.Infrastructure.csproj src/Hagale.Infrastructure/
COPY src/Hagale.Api/Hagale.Api.csproj src/Hagale.Api/
RUN dotnet restore src/Hagale.Api/Hagale.Api.csproj

COPY . .
RUN dotnet publish src/Hagale.Api/Hagale.Api.csproj \
    --configuration Release \
    --output /app/publish \
    /p:UseAppHost=false

FROM build AS migrations
RUN dotnet tool restore
ENTRYPOINT ["dotnet", "tool", "run", "dotnet-ef", "database", "update", "--project", "src/Hagale.Infrastructure/Hagale.Infrastructure.csproj", "--startup-project", "src/Hagale.Api/Hagale.Api.csproj"]

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .
RUN mkdir -p /app/App_Data/private-documents

ENTRYPOINT ["dotnet", "Hagale.Api.dll"]
