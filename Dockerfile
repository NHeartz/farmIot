FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["src/MyAspNetCoreHiveMQ/MyAspNetCoreHiveMQ.csproj", "src/MyAspNetCoreHiveMQ/"]
RUN dotnet restore "src/MyAspNetCoreHiveMQ/MyAspNetCoreHiveMQ.csproj"
COPY . .
WORKDIR "/src/src/MyAspNetCoreHiveMQ"
RUN dotnet build "MyAspNetCoreHiveMQ.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MyAspNetCoreHiveMQ.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MyAspNetCoreHiveMQ.dll"]