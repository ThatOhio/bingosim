FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["BingoSim.Worker/BingoSim.Worker.csproj", "BingoSim.Worker/"]
RUN dotnet restore "BingoSim.Worker/BingoSim.Worker.csproj"
COPY . .
WORKDIR "/src/BingoSim.Worker"
RUN dotnet build "./BingoSim.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./BingoSim.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BingoSim.Worker.dll"]
