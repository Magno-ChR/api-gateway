# Fase base: imagen runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 5001
USER $APP_UID

# Fase build: compilar la aplicación
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copiar solo el .csproj y restaurar (mejor uso de capas y caché)
COPY ["api-gateway-dp.csproj", "."]
RUN dotnet restore "api-gateway-dp.csproj"

# Copiar el resto del código y compilar
COPY . .
RUN dotnet build "api-gateway-dp.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Fase publish: publicar para producción
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "api-gateway-dp.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Fase final: imagen de producción
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "api-gateway-dp.dll"]
