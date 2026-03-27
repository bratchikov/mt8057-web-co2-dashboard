# CO2 Dashboard - Docker Setup

Этот проект представляет собой веб-приложение для мониторинга уровня CO2 и температуры с использованием HID устройства.

## Требования

- Docker (версия 20.10 или выше)
- Docker Compose (версия 2.0 или выше)
- USB HID устройство для измерения CO2 (MT8057 или совместимое)

## Структура проекта

```
.
├── Dockerfile              # Dockerfile для Go backend (с встроенным frontend)
├── docker-compose.yml      # Конфигурация Docker Compose
├── .dockerignore          # Файлы, исключенные из образа
├── frontend/              # Исходный код frontend (Vue.js)
│   ├── .dockerignore      # Файлы, исключенные из образа frontend
│   └── ...
└── data/                  # Директория для хранения данных SQLite (создается автоматически)
```

## Быстрый старт

### 1. Создайте директорию для данных (если не существует)

```bash
mkdir data
```

> **Примечание:** Данные SQLite базы будут сохраняться в директории `./data` на хосте, что обеспечивает их сохранность при перезапуске контейнера.

### 2. Соберите и запустите контейнер

```bash
docker-compose up -d --build
```

### 3. Проверьте статус контейнера

```bash
docker-compose ps
```

### 4. Доступ к приложению

- **Приложение**: http://localhost:8072/ui (или http://localhost:8072)
- **Backend API**: http://localhost:8072/api

> **Примечание:** Frontend теперь обслуживается тем же Go backend через `router.StaticFS("/ui", ...)`.

## Управление контейнерами

### Просмотр логов

```bash
# Все контейнеры
docker-compose logs -f

# Только backend
docker-compose logs -f backend
```

### Остановка контейнеров

```bash
docker-compose down
```

### Перезапуск контейнеров

```bash
docker-compose restart
```

### Остановка и удаление контейнеров с данными

```bash
docker-compose down -v
```

## Логирование и отладка

### Просмотр логов контейнера

```bash
# Все контейнеры
docker-compose logs -f

# Только backend
docker-compose logs -f backend

# Логи за последнюю минуту
docker-compose logs --tail=1m backend
```

### Отладка внутри контейнера

```bash
# Войти в контейнер
docker exec -it co2-backend sh

# Проверить права доступа к базе данных
ls -la /app/data

# Проверить логи приложения (если используется файловое логирование)
cat /app/data/app.log 2>/dev/null || echo "Файловое логирование не настроено"
```

### Проверка подключения к USB устройству

```bash
# Проверить, что устройство доступно в контейнере
docker exec co2-backend ls -la /dev/bus/usb

# Проверить логи на наличие ошибок USB
docker-compose logs backend | grep -i "usb\|device"
```

## Работа с USB устройством

Контейнер backend имеет прямой доступ к USB устройству через `/dev/bus/usb`. Убедитесь, что:

1. Устройство подключено к хосту
2. У вас есть права на доступ к USB устройствам (членство в группе `plugdev` или `dialout`)

### Проверка USB устройства на хосте

```bash
# Windows (WSL2)
lsusb

# Linux
ls -la /dev/bus/usb
```

## Настройка прав доступа к USB устройству

### Linux (udev правила)

Для корректной работы с USB HID устройством на Linux необходимо настроить udev правила:

1. Создайте файл правила `/etc/udev/rules.d/99-co2-sensor.rules`:

```bash
sudo nano /etc/udev/rules.d/99-co2-sensor.rules
```

2. Добавьте следующее содержимое (VendorID: 0x04d9, ProductID: 0xa052):

```udev
# MT8057 CO2 Sensor
SUBSYSTEM=="usb", ATTR{idVendor}=="04d9", ATTR{idProduct}=="a052", MODE="0666"
SUBSYSTEM=="usb_device", ATTR{idVendor}=="04d9", ATTR{idProduct}=="a052", MODE="0666"
```

3. Перезагрузите udev правила:

```bash
sudo udevadm control --reload-rules
sudo udevadm trigger
```

4. Отключите и подключите заново USB устройство

### Группа plugdev/dialout

Если udev правила не работают, добавьте пользователя в группу `plugdev` или `dialout`:

```bash
# Для группы plugdev (Ubuntu/Debian)
sudo usermod -a -G plugdev $USER

# Для группы dialout (Arch Linux)
sudo usermod -a -G dialout $USER

# Перезапустите терминал или выполните:
newgrp plugdev
# или
newgrp dialout
```

### Windows (WSL2)

На Windows с WSL2:
1. Убедитесь, что установлен [USBIPD-WIN](https://github.com/dorssel/usbipd-win)
2. Подключите устройство к хосту Windows
3. В WSL2 выполните:
   ```bash
   # Просмотр подключенных USB устройств
   lsusb
   
   # Устройство должно быть доступно через /dev/bus/usb
   ls -la /dev/bus/usb
   ```

## Обновление образа и миграция данных

### Обновление без пересборки с нуля

```bash
# Остановите контейнер
docker-compose down

# Удалите старый образ
docker rmi mt8057-web-co2-dashboard_backend

# Пересоберите и запустите
docker-compose up -d --build
```

### Сохранение данных при обновлении

Данные сохраняются в volume mount `./data:/app/data`, поэтому они не удалятся при обновлении:

```bash
# Обновление с сохранением данных
docker-compose pull  # если используется образ из репозитория
docker-compose up -d --build
```

### Резервное копирование базы данных

```bash
# Создать резервную копию
docker exec co2-backend cp /app/data/sensor_data.db /app/data/sensor_data.db.backup

# Скопировать резервную копию на хост
docker cp co2-backend:/app/data/sensor_data.db.backup ./data/sensor_data.db.backup

# Восстановить из резервной копии
docker cp ./data/sensor_data.db.backup co2-backend:/app/data/sensor_data.db
docker exec co2-backend chown appuser:appgroup /app/data/sensor_data.db
```

## Проблемы и решения

### Контейнер не может найти USB устройство

1. Проверьте, что устройство подключено:
   ```bash
   ls -la /dev/bus/usb
   ```

2. Проверьте права доступа:
   ```bash
   sudo usermod -a -G plugdev $USER
   # Перезапустите терминал или выполните:
   newgrp plugdev
   ```

3. Перезапустите контейнер:
   ```bash
   docker-compose restart backend
   ```

### Ошибки при сборке

1. Очистите кэш Docker:
   ```bash
   docker-compose down -v
   docker builder prune
   docker-compose up -d --build
   ```

### Данные не сохраняются

Убедитесь, что директория `data` существует и имеет правильные права:
```bash
mkdir -p data
chmod 755 data
```

### Ошибка "unable to open database file"

Эта ошибка может возникнуть, если директория `/app/data` не существует или не имеет правильных прав. В Dockerfile директория создается автоматически, но если вы используете volume mount, убедитесь, что:

1. Директория `data` существует на хосте:
   ```bash
   mkdir -p data
   chmod 755 data
   ```

2. Перезапустите контейнер:
   ```bash
   docker-compose restart backend
   ```

3. Если проблема сохраняется, пересоберите образ:
   ```bash
   docker-compose down -v
   docker-compose up -d --build
   ```

## Переменные окружения

### Backend

| Переменная | Значение по умолчанию | Описание |
|------------|----------------------|----------|
| `TZ` | `Asia/Yekaterinburg` | Часовой пояс |

> **Примечание:** Порт 8072 жестко задан в коде приложения и не может быть изменен через переменные окружения. Для изменения порта требуется изменение исходного кода и пересборка образа.

## Архитектура

### Backend (Go)

- Порт: 8072 (жестко задан)
- SQLite база данных: `/app/data/sensor_data.db`
- Флаг `-dbpath`: Путь к базе данных (по умолчанию `/app/data/sensor_data.db`)
- WebSocket: `/ws`
- API endpoints:
  - `GET /api/data/latest` - Последние N измерений
  - `GET /api/data/history` - Исторические данные за период
- Frontend (статические файлы): `/ui` (обслуживается тем же backend)

### Frontend (Vue.js)

- Собирается в процессе сборки backend-образа
- Доступен по пути `/ui` через Go backend
- Удален отдельный nginx контейнер для упрощения архитектуры

## Разработка

### Локальная разработка с Docker

Для разработки можно использовать volume mounts. Создайте файл `docker-compose.dev.yml`:

```yaml
version: '3.8'

services:
  backend:
    build:
      context: .
      dockerfile: Dockerfile
    volumes:
      - .:/app
      - ./data:/app/data
    command: go run main.go -dbpath /app/data/sensor_data.db
    ports:
      - "8072:8072"
    devices:
      - "/dev/bus/usb:/dev/bus/usb"
```

> **Примечание:** При использовании volume mounts для разработки убедитесь, что:
> - Путь к базе данных в команде (`-dbpath`) соответствует пути в Dockerfile (`/app/data/sensor_data.db`)
> - Директория `./data` существует на хосте
> - Права доступа к директории позволяют записи

Запуск:
```bash
docker-compose -f docker-compose.dev.yml up
```

> **Примечание:** Для локальной разработки без Docker рекомендуется использовать `go run main.go` напрямую на хосте с установленным Go и необходимыми зависимостями.

## Резервное копирование базы данных

### Ручное резервное копирование

```bash
# Создать резервную копию внутри контейнера
docker exec co2-backend cp /app/data/sensor_data.db /app/data/sensor_data.db.backup

# Скопировать резервную копию на хост
docker cp co2-backend:/app/data/sensor_data.db.backup ./data/sensor_data.db.backup

# Восстановить из резервной копии
docker cp ./data/sensor_data.db.backup co2-backend:/app/data/sensor_data.db
docker exec co2-backend chown appuser:appgroup /app/data/sensor_data.db
```

### Автоматическое резервное копирование

Создайте скрипт `backup.sh`:

```bash
#!/bin/bash
BACKUP_DIR="./data/backups"
DATE=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="$BACKUP_DIR/sensor_data_$DATE.db"

mkdir -p "$BACKUP_DIR"
docker exec co2-backend cp /app/data/sensor_data.db "$BACKUP_FILE"
echo "Резервная копия создана: $BACKUP_FILE"
```

## Пример .env файла

Создайте файл `.env` в корне проекта для настройки переменных окружения:

```env
# Часовой пояс
TZ=Asia/Yekaterinburg

# Путь к базе данных (опционально, по умолчанию /app/data/sensor_data.db)
# DB_PATH=/app/data/sensor_data.db
```

> **Примечание:** Переменная `TZ` используется для настройки часового пояса контейнера. Порт 8072 и путь к базе данных жестко заданы в коде и не могут быть изменены через переменные окружения.

## Лицензия

См. файл LICENSE в корне проекта.
