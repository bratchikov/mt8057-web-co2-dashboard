# Build frontend
FROM node:20-alpine AS frontend-builder

WORKDIR /app

COPY frontend/package*.json ./

RUN npm install

COPY frontend/ .

RUN npm run build

# Build backend
FROM golang:1.21-alpine AS backend-builder

WORKDIR /app

# Install dependencies
RUN apk add --no-cache git build-base linux-headers eudev-dev

# Copy go.mod and go.sum
COPY go.mod go.sum ./

# Download dependencies
RUN go mod download

# Copy source code
COPY . .

# Copy frontend build from frontend-builder stage
COPY --from=frontend-builder /app/dist ./frontend/dist

# Build the application
RUN CGO_ENABLED=1 GOOS=linux go build -a -installsuffix cgo -o main .

# Runtime stage
FROM alpine:3.18

WORKDIR /app

# Install required packages
RUN apk add --no-cache ca-certificates eudev input-utils

# Create non-root user and add to input group for hidraw access
RUN addgroup -g 1000 -S appgroup && \
    adduser -u 1000 -S appuser -G appgroup input

# Create data directory
RUN mkdir -p /app/data && chown -R appuser:appgroup /app/data

# Copy built binary
COPY --from=backend-builder /app/main .

# Copy frontend build
COPY --from=backend-builder /app/frontend/dist ./frontend/dist

# Set ownership
RUN chown -R appuser:appgroup /app

# Ensure data directory exists with proper permissions (even with volume mount)
RUN mkdir -p /app/data && chown appuser:appgroup /app/data && chmod 777 /app/data

USER appuser

# Expose port
EXPOSE 8072

# Volume for data persistence
VOLUME ["/app/data"]

# Run the application
CMD ["./main", "-dbpath", "/app/data/sensor_data.db"]
