FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["FaissalBot.csproj", "."]
RUN dotnet restore "FaissalBot.csproj"

COPY . .
RUN dotnet build "FaissalBot.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "FaissalBot.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FaissalBot.dll"]

