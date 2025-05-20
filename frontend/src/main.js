import { createApp } from 'vue'
import App from './App.vue'
import './style.css'

// Import ECharts components
import ECharts, { THEME_KEY } from 'vue-echarts'
import { use } from 'echarts/core'

// Import ECharts modules as needed
import {
  CanvasRenderer
} from 'echarts/renderers'
import {
  LineChart,
  BarChart // Example: if you need Bar chart later
} from 'echarts/charts'
import {
  GridComponent,
  TooltipComponent,
  LegendComponent,
  TitleComponent, // For chart titles
  DataZoomComponent // For zooming
} from 'echarts/components'

use([
  CanvasRenderer,
  LineChart,
  BarChart,
  GridComponent,
  TooltipComponent,
  LegendComponent,
  TitleComponent,
  DataZoomComponent
])

const app = createApp(App)

// Register ECharts component globally
app.component('v-chart', ECharts)

// Optional: Provide a theme for ECharts
// app.provide(THEME_KEY, 'dark'); // Example for dark theme

app.mount('#app')
