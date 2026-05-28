FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["AccessKepler.csproj", "."]
RUN dotnet restore "AccessKepler.csproj"
COPY . .
RUN dotnet build "AccessKepler.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AccessKepler.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Required for camera/HTTPS in PWA
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "AccessKepler.dll"]
