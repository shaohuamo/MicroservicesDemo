FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/backend/Services/Notifications/NotificationsMicroservice.DlqReplay/NotificationsMicroservice.DlqReplay.csproj", "src/backend/Services/Notifications/NotificationsMicroservice.DlqReplay/"]
RUN dotnet restore "src/backend/Services/Notifications/NotificationsMicroservice.DlqReplay/NotificationsMicroservice.DlqReplay.csproj"
COPY . .
RUN dotnet publish "src/backend/Services/Notifications/NotificationsMicroservice.DlqReplay/NotificationsMicroservice.DlqReplay.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:9.0
USER $APP_UID
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NotificationsMicroservice.DlqReplay.dll"]
