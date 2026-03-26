# Build stage
FROM golang:1.21-alpine AS builder

WORKDIR /app

# Install dependencies
RUN apk add --no-cache git build-base linux-headers

# Copy go.mod and go.sum
COPY go.mod go.sum ./

# Download dependencies
RUN go mod download

# Copy source code
COPY . .

# Build the application
RUN CGO_ENABLED=1 GOOS=linux go build -a -installsuffix cgo -o main .

# Runtime stage
FROM alpine:latest

WORKDIR /app

# Install required packages
RUN apk add --no-cache ca-certificates udev

# Create non-root user
RUN addgroup -g 1000 -S appgroup && \
    adduser -u 1000 -S appuser -G appgroup

# Create data directory
RUN mkdir -p /app/data && chown -R appuser:appgroup /app/data

# Copy built binary
COPY --from=builder /app/main .

# Copy frontend build
COPY --from=builder /app/frontend/dist ./frontend/dist

# Set ownership
RUN chown -R appuser:appgroup /app

USER appuser

# Expose port
EXPOSE 8072

# Volume for data persistence
VOLUME ["/app/data"]

# Run the application
CMD ["./main", "-dbpath", "/app/data/sensor_data.db"]
