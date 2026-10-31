FROM node:20-bookworm-slim AS client
WORKDIR /src/client
COPY client/package.json client/package-lock.json ./
RUN npm ci
COPY client/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY server/PriceHunt.Api/PriceHunt.Api.csproj server/PriceHunt.Api/
RUN dotnet restore server/PriceHunt.Api/PriceHunt.Api.csproj
COPY server/PriceHunt.Api/ server/PriceHunt.Api/
RUN dotnet publish server/PriceHunt.Api/PriceHunt.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=client /src/client/dist/pricehunt/browser ./wwwroot
USER root
RUN mkdir -p /app/data && chown app:app /app/data
USER app
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ConnectionStrings__Default="Data Source=/app/data/pricehunt.db"
ENTRYPOINT ["dotnet", "PriceHunt.Api.dll"]
