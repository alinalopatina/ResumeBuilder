# Образ веб-службы ResumeBuilder.Api (лаба 10.2).
# Собирается из корня репозитория:  docker build -f src/ResumeBuilder.Api/Dockerfile -t resumebuilder-api:1.2.0 .
# Многоэтапная сборка: в этапе build есть .NET SDK (~800 МБ), в итоговый образ попадает
# только среда выполнения ASP.NET (~220 МБ) и опубликованная программа.

# ---------- этап 1: сборка ----------
# Этап сборки выполняется на платформе сборщика (быстро), результат подходит и для amd64, и для arm64
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG VERSION=1.2.0
WORKDIR /src

# Сначала только файлы проектов и restore: этот слой кэшируется Docker'ом
# и не пересобирается, пока не поменялись зависимости (NuGet-пакеты)
COPY Directory.Build.props ./
COPY src/ResumeBuilder.Core/ResumeBuilder.Core.csproj src/ResumeBuilder.Core/
COPY src/ResumeBuilder.Api/ResumeBuilder.Api.csproj src/ResumeBuilder.Api/
RUN dotnet restore src/ResumeBuilder.Api/ResumeBuilder.Api.csproj

# Потом исходный код и публикация
COPY src/ResumeBuilder.Core/ src/ResumeBuilder.Core/
COPY src/ResumeBuilder.Api/ src/ResumeBuilder.Api/
RUN dotnet publish src/ResumeBuilder.Api/ResumeBuilder.Api.csproj -c Release -o /app --no-restore -p:Version=$VERSION

# ---------- этап 2: итоговый образ ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
ARG VERSION=1.2.0
LABEL org.opencontainers.image.title="ResumeBuilder.Api" \
      org.opencontainers.image.version=$VERSION \
      org.opencontainers.image.source="https://github.com/alinalopatina/ResumeBuilder"

# Шрифты с кириллицей и fontconfig нужны QuestPDF для PDF; curl — для проверки здоровья
RUN apt-get update \
 && apt-get install -y --no-install-recommends libfontconfig1 fonts-liberation fonts-dejavu-core curl \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app .

# Конфигурация через переменные окружения (можно переопределить в docker run -e / compose)
ENV ASPNETCORE_HTTP_PORTS=8080 \
    App__DefaultLanguage=ru-RU \
    App__QrPixelsPerModule=8
EXPOSE 8080

# Не запускаем от root: пользователь app уже есть в образах .NET 8
USER $APP_UID

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD curl -fs http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "ResumeBuilder.Api.dll"]
