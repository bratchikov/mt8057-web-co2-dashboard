package main

import (
	"database/sql"
	"encoding/json" // For WebSocket message broadcasting
	"errors"
	"flag"
	"fmt"
	"log"
	"net/http"
	"os"
	"path/filepath"
	"strconv"
	"sync"
	"time"

	"github.com/gin-gonic/gin"
	"github.com/gorilla/websocket"
	_ "github.com/mattn/go-sqlite3" // SQLite driver
	"github.com/sstallion/go-hid"
)

var (
	temperature float32 // Current temperature reading
	co2         uint16  // Current CO2 reading
	db          *sql.DB // Database connection

	upgrader = websocket.Upgrader{
		CheckOrigin: func(r *http.Request) bool {
			return true // Allow all origins for simplicity
		},
	}
	clients      = make(map[*websocket.Conn]bool) // Connected WebSocket clients
	clientsMu    sync.Mutex                       // Mutex for clients map
	sensorDataMu sync.Mutex                       // Mutex for sensor data (temperature and co2)
)

// Reading represents a single sensor reading for API and WebSocket.
type Reading struct {
	Timestamp   time.Time `json:"timestamp"`
	Temperature float32   `json:"temperature"`
	CO2         uint16    `json:"co2"`
}

// broadcastMessage sends a message to all connected WebSocket clients.
func broadcastMessage(messageType int, data []byte) {
	clientsMu.Lock()
	defer clientsMu.Unlock()
	for client := range clients {
		err := client.WriteMessage(messageType, data)
		if err != nil {
			log.Printf("Error broadcasting message: %v", err)
			client.Close()
			delete(clients, client)
		}
	}
}

// handleConnections handles incoming WebSocket connections.
func handleConnections(c *gin.Context) {
	ws, err := upgrader.Upgrade(c.Writer, c.Request, nil)
	if err != nil {
		log.Printf("Failed to set websocket upgrade: %+v\n", err)
		return
	}
	defer ws.Close()

	clientsMu.Lock()
	clients[ws] = true
	clientsMu.Unlock()
	log.Println("Client connected")

	// Keep connection alive and detect disconnect
	for {
		// Read a message (can be empty); primarily to detect client disconnect
		_, _, err := ws.ReadMessage()
		if err != nil {
			log.Printf("Client disconnected: %v", err)
			clientsMu.Lock()
			delete(clients, ws)
			clientsMu.Unlock()
			break
		}
	}
}

// getLatestData handles requests for the latest N readings.
func getLatestData(c *gin.Context) {
	countStr := c.DefaultQuery("count", "100") // Default to the last 100 readings
	count, err := strconv.Atoi(countStr)
	if err != nil || count <= 0 {
		c.JSON(http.StatusBadRequest, gin.H{"error": "Invalid count parameter"})
		return
	}

	rows, err := db.Query("SELECT timestamp, temperature, co2 FROM readings ORDER BY timestamp DESC LIMIT ?", count)
	if err != nil {
		log.Printf("Error querying latest data: %v", err)
		c.JSON(http.StatusInternalServerError, gin.H{"error": "Failed to retrieve data"})
		return
	}
	defer rows.Close()

	var readings []Reading
	for rows.Next() {
		var r Reading
		var ts string
		if err := rows.Scan(&ts, &r.Temperature, &r.CO2); err != nil {
			log.Printf("Error scanning row: %v", err)
			continue
		}
		parsedTime, parseErr := time.Parse(time.RFC3339, ts)
		if parseErr != nil {
			parsedTime, parseErr = time.Parse("2006-01-02 15:04:05", ts)
			if parseErr != nil {
				parsedTime, parseErr = time.Parse("2006-01-02 15:04:05.999999999-07:00", ts)
				if parseErr != nil {
					log.Printf("Error parsing timestamp '%s' with multiple formats: %v", ts, parseErr)
					continue
				}
			}
		}
		r.Timestamp = parsedTime
		readings = append(readings, r)
	}
	// Reverse to have chronological order for charts
	for i, j := 0, len(readings)-1; i < j; i, j = i+1, j-1 {
		readings[i], readings[j] = readings[j], readings[i]
	}
	c.JSON(http.StatusOK, readings)
}

func getHistoricalData(c *gin.Context) {
	startTimeStr := c.Query("start_time")
	endTimeStr := c.Query("end_time")

	if startTimeStr == "" || endTimeStr == "" {
		c.JSON(http.StatusBadRequest, gin.H{"error": "start_time and end_time parameters are required"})
		return
	}

	startTime, err := time.Parse(time.RFC3339, startTimeStr)
	if err != nil {
		c.JSON(http.StatusBadRequest, gin.H{"error": "Invalid start_time format, use RFC3339 (e.g., 2023-01-01T00:00:00Z)"})
		return
	}
	endTime, err := time.Parse(time.RFC3339, endTimeStr)
	if err != nil {
		c.JSON(http.StatusBadRequest, gin.H{"error": "Invalid end_time format, use RFC3339 (e.g., 2023-01-01T23:59:59Z)"})
		return
	}

	rows, err := db.Query("SELECT timestamp, temperature, co2 FROM readings WHERE timestamp BETWEEN ? AND ? ORDER BY timestamp ASC", startTime.Format("2006-01-02 15:04:05"), endTime.Format("2006-01-02 15:04:05"))
	if err != nil {
		log.Printf("Error querying historical data: %v", err)
		c.JSON(http.StatusInternalServerError, gin.H{"error": "Failed to retrieve data"})
		return
	}
	defer rows.Close()

	var readings []Reading
	for rows.Next() {
		var r Reading
		var ts string
		if err := rows.Scan(&ts, &r.Temperature, &r.CO2); err != nil {
			log.Printf("Error scanning row: %v", err)
			continue
		}
		parsedTime, parseErr := time.Parse(time.RFC3339, ts)
		if parseErr != nil {
			parsedTime, parseErr = time.Parse("2006-01-02 15:04:05", ts)
			if parseErr != nil {
				parsedTime, parseErr = time.Parse("2006-01-02 15:04:05.999999999-07:00", ts)
				if parseErr != nil {
					log.Printf("Error parsing timestamp '%s' with multiple formats: %v", ts, parseErr)
					continue
				}
			}
		}
		r.Timestamp = parsedTime
		readings = append(readings, r)
	}
	c.JSON(http.StatusOK, readings)
}

// getData reads raw data from the HID device and calls decode.
func getData(dev *hid.Device, shouldDecode bool) error {
	buf := make([]byte, 8)

	i, err := dev.Read(buf)
	if err != nil {
		return err
	}

	if i != len(buf) {
		return errors.New("wrong read data size")
	}

	decode(buf, shouldDecode)
	return nil
}

// decode processes the raw buffer from the HID device.
// It decodes data if shouldDecode is true (for older device models)
// or processes it raw if shouldDecode is false (for newer device models).
func decode(raw_buf []byte, shouldDecode bool) {
	decryptedResult := make([]byte, 8)

	if shouldDecode {
		buf_c := make([]byte, 8)
		copy(buf_c, raw_buf)

		buf_c[0], buf_c[2] = buf_c[2], buf_c[0]
		buf_c[1], buf_c[4] = buf_c[4], buf_c[1]
		buf_c[3], buf_c[7] = buf_c[7], buf_c[3]
		buf_c[5], buf_c[6] = buf_c[6], buf_c[5]

		tmp_c := (buf_c[7] << 5)
		decryptedResult[7] = (buf_c[6] << 5) | (buf_c[7] >> 3)
		decryptedResult[6] = (buf_c[5] << 5) | (buf_c[6] >> 3)
		decryptedResult[5] = (buf_c[4] << 5) | (buf_c[5] >> 3)
		decryptedResult[4] = (buf_c[3] << 5) | (buf_c[4] >> 3)
		decryptedResult[3] = (buf_c[2] << 5) | (buf_c[3] >> 3)
		decryptedResult[2] = (buf_c[1] << 5) | (buf_c[2] >> 3)
		decryptedResult[1] = (buf_c[0] << 5) | (buf_c[1] >> 3)
		decryptedResult[0] = tmp_c | (buf_c[0] >> 3)

		magicWord_c := []byte("Htemp99e")
		for i := 0; i < 8; i++ {
			swapped_magic_byte := (magicWord_c[i] << 4) | (magicWord_c[i] >> 4)
			decryptedResult[i] -= swapped_magic_byte
		}
	} else {
		copy(decryptedResult, raw_buf)
	}

	if decryptedResult[4] != 0x0d {
		log.Printf("decode: C-style check - Unexpected data (decryptedResult[4] = %02x, want 0x0d). Aborting packet.", decryptedResult[4])
		return
	}

	packetType := decryptedResult[0]
	w := uint16(decryptedResult[1])<<8 | uint16(decryptedResult[2])

	checksum := decryptedResult[0] + decryptedResult[1] + decryptedResult[2]
	if checksum != decryptedResult[3] {
		log.Printf("decode: Checksum mismatch (expected 0x%x, got 0x%x) for packet type 0x%02x. Aborting packet processing.", checksum, decryptedResult[3], packetType)
		return
	}

	switch packetType {
	case 0x42: // Temperature (CODE_TAMB)
		newTemp := float32(w)*0.0625 - 273.15
		sensorDataMu.Lock()
		temperature = newTemp
		sensorDataMu.Unlock()
	case 0x50: // CO2 (CODE_CNTR)
		newCO2 := w
		sensorDataMu.Lock()
		co2 = newCO2
		sensorDataMu.Unlock()
	default:
		// Unknown packet types are intentionally not logged here for production to reduce noise.
		// If needed for debugging, uncomment the log line below.
		// log.Printf("decode: Unknown or unhandled packet type 0x%02x. Raw buf: %x. Decrypted result: %x", packetType, raw_buf, decryptedResult)
	}
}

// Removed duplicate pollHIDDeviceLoop function. The correct one is at the end of the file.

// initDB initializes the SQLite database
func initDB(dbPath string) error {
	// Ensure the directory exists
	dir := filepath.Dir(dbPath)
	if err := os.MkdirAll(dir, 0777); err != nil {
		return fmt.Errorf("failed to create directory: %w", err)
	}

	// Ensure directory has proper permissions (especially important for volume mounts)
	if err := os.Chmod(dir, 0777); err != nil {
		log.Printf("Warning: Could not chmod directory %s: %v", dir, err)
	}

	// Check if the database file already exists and fix permissions if needed
	if _, err := os.Stat(dbPath); err == nil {
		// File exists, ensure it's writable
		if err := os.Chmod(dbPath, 0666); err != nil {
			log.Printf("Warning: Could not chmod database file: %v", err)
		}
		// Also check for -journal file and fix permissions
		journalPath := dbPath + "-journal"
		if _, err := os.Stat(journalPath); err == nil {
			if err := os.Chmod(journalPath, 0666); err != nil {
				log.Printf("Warning: Could not chmod journal file: %v", err)
			}
		}
	}

	var err error
	db, err = sql.Open("sqlite3", dbPath+"?_busy_timeout=5000")
	if err != nil {
		return fmt.Errorf("failed to open database: %w", err)
	}

	// Test the connection and create table
	createTableSQL := `CREATE TABLE IF NOT EXISTS readings (
		id INTEGER PRIMARY KEY AUTOINCREMENT,
		timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
		temperature REAL,
		co2 INTEGER
	);`

	_, err = db.Exec(createTableSQL)
	if err != nil {
		return fmt.Errorf("failed to create table: %w", err)
	}
	log.Println("Database initialized and table created successfully")
	return nil
}

// saveReading saves a new temperature and CO2 reading to the database
func saveReading(temp float32, co2Val uint16) error {
	insertSQL := `INSERT INTO readings (temperature, co2) VALUES (?, ?)`
	statement, err := db.Prepare(insertSQL)
	if err != nil {
		return fmt.Errorf("failed to prepare insert statement: %w", err)
	}
	defer statement.Close()

	_, err = statement.Exec(temp, co2Val)
	if err != nil {
		return fmt.Errorf("failed to execute insert statement: %w", err)
	}
	log.Printf("Saved reading: temp=%.2f, co2=%d\n", temp, co2Val)
	return nil
}

func main() {
	dbPath := flag.String("dbpath", "/app/data/sensor_data.db", "Path to SQLite database file")
	flag.Parse()

	if err := initDB(*dbPath); err != nil {
		log.Fatalf("Error initializing database: %v", err)
	}
	defer db.Close()

	var shouldDecodeData bool
	var dev *hid.Device
	var err error

	dev, err = hid.OpenFirst(0x04d9, 0xa052)
	if err != nil {
		log.Printf("Warning: Could not open HID device (0x04d9:0xa052): %v", err)
		log.Printf("Running in simulation mode - no sensor data will be collected")
	} else {
		// Determine if data should be decoded based on device release number
		deviceInfo, err := dev.GetDeviceInfo()
		if err != nil {
			log.Printf("Warning: Could not get device info: %v. Assuming older device (will decode).", err)
			shouldDecodeData = true
		} else {
			log.Printf("Device Info from go-hid: Path=%s, VendorID=0x%04x, ProductID=0x%04x, ReleaseNbr=0x%04x, InterfaceNbr=%d",
				deviceInfo.Path, deviceInfo.VendorID, deviceInfo.ProductID, deviceInfo.ReleaseNbr, deviceInfo.InterfaceNbr)
			// Logic from C-code (co2mon.c): decode_data = 1 (decode) if release_number <= 0x0100
			if deviceInfo.ReleaseNbr <= 0x0100 {
				shouldDecodeData = true
				log.Println("Device ReleaseNbr <= 0x0100. Data will be decoded.")
			} else {
				shouldDecodeData = false
				log.Println("Device ReleaseNbr > 0x0100. Data will NOT be decoded (treated as 'new' device model).")
			}
		}

		// Send initial feature report (Report ID 0, 8 zero bytes of data)
		// This is analogous to co2mon_send_magic_table(dev, magic_table) in C
		sendReportBuf := make([]byte, 9) // Report ID + 8 bytes data
		sendReportBuf[0] = 0x00

		bytesSent, err := dev.SendFeatureReport(sendReportBuf)
		if err != nil {
			log.Fatalf("Error sending initial feature report to HID device: %v", err)
		}
		log.Printf("Initial feature report sent. Bytes written: %d", bytesSent)
		log.Println("Decryption key logic is now handled within decode function (effectively a zero key if decoding).")

		// Start goroutine for continuous HID data polling
		go pollHIDDeviceLoop(dev, shouldDecodeData)
		log.Println("HID polling goroutine started.")
	}

	// Goroutine for periodic saving to DB and broadcasting via WebSocket
	go func() {
		initialDelayTimer := time.NewTimer(5 * time.Second) // Allow some time for initial data
		<-initialDelayTimer.C

		ticker := time.NewTicker(10 * time.Second) // Save/broadcast every 10 seconds
		defer ticker.Stop()
		for {
			select {
			case <-ticker.C:
				var currentTemp float32
				var currentCO2 uint16

				sensorDataMu.Lock()
				currentTemp = temperature
				currentCO2 = co2
				sensorDataMu.Unlock()

				if currentTemp != 0 || currentCO2 != 0 {
					log.Printf("Periodic Save/Broadcast: temp=%.2f, co2=%d\n", currentTemp, currentCO2)
					if err := saveReading(currentTemp, currentCO2); err != nil {
						log.Printf("Error saving periodic reading: %v", err)
					}

					readingData := Reading{
						Timestamp:   time.Now(),
						Temperature: currentTemp,
						CO2:         currentCO2,
					}
					jsonData, err := json.Marshal(readingData)
					if err != nil {
						log.Printf("Error marshalling reading data to JSON: %v", err)
					} else {
						broadcastMessage(websocket.TextMessage, jsonData)
						log.Println("Broadcasted new reading to clients")
					}
				} else {
					log.Println("Periodic Save/Broadcast: No valid data to send/save yet (temp or co2 is zero).")
				}
			}
		}
	}()

	// Setup Gin router
	router := gin.Default()

	// API routes
	apiGroup := router.Group("/api")
	{
		apiGroup.GET("/data/latest", getLatestData)
		apiGroup.GET("/data/history", getHistoricalData)
	}

	// WebSocket route
	router.GET("/ws", handleConnections)

	// Serve static files for frontend
	router.StaticFS("/ui", http.Dir("./frontend/dist"))
	router.GET("/", func(c *gin.Context) {
		c.Redirect(http.StatusMovedPermanently, "/ui/index.html")
	})

	log.Println("Starting server on :8072")
	if err := router.Run(":8072"); err != nil {
		log.Fatalf("Failed to run server: %v", err)
	}
}

// pollHIDDeviceLoop continuously polls the HID device for data.
// It updates the global temperature and co2 variables under a mutex.
func pollHIDDeviceLoop(dev *hid.Device, shouldDecode bool) {
	log.Println("Starting HID polling loop...")
	// Small initial delay before the first read in the loop,
	// to allow the rest of main to initialize if needed.
	time.Sleep(1 * time.Second)

	for {
		err := getData(dev, shouldDecode) // getData calls decode, which updates global vars under mutex
		if err != nil {
			log.Printf("Error in pollHIDDeviceLoop - getData: %v. Retrying in 5s.", err)
			time.Sleep(5 * time.Second) // Pause on read error
			continue
		}
		// If getData worked without errors, decode was called.
		// decode itself handles whether temperature/co2 were actually updated.

		// Small delay between successful read attempts to avoid high CPU/device load.
		time.Sleep(500 * time.Millisecond)
	}
}
