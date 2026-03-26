# CO2 Dashboard - Docker Setup

Этот проект представляет собой веб-приложение для мониторинга уровня CO2 и температуры с использованием HID устройства.

## Требования

- Docker (версия 20.10 или выше)
- Docker Compose (версия 2.0 или выше)
- USB HID устройство для измерения CO2 (MT8057 или совместимое)

## Структура проекта

```
.
├── Dockerfile              # Dockerfile для Go backend
├── docker-compose.yml      # Конфигурация Docker Compose
├── .dockerignore          # Файлы, исключенные из образа
├── frontend/
│   ├── Dockerfile         # Dockerfile для frontend
│   ├── nginx.conf         # Конфигурация nginx
│   └── .dockerignore      # Файлы, исключенные из образа frontend
└── data/                  # Директория для хранения данных SQLite (создается автоматически)
```

## Быстрый старт

### 1. Создайте директорию для данных (если не существует)

```bash
mkdir data
```

> **Примечание:** Данные SQLite базы будут сохраняться в директории `./data` на хосте, что обеспечивает их сохранность при перезапуске контейнеров.

### 2. Соберите и запустите контейнеры

```bash
docker-compose up -d --build
```

### 3. Проверьте статус контейнеров

```bash
docker-compose ps
```

### 4. Доступ к приложению

- **Frontend**: http://localhost:8080
- **Backend API**: http://localhost:8072

## Управление контейнерами

### Просмотр логов

```bash
# Все контейнеры
docker-compose logs -f

# Только backend
docker-compose logs -f backend

# Только frontend
docker-compose logs -f frontend
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

## Переменные окружения

### Backend

| Переменная | Значение по умолчанию | Описание |
|------------|----------------------|----------|
| `TZ` | `Asia/Yekaterinburg` | Часовой пояс |

## Архитектура

### Backend (Go)

- Порт: 8072
- SQLite база данных: `/app/sensor_data.db`
- WebSocket: `/ws`
- API endpoints:
  - `GET /api/data/latest` - Последние N измерений
  - `GET /api/data/history` - Исторические данные за период

### Frontend (Vue.js + Nginx)

- Порт: 8080
- Статические файлы: `/usr/share/nginx/html`
- Проксирование API: `/api` → `backend:8072`
- Проксирование WebSocket: `/ws` → `backend:8072`

## Разработка

### Локальная разработка с Docker

Для разработки можно использовать volume mounts:

```yaml
# docker-compose.dev.yml
version: '3.8'

services:
  backend:
    build:
      context: .
      dockerfile: Dockerfile
    volumes:
      - .:/app
      - ./data:/app/data
    command: go run main.go -dbpath ./sensor_data.db
    ports:
      - "8072:8072"
    devices:
      - "/dev/bus/usb:/dev/bus/usb"
```

Запуск:
```bash
docker-compose -f docker-compose.dev.yml up
```

## Лицензия

См. файл LICENSE в корне проекта.
