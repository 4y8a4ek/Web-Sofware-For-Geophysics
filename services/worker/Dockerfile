# Этап сборки
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src

# Копируем файлы проектов для восстановления зависимостей
COPY services/worker/Worker.csproj services/worker/
COPY services/MeshDataStructures/MeshDataStructures.csproj services/MeshDataStructures/

# Восстанавливаем зависимости
RUN dotnet restore "services/worker/Worker.csproj"

# Копируем все остальные исходники
COPY . .

# Переходим в папку worker и публикуем (без --no-restore, чтобы перепроверить)
WORKDIR /src/services/worker
RUN dotnet publish "Worker.csproj" -c Release -o /app/publish

# Финальный образ с рантаймом
FROM mcr.microsoft.com/dotnet/runtime:7.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Worker.dll"]