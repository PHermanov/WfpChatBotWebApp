FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Install Playwright dependencies (for browsers)
RUN apt-get update && \
    apt-get install -y wget gnupg ca-certificates && \
    apt-get install -y --no-install-recommends \
        libnss3 libatk-bridge2.0-0 libgtk-3-0 libxss1 libasound2 libgbm1 libxshmfence1 \
        libdrm2 libxcomposite1 libxdamage1 libxrandr2 libxinerama1 libpango-1.0-0 \
        libpangocairo-1.0-0 libcups2 libx11-xcb1 && \
    rm -rf /var/lib/apt/lists/*

# Copy csproj and restore as distinct layers
COPY ["WfpChatBotWebApp/WfpChatBotWebApp.csproj", "WfpChatBotWebApp/"]
RUN dotnet restore "WfpChatBotWebApp/WfpChatBotWebApp.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/WfpChatBotWebApp"
RUN dotnet publish "WfpChatBotWebApp.csproj" -c Release -o /app/publish

# Install Playwright browsers
RUN dotnet tool install --global Microsoft.Playwright.CLI && \
    export PATH="$PATH:/root/.dotnet/tools" && \
    playwright install --with-deps

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV PATH="$PATH:/root/.dotnet/tools"
ENTRYPOINT ["dotnet", "WfpChatBotWebApp.dll"]