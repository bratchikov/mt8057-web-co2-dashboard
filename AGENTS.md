# CO2 Dashboard - Project Agents

This document provides comprehensive information for AI agents working on the CO2 Dashboard project.

## Overview

The CO2 Dashboard is a full-stack web application for monitoring CO2 levels and temperature using a USB HID-based sensor device (primarily Даджет MT8057). The application features real-time data visualization, historical data analysis, configurable alerts, and a responsive web interface.

### Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                         User Browser                                │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │                    Vue.js Frontend                            │  │
│  │  - Real-time charts (ECharts)                                 │  │
│  │  - Historical data browser                                    │  │
│  │  - Theme switching (Light/Dark)                               │  │
│  │  - CO2 alert notifications                                    │  │
│  └───────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                    ┌───────────────┴───────────────┐
                    │  WebSocket (/ws)              │
                    │  HTTP API (/api)              │
                    └───────────────┬───────────────┘
┌─────────────────────────────────────────────────────────────────────┐
│                      Go Backend (main.go)                           │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │  Gin HTTP Server (port 8072)                                  │  │
│  │  - GET /api/data/latest   - Get latest N readings            │  │
│  │  - GET /api/data/history  - Get historical data by time range│  │
│  │  - GET /ws                - WebSocket endpoint               │  │
│  └───────────────────────────────────────────────────────────────┘  │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │  Data Processing Layer                                        │  │
│  │  - HID device communication (go-hid)                          │  │
│  │  - Data decoding algorithm                                    │  │
│  │  - SQLite database operations                                 │  │
│  │  - WebSocket broadcasting                                     │  │
│  └───────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                    ┌───────────────┴───────────────┐
                    │  USB HID Device               │
                    │  (MT8057 or compatible)       │
                    └───────────────────────────────┘
```

## Key Modules

### 1. Backend (`main.go`)

The Go backend handles sensor data collection, storage, and API serving.

#### Core Components

| Component | Description |
|-----------|-------------|
| `Reading` struct | Data model for sensor readings (timestamp, temperature, CO2) |
| `broadcastMessage()` | WebSocket message broadcasting to all connected clients |
| `handleConnections()` | WebSocket connection handler |
| `getLatestData()` | API endpoint for latest N readings |
| `getHistoricalData()` | API endpoint for historical data by time range |
| `decode()` | HID data decoding algorithm |
| `saveReading()` | Database persistence function |
| `pollHIDDeviceLoop()` | Continuous HID device polling goroutine |

#### API Functions

##### [`getLatestData()`](main.go:88)
Retrieves the latest N sensor readings from the database.

```go
func getLatestData(c *gin.Context) {
    countStr := c.DefaultQuery("count", "100") // Default to last 100 readings
    count, err := strconv.Atoi(countStr)
    // Returns JSON array of Reading objects
}
```

**Query Parameters:**
- `count` (optional, default: "100"): Number of latest readings to return

**Response:**
```json
[
  {
    "timestamp": "2024-01-15T10:30:00Z",
    "temperature": 22.5,
    "co2": 450
  }
]
```

##### [`getHistoricalData()`](main.go:133)
Retrieves sensor readings within a specified time range.

```go
func getHistoricalData(c *gin.Context) {
    startTimeStr := c.Query("start_time")
    endTimeStr := c.Query("end_time")
    // Returns JSON array of Reading objects
}
```

**Query Parameters:**
- `start_time` (required): RFC3339 formatted timestamp (e.g., "2024-01-15T00:00:00Z")
- `end_time` (required): RFC3339 formatted timestamp

##### [`handleConnections()`](main.go:60)
WebSocket connection handler for real-time data streaming.

```go
func handleConnections(c *gin.Context) {
    ws, err := upgrader.Upgrade(c.Writer, c.Request, nil)
    // Keeps connection alive, detects disconnects
}
```

### 2. Frontend (`frontend/src/`)

The Vue.js 3 frontend provides the user interface for data visualization.

#### Core Components

| Component | Description |
|-----------|-------------|
| `App.vue` | Main application component with layout and state management |
| `TemperatureChart.vue` | ECharts-based temperature visualization |
| `CO2Chart.vue` | ECharts-based CO2 visualization |

#### Frontend API Calls

```javascript
// Fetch latest data
const response = await fetch(`/api/data/latest?count=${MAX_DATA_POINTS}`);

// WebSocket connection for real-time updates
const ws = new WebSocket(`ws://${window.location.host}/ws`);
```

## Technology Stack

### Backend
- **Language:** Go 1.19
- **HTTP Framework:** Gin-gonic Gin v1.10.1
- **Database:** SQLite3 (via go-sqlite3)
- **HID Communication:** go-hid v0.13.2
- **WebSocket:** gorilla/websocket v1.5.3

### Frontend
- **Framework:** Vue.js 3.5.13
- **Build Tool:** Vite 6.3.5
- **Charts:** ECharts 5.6.0 (via vue-echarts)
- **Language:** JavaScript (ES6+)

### Infrastructure
- **Containerization:** Docker (multi-stage build)
- **Orchestration:** Docker Compose
- **Base Image:** Alpine Linux 3.18

## Coding Rules

### General Principles
1. **Type Safety:** Use TypeScript strict mode for frontend code
2. **No Fluent API:** Avoid method chaining patterns; use explicit function calls
3. **Error Handling:** Always handle errors explicitly; log meaningful messages
4. **Concurrency:** Use mutexes for shared data access (`sensorDataMu`, `clientsMu`)
5. **No External Dependencies:** No external dependencies without explicit approval

### Go Coding Standards
```go
// ✅ Good: Explicit error handling
if err != nil {
    log.Printf("Error: %v", err)
    return fmt.Errorf("operation failed: %w", err)
}

// ❌ Bad: Ignoring errors
result, _ := someOperation()  // Never ignore errors
```

### Vue.js Coding Standards
```javascript
// ✅ Good: Explicit imports
import { ref, computed, onMounted } from 'vue';

// ✅ Good: Reactive data with reactive()
const data = reactive({ values: [], timestamps: [] });

// ❌ Bad: Implicit reactivity without proper Vue reactivity system
let data = { values: [] };  // Not reactive!
```

## Tests

### Running Tests

```bash
# Backend tests (if any)
go test ./...

# Frontend tests (if any)
cd frontend
pnpm test
```

### Test Commands
- **Backend:** `go test -v ./...` - Run all backend tests with verbose output
- **Frontend:** `pnpm test` - Run frontend unit tests
- **Integration:** Run the full application and verify API endpoints manually

### Test Requirements
- **Always add tests** for new features or bug fixes
- Backend tests should cover data processing logic
- Frontend tests should cover component behavior and API integration

## Docker

### Prerequisites
- Docker 20.10 or higher
- Docker Compose 2.0 or higher
- USB HID device connected (for sensor data)

### Quick Start

```bash
# Build and start containers
docker-compose up -d --build

# Check status
docker-compose ps

# View logs
docker-compose logs -f
```

### Docker Commands

| Command | Description |
|---------|-------------|
| `docker-compose up -d --build` | Build and start containers in detached mode |
| `docker-compose down` | Stop and remove containers |
| `docker-compose down -v` | Stop, remove containers AND volumes (data loss!) |
| `docker-compose restart` | Restart all containers |
| `docker-compose logs -f` | Follow logs from all containers |
| `docker-compose logs -f backend` | Follow backend container logs |

### Volume Mounts
- `./data:/app/data:rw` - SQLite database storage (persists across restarts)

### Network
- Backend exposed on port `8072`
- Frontend served at `/ui` endpoint

### Access Points
- **Application:** http://localhost:8072/ui
- **Backend API:** http://localhost:8072/api
- **WebSocket:** ws://localhost:8072/ws

## Restrictions

### Dependency Management
- **No external dependencies without approval** - All new dependencies must be reviewed and approved
- Run `go mod tidy` after adding/removing dependencies
- Frontend dependencies must be added via `pnpm add` in the `frontend/` directory

### Testing Requirements
- **Always add tests** for new features
- Bug fixes should include regression tests
- API changes require endpoint tests

### Code Review
- All changes must be reviewed before merging
- Test coverage should not decrease
- Security implications must be considered

## Project Structure

```
.
├── AGENTS.md              # This file - project documentation for AI agents
├── README.md              # User-facing documentation
├── README-Docker.md       # Docker-specific documentation
├── docker-compose.yml     # Docker Compose configuration
├── Dockerfile             # Multi-stage Docker build
├── .dockerignore          # Docker build exclusions
├── .env.example           # Environment variable template
├── go.mod                 # Go module definition
├── go.sum                 # Go dependency checksums
├── main.go                # Backend entry point
├── data/                  # SQLite database storage (created at runtime)
└── frontend/              # Vue.js frontend application
    ├── src/
    │   ├── App.vue        # Main Vue component
    │   ├── main.js        # Frontend entry point
    │   ├── style.css      # Global styles
    │   └── components/    # Vue components
    │       ├── CO2Chart.vue
    │       └── TemperatureChart.vue
    ├── public/            # Static assets
    ├── index.html         # Frontend HTML template
    ├── package.json       # Frontend dependencies
    └── vite.config.js     # Vite build configuration
```

## API Reference

### HTTP Endpoints

#### GET `/api/data/latest`
Get the latest N sensor readings.

**Query Parameters:**
- `count` (optional, default: 100): Number of readings to return

**Response:** `200 OK` - JSON array of Reading objects

#### GET `/api/data/history`
Get sensor readings within a time range.

**Query Parameters:**
- `start_time` (required): RFC3339 timestamp
- `end_time` (required): RFC3339 timestamp

**Response:** `200 OK` - JSON array of Reading objects

### WebSocket Endpoint

#### ws `/ws`
Real-time data streaming via WebSocket.

**Connection:** Keep-alive connection that receives JSON messages

**Message Format:**
```json
{
  "timestamp": "2024-01-15T10:30:00Z",
  "temperature": 22.5,
  "co2": 450
}
```

## Troubleshooting

### Common Issues

1. **HID Device Not Found**
   - Check USB device connection
   - Verify device permissions (`/dev/hidraw0`, `/dev/bus/usb`)
   - Check logs: `docker-compose logs backend`

2. **Database Permission Errors**
   - Ensure `./data` directory exists and is writable
   - Check volume mount: `docker-compose.yml` line 13

3. **Frontend Not Loading**
   - Verify backend is running: `docker-compose ps`
   - Check browser console for API errors
   - Ensure frontend was built: `docker-compose logs frontend-builder`

## Maintenance

### Database Backup
```bash
# Copy database from container
docker cp co2-backend:/app/data/sensor_data.db ./data/sensor_data.db.backup
```

### Database Reset
```bash
# Stop containers
docker-compose down

# Remove data volume
docker-compose down -v

# Restart
docker-compose up -d --build
```

### Updating Dependencies
```bash
# Backend
go get new/package@version
go mod tidy

# Frontend
cd frontend
pnpm add package@version
pnpm install
```
