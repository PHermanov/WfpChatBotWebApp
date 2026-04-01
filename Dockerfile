FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview-jammy

WORKDIR /app

RUN apt-get update && apt-get install -y \
    wget \
    gnupg \
    ca-certificates \
    libgtk-4-1 \
    libgraphene-1.0-0 \
    libevent-2.1-7 \
    libopus0 \
    libgstreamer1.0-0 \
    libgstreamer-plugins-base1.0-0 \
    libflite1 \
    libavif16 \
    libharfbuzz-icu0 \
    libsecret-1-0 \
    libwoff2-1 \
    libx264-163 \
    libgles2 \
    && rm -rf /var/lib/apt/lists/*

COPY . .

RUN dotnet publish WfpChatBotWebApp/WfpChatBotWebApp.csproj -c Release -o /app/out

WORKDIR /app/out

RUN dotnet tool install --global Microsoft.Playwright.CLI \
    && playwright install --with-deps

ENV PATH="$PATH:/root/.dotnet/tools"
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "WfpChatBotWebApp.dll"]