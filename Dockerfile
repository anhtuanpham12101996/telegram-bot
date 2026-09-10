# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY TelegramRelay.csproj ./
RUN dotnet restore TelegramRelay.csproj

COPY . ./
RUN dotnet publish TelegramRelay.csproj -c Release -o /app/out --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/out .

# Render assigns PORT; default 8080 for local `docker run`
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
ENV PORT=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "TelegramRelay.dll"]
