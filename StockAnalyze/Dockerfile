# 使用 .NET 8 SDK 作為建置環境
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

# 複製 csproj 檔案並還原依賴
COPY StockAnalyze.csproj ./ 

RUN dotnet restore

# 複製整個專案並建置
COPY . ./ 

# 清除 NuGet 緩存並進行建置
RUN dotnet restore --no-cache
RUN dotnet build -c Release --no-restore

# 安裝 Chromium 和 Playwright 所需的依賴
RUN apt-get update && \
    apt-get install -y chromium wget libnss3 libatk1.0-0 libatk-bridge2.0-0 libcups2 libxkbcommon-x11-0 libxcomposite1 libxrandr2 libxdamage1 libpango-1.0-0 libpangocairo-1.0-0 libasound2 libgbm1 libgtk-3-0 && \
    apt-get clean

# 設定 Playwright 瀏覽器的路徑
ENV PLAYWRIGHT_BROWSERS_PATH="/usr/bin"

# 安裝 Playwright CLI 並下載 Chromium
RUN dotnet tool install --global Microsoft.Playwright.CLI && \
    playwright install --with-deps

# 發佈專案
RUN dotnet publish -c Release -o out

# 使用 .NET 8 執行環境
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# 設定 Playwright 瀏覽器的路徑
ENV PLAYWRIGHT_BROWSERS_PATH="/usr/bin"

# 複製編譯好的應用程式
COPY --from=build-env /app/out . 

# 設定進入點
ENTRYPOINT ["dotnet", "StockAnalyze.dll"]
