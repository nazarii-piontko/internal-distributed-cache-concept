FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 5000
EXPOSE 5100

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["IDCC.TestService/IDCC.TestService.csproj", "IDCC.TestService/"]
RUN dotnet restore "IDCC.TestService/IDCC.TestService.csproj"
COPY . .
WORKDIR "/src/IDCC.TestService"
RUN dotnet build "IDCC.TestService.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "IDCC.TestService.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "IDCC.TestService.dll"]
