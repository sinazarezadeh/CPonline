# Small, cheaply self-hostable signaling relay - it only ever carries short JSON control
# messages (room create/resolve/report-address), never game traffic, so this image doesn't
# need to be beefy. A $4-5/mo VPS is plenty for a friend-group's worth of rooms.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/CPonline.Shared/CPonline.Shared.csproj src/CPonline.Shared/
COPY src/CPonline.Matchmaking/CPonline.Matchmaking.csproj src/CPonline.Matchmaking/
RUN dotnet restore src/CPonline.Matchmaking/CPonline.Matchmaking.csproj

COPY src/CPonline.Shared/ src/CPonline.Shared/
COPY src/CPonline.Matchmaking/ src/CPonline.Matchmaking/
RUN dotnet publish src/CPonline.Matchmaking/CPonline.Matchmaking.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "CPonline.Matchmaking.dll"]
