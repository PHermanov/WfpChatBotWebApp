FROM mcr.microsoft.com/playwright/dotnet:v1.58.0-noble

WORKDIR /app

# Copy csproj and restore as distinct layers
COPY ["WfpChatBotWebApp/WfpChatBotWebApp.csproj", "WfpChatBotWebApp/"]
RUN dotnet restore "WfpChatBotWebApp/WfpChatBotWebApp.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/app/WfpChatBotWebApp"
RUN dotnet publish "WfpChatBotWebApp.csproj" -c Release -o /app/publish

WORKDIR /app/publish
ENTRYPOINT ["dotnet", "WfpChatBotWebApp.dll"]