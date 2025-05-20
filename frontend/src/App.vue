<script setup>
import { ref, onMounted, reactive, computed, nextTick, watch } from 'vue'; // Added watch
import * as echarts from 'echarts/core'; // Import echarts for connect API
import TemperatureChart from './components/TemperatureChart.vue';
import CO2Chart from './components/CO2Chart.vue';

const temperatureData = reactive({
  timestamps: [],
  values: []
});
const co2Data = reactive({
  timestamps: [],
  values: []
});

const statusMessage = ref('Connecting to server...');
const MAX_DATA_POINTS = 300; // Keep last 300 points for live chart
const dataZoomResetKey = ref(0); // Key to trigger dataZoom reset in child charts
const activeFilter = ref('Live'); // To track the currently active filter button
const currentTheme = ref(localStorage.getItem('theme') || 'light');

// CO2 Alert states
const co2Threshold1 = ref(parseInt(localStorage.getItem('co2Threshold1') || '1000', 10));
const co2Threshold2 = ref(parseInt(localStorage.getItem('co2Threshold2') || '1500', 10));
const co2AlertLevel = ref(0); // 0: normal, 1: threshold1, 2: threshold2
// Flags to track if a notification has been sent for the current alert state
const co2Alert1Notified = ref(false); 
const co2Alert2Notified = ref(false);

// Persist alert settings
watch(co2Threshold1, (newValue) => localStorage.setItem('co2Threshold1', newValue.toString()));
watch(co2Threshold2, (newValue) => localStorage.setItem('co2Threshold2', newValue.toString()));


// Function to apply theme class to body
const applyTheme = (themeName) => {
  if (themeName === 'dark') {
    document.body.classList.add('dark-theme');
    document.body.classList.remove('light-theme');
  } else {
    document.body.classList.add('light-theme');
    document.body.classList.remove('dark-theme');
  }
};

// Watch for theme changes and apply them
watch(currentTheme, (newTheme) => {
  localStorage.setItem('theme', newTheme);
  applyTheme(newTheme);
}, { immediate: true }); // immediate: true to apply on initial load

const toggleTheme = () => {
  currentTheme.value = currentTheme.value === 'light' ? 'dark' : 'light';
};

function formatChartData(rawData) {
  const timestamps = [];
  const tempData = [];
  const co2Values = [];

  rawData.forEach(item => {
    timestamps.push(new Date(item.timestamp).toLocaleTimeString());
    tempData.push(item.temperature.toFixed(2));
    co2Values.push(item.co2);
  });
  return { timestamps, tempData, co2Values };
}

async function fetchInitialData() {
  try {
    statusMessage.value = 'Fetching initial data...';
    // Adjust API host and port if not running on the same as the frontend development server
    const response = await fetch(`/api/data/latest?count=${MAX_DATA_POINTS}`);
    if (!response.ok) {
      throw new Error(`HTTP error! status: ${response.status}`);
    }
    const data = await response.json();
    
    const { timestamps, tempData, co2Values } = formatChartData(data);

    temperatureData.timestamps = [...timestamps];
    temperatureData.values = [...tempData];
    co2Data.timestamps = [...timestamps]; // Assuming CO2 uses the same timestamps
    co2Data.values = [...co2Values];

    // Calculate initial stats for the full dataset
    temperatureStats.value = calculateStats(temperatureData.values);
    co2Stats.value = calculateStats(co2Data.values);
    
    statusMessage.value = 'Initial data loaded. Waiting for live updates...';
  } catch (error) {
    console.error('Error fetching initial data:', error);
    statusMessage.value = `Error fetching initial data: ${error.message}. Please check server.`;
  }
}

function setupWebSocket() {
  const wsProtocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
  const wsUrl = `${wsProtocol}//${window.location.host}/ws`;
  
  statusMessage.value = `Connecting to WebSocket at ${wsUrl}...`;
  const socket = new WebSocket(wsUrl);

  socket.onopen = () => {
    console.log('WebSocket connection established');
    statusMessage.value = 'Connected to server. Receiving live data.';
  };

  socket.onmessage = (event) => {
    try {
      const newDataPoint = JSON.parse(event.data);

      const newTimestamp = new Date(newDataPoint.timestamp).toLocaleTimeString();

      // Update Temperature Data
      temperatureData.timestamps.push(newTimestamp);
      temperatureData.values.push(newDataPoint.temperature.toFixed(2));
      if (temperatureData.timestamps.length > MAX_DATA_POINTS) {
        temperatureData.timestamps.shift();
        temperatureData.values.shift();
      }
       // Trigger reactivity for the chart component by reassigning the array or specific elements
      temperatureData.timestamps = [...temperatureData.timestamps];
      temperatureData.values = [...temperatureData.values];
      // Recalculate stats if not in historical view (i.e., live data is active)
      if (activeFilter.value === 'Live') {
        temperatureStats.value = calculateStats(temperatureData.values);
      }


      // Update CO2 Data
      co2Data.timestamps.push(newTimestamp);
      co2Data.values.push(newDataPoint.co2);
      if (co2Data.timestamps.length > MAX_DATA_POINTS) {
        co2Data.timestamps.shift();
        co2Data.values.shift();
      }
      co2Data.timestamps = [...co2Data.timestamps];
      co2Data.values = [...co2Data.values];
      // Recalculate stats if not in historical view
      if (activeFilter.value === 'Live') {
        co2Stats.value = calculateStats(co2Data.values);
      }
      
      // Check CO2 Alerts
      checkCO2Alerts(newDataPoint.co2);

    } catch (error) {
      console.error('Error processing message from WebSocket:', error);
      statusMessage.value = 'Error processing live data.';
    }
  };

  socket.onerror = (error) => {
    console.error('WebSocket error:', error);
    statusMessage.value = 'WebSocket connection error. Attempting to reconnect...';
  };

  socket.onclose = (event) => {
    console.log('WebSocket connection closed:', event);
    statusMessage.value = 'Disconnected from server. Attempting to reconnect in 5 seconds...';
    // Attempt to reconnect after a delay
    setTimeout(setupWebSocket, 5000);
  };
}

onMounted(() => {
  applyTheme(currentTheme.value); // Apply stored theme on mount
  requestNotificationPermission(); // Request permission on mount
  fetchInitialData();
  setupWebSocket();

  // Connect ECharts instances for synchronized tooltips/axisPointers
  nextTick(() => {
    // This needs to be done after both charts are initialized and their DOM is ready.
    // onMounted ensures components are created, nextTick ensures DOM is updated.
    try {
      echarts.connect('sensorChartsGroup');
      // console.log("ECharts graphs connected with group 'sensorChartsGroup'"); // Keep console log for connect success/failure
    } catch (e) {
      console.error("Failed to connect ECharts graphs:", e);
    }
  });
});

const latestTemperature = computed(() => {
  if (temperatureData.values.length > 0) {
    return temperatureData.values[temperatureData.values.length - 1];
  }
  return 'N/A';
});

const latestCO2 = computed(() => {
  if (co2Data.values.length > 0) {
    return co2Data.values[co2Data.values.length - 1];
  }
  return 'N/A';
});

// Helper function to calculate statistics
function calculateStats(values) {
  if (!values || values.length === 0) {
    return { min: 'N/A', max: 'N/A', avg: 'N/A', median: 'N/A' };
  }
  // Ensure values are numbers for calculations
  const numericValues = values.map(v => parseFloat(v)).filter(v => !isNaN(v));
  if (numericValues.length === 0) {
    return { min: 'N/A', max: 'N/A', avg: 'N/A', median: 'N/A' };
  }

  const min = Math.min(...numericValues).toFixed(2);
  const max = Math.max(...numericValues).toFixed(2);
  const sum = numericValues.reduce((acc, val) => acc + val, 0);
  const avg = (sum / numericValues.length).toFixed(2);
  
  const sortedValues = [...numericValues].sort((a, b) => a - b);
  let median;
  const mid = Math.floor(sortedValues.length / 2);
  if (sortedValues.length % 2 === 0) {
    median = ((sortedValues[mid - 1] + sortedValues[mid]) / 2).toFixed(2);
  } else {
    median = sortedValues[mid].toFixed(2);
  }
  return { min, max, avg, median };
}

// Make stats simple refs, not computed, as they will be updated by events or direct calls
const temperatureStats = ref({ min: 'N/A', max: 'N/A', avg: 'N/A', median: 'N/A' });
const co2Stats = ref({ min: 'N/A', max: 'N/A', avg: 'N/A', median: 'N/A' });

// Event handlers for stats recalculation from child components
const handleTemperatureStatsRecalculate = (visibleValues) => {
  temperatureStats.value = calculateStats(visibleValues);
};

const handleCO2StatsRecalculate = (visibleValues) => {
  co2Stats.value = calculateStats(visibleValues);
};


async function fetchHistoricalData(days) {
  activeFilter.value = `${days} Day(s)`; // Update active filter
  statusMessage.value = `Fetching data for the last ${days} day(s)...`;
  try {
    const endDate = new Date();
    const startDate = new Date();
    startDate.setDate(endDate.getDate() - days);

    const startTimeStr = startDate.toISOString();
    const endTimeStr = endDate.toISOString();

    const response = await fetch(`/api/data/history?start_time=${startTimeStr}&end_time=${endTimeStr}`);
    if (!response.ok) {
      throw new Error(`HTTP error! status: ${response.status}`);
    }
    const data = await response.json();
    
    const { timestamps, tempData, co2Values } = formatChartData(data);

    dataZoomResetKey.value++; // Increment key to trigger zoom reset in children

    temperatureData.timestamps = [...timestamps];
    temperatureData.values = [...tempData];
    co2Data.timestamps = [...timestamps];
    co2Data.values = [...co2Values];

    // Calculate stats for the newly fetched historical data
    temperatureStats.value = calculateStats(temperatureData.values);
    co2Stats.value = calculateStats(co2Data.values);
    
    statusMessage.value = `Data for the last ${days} day(s) loaded.`;
  } catch (error) {
    console.error(`Error fetching historical data for ${days} days:`, error);
    statusMessage.value = `Error fetching historical data: ${error.message}.`;
  }
}

function loadLiveData() {
  // Essentially re-fetch initial data which shows latest N points and WebSocket will continue
  activeFilter.value = 'Live'; // Update active filter
  dataZoomResetKey.value++; // Increment key to trigger zoom reset in children
  fetchInitialData();
}

const co2DisplayClass = computed(() => {
  if (co2AlertLevel.value === 2) return 'alert-level-2';
  if (co2AlertLevel.value === 1) return 'alert-level-1';
  return '';
});

// Notification Logic
function requestNotificationPermission() {
  if (!("Notification" in window)) {
    console.warn("This browser does not support desktop notification.");
    return Promise.resolve("unsupported"); // Indicate lack of support
  }
  if (Notification.permission === "granted") {
    return Promise.resolve("granted"); // Already granted
  }
  if (Notification.permission !== "denied") { // 'default' or not yet asked
    return Notification.requestPermission(); // Returns a Promise
  }
  // If permission is "denied"
  return Promise.resolve("denied");
}

async function sendBrowserNotification(title, body) {
  // Ensure permission is requested before trying to send a notification
  // This is important if the initial onMounted request wasn't interacted with
  const permission = await requestNotificationPermission(); 

  if (permission === "granted") {
    // Using a unique tag for each notification instance to ensure it shows, especially for debugging
    const uniqueTag = title + "_co2_alert_ts_" + Date.now();
    console.log(`Creating notification with title: "${title}", body: "${body}", tag: "${uniqueTag}"`);
    new Notification(title, { body, tag: uniqueTag });
  } else {
    console.warn(`Notification permission is ${permission}. Cannot send notification: "${title}"`);
  }
}

function checkCO2Alerts(currentCO2) {
  const co2Val = parseFloat(currentCO2);
  let newVisualAlertLevel = 0; // For visual indication on the page

  // Determine visual alert level
  if (co2Val >= co2Threshold2.value) {
    newVisualAlertLevel = 2;
  } else if (co2Val >= co2Threshold1.value) {
    newVisualAlertLevel = 1;
  }
  co2AlertLevel.value = newVisualAlertLevel; // Update visual alert level immediately

  // Handle browser notifications based on transitions
  if (co2Val >= co2Threshold2.value) { // Currently in T2 range
    if (!co2Alert2Notified.value) {
      sendBrowserNotification("CO2 Critical Alert!", `CO2 level at ${co2Val} ppm (Threshold: ${co2Threshold2.value} ppm).`);
      co2Alert2Notified.value = true;
      co2Alert1Notified.value = true; // If T2 is hit, T1 is also considered "notified"
    }
  } else if (co2Val >= co2Threshold1.value) { // Currently in T1 range (but below T2)
    if (!co2Alert1Notified.value) {
      sendBrowserNotification("CO2 Warning!", `CO2 level at ${co2Val} ppm (Threshold: ${co2Threshold1.value} ppm).`);
      co2Alert1Notified.value = true;
    }
    // If we were previously in T2 and now dropped to T1, reset T2 notification flag
    if (co2Alert2Notified.value) {
      co2Alert2Notified.value = false;
    }
  } else { // CO2 is below T1
    // If CO2 drops below threshold 1, reset both notification flags so they can trigger again
    if (co2Alert1Notified.value) {
      co2Alert1Notified.value = false;
    }
    if (co2Alert2Notified.value) {
      co2Alert2Notified.value = false;
    }
  }
}

// Watch latestCO2 to check alerts (also handles initial load after data fetch)
watch(latestCO2, (newVal) => {
  if (newVal !== 'N/A') {
    checkCO2Alerts(parseFloat(newVal));
  }
}, { immediate: true });

// The onMounted hook for notification permission is already present and correct.
// Sound related logic (alertAudio, playAlertSound, isMuted, toggleMute) has been removed.
</script>

<!-- TEMPLATE AND STYLE SECTIONS BELOW SHOULD REMAIN UNCHANGED FROM THEIR CURRENT STATE -->
<!-- (The following is just a placeholder to satisfy the tool format, -->
<!--  the actual template and style from your file should be kept) -->
<template>
  <div id="app-container">
    <header>
      <div class="header-top-row">
        <h1>Sensor Dashboard</h1>
        <button @click="toggleTheme" class="theme-toggle-button">
          Switch to {{ currentTheme.value === 'light' ? 'Dark' : 'Light' }} Theme
        </button>
      </div>
      <div class="time-filters">
        <button @click="loadLiveData" :class="{ active: activeFilter === 'Live' }">Live</button>
        <button @click="fetchHistoricalData(1)" :class="{ active: activeFilter === '1 Day(s)' }">1 Day</button>
        <button @click="fetchHistoricalData(3)" :class="{ active: activeFilter === '3 Day(s)' }">3 Days</button>
        <button @click="fetchHistoricalData(7)" :class="{ active: activeFilter === '7 Day(s)' }">7 Days</button>
        <button @click="fetchHistoricalData(14)" :class="{ active: activeFilter === '14 Day(s)' }">2 Weeks</button>
        <button @click="fetchHistoricalData(30)" :class="{ active: activeFilter === '30 Day(s)' }">1 Month</button>
      </div>
      <div class="alert-settings">
        <label for="thresh1">CO2 Alert 1 (ppm):</label>
        <input type="number" id="thresh1" v-model.number="co2Threshold1" />
        <label for="thresh2">CO2 Alert 2 (ppm):</label>
        <input type="number" id="thresh2" v-model.number="co2Threshold2" />
        <!-- Mute button and its related logic were removed -->
      </div>
      <p class="status-message">{{ statusMessage }}</p>
      <p v-if="co2AlertLevel > 0" :class="co2DisplayClass" class="co2-alert-message">
        CO2 Level Alert: {{ co2AlertLevel === 1 ? `Warning (>${co2Threshold1} ppm)` : `Critical (>${co2Threshold2} ppm)` }}
      </p>
    </header>
    
    <main class="charts-container">
      <div class="chart-wrapper">
        <TemperatureChart
          :chartData="temperatureData"
          :currentValue="latestTemperature"
          :stats="temperatureStats"
          :dataZoomResetKey="dataZoomResetKey"
          @stats-recalculate="handleTemperatureStatsRecalculate" />
      </div>
      <div class="chart-wrapper">
        <CO2Chart
          :chartData="co2Data"
          :currentValue="latestCO2"
          :stats="co2Stats"
          :dataZoomResetKey="dataZoomResetKey"
          @stats-recalculate="handleCO2StatsRecalculate" />
      </div>
    </main>
  </div>
</template>

<style scoped>
#app-container {
  font-family: Avenir, Helvetica, Arial, sans-serif;
  -webkit-font-smoothing: antialiased;
  -moz-osx-font-smoothing: grayscale;
  text-align: center;
  color: #2c3e50;
  width: 100%;
  height: 100%;
  display: flex;
  flex-direction: column;
  box-sizing: border-box; /* Include padding and border in the element's total width and height */
}

header {
  margin-bottom: 10px;
  padding: 10px;
  border-bottom: 1px solid #eee; /* This will be overridden by dark theme styles if active */
  flex-shrink: 0; /* Prevent header from shrinking */
}

.header-top-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 10px;
}

.theme-toggle-button {
  padding: 6px 12px;
  font-size: 0.9em;
  background-color: #6c757d; /* A neutral grey */
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  transition: background-color 0.2s ease-in-out, color 0.2s ease-in-out;
}

.theme-toggle-button:hover {
  background-color: #5a6268;
}

/* Specific dark theme style for the toggle button is handled by global styles in style.css */
/* body.dark-theme .theme-toggle-button { ... } */


header h1 {
  margin: 0 0 5px 0;
  font-size: 1.5em;
}

.alert-settings {
  margin-top: 10px;
  margin-bottom: 10px;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 10px;
  flex-wrap: wrap;
}
.alert-settings label {
  margin-right: 5px;
  /* Ensure label color matches theme text color by inheriting */
  /* color: inherit; - This is implicitly handled by body styles now */
}
.alert-settings input[type="number"] {
  width: 80px;
  padding: 6px 8px; /* Adjusted padding for better appearance */
  border-radius: 4px;
  border: 1px solid #ced4da; /* Standard Bootstrap-like light theme border */
  background-color: #ffffff; /* Standard light theme input background */
  color: #495057; /* Standard light theme input text color */
  box-sizing: border-box; /* Ensure padding and border are included in the element's total width and height */
  transition: border-color 0.15s ease-in-out, box-shadow 0.15s ease-in-out;
}
.alert-settings input[type="number"]:focus {
  border-color: #80bdff; /* Blue focus border, common in light themes */
  outline: 0;
  box-shadow: 0 0 0 0.2rem rgba(0, 123, 255, 0.25); /* Blue focus shadow */
}
/* Styles for .mute-button and its dark theme variant are removed as the button is no longer present */


.co2-alert-message {
  padding: 5px;
  margin-top: 5px;
  border-radius: 4px;
  font-weight: bold;
}
.alert-level-1 {
  background-color: #fff3cd; /* Light yellow for warning */
  color: #856404;
  border: 1px solid #ffeeba;
}
.alert-level-2 {
  background-color: #f8d7da; /* Light red for critical */
  color: #721c24;
  border: 1px solid #f5c6cb;
}
body.dark-theme .alert-level-1 {
  background-color: #66512c;
  color: #ffeca9;
  border-color: #8c734b;
}
body.dark-theme .alert-level-2 {
  background-color: #5c2d30;
  color: #f5c6cb;
  border-color: #7c3f43;
}


.time-filters {
  margin-bottom: 10px;
}

.time-filters button {
  margin: 0 5px;
  padding: 8px 15px; /* Increased padding for better appearance */
  border: 1px solid #007bff; /* Blue border */
  background-color: #f8f9fa; /* Light grey background */
  color: #007bff; /* Blue text */
  cursor: pointer;
  border-radius: 5px; /* Slightly more rounded corners */
  font-weight: 500; /* Medium font weight */
  transition: background-color 0.2s ease-in-out, color 0.2s ease-in-out; /* Smooth transition */
}

.time-filters button:hover {
  background-color: #e9ecef; /* Slightly darker grey on hover */
  color: #0056b3; /* Darker blue text on hover */
}

.time-filters button.active {
  background-color: #007bff; /* Blue background for active button */
  color: #ffffff; /* White text for active button */
  border-color: #0056b3; /* Darker blue border for active button */
}

.status-message {
  font-size: 0.8em;
  color: #555;
  min-height: 1.1em;
}

.charts-container {
  display: flex;
  flex-direction: column; /* Charts always stacked vertically */
  gap: 10px;
  flex-grow: 1; /* Takes up all remaining space */
  overflow: hidden; /* Prevent content from overflowing */
  padding: 10px; /* Small padding for the charts container */
  box-sizing: border-box;
}

.chart-wrapper {
  border: 1px solid #ddd;
  border-radius: 8px;
  padding: 10px;
  box-shadow: 0 2px 4px rgba(0,0,0,0.1);
  background-color: #fff;
  display: flex; /* Allow chart inside to stretch */
  flex-direction: column; /* Allow chart inside to stretch */
  flex-grow: 1; /* Each wrapper tries to take available space */
  min-height: 0; /* For correct flex-grow behavior in some cases */
  width: 100%; /* Explicitly set width to 100% for vertical layout */
  box-sizing: border-box;
}

/* Media query for row layout removed, charts are always stacked. */
/* Max-width for .charts-container also removed to use full width. */
</style>
