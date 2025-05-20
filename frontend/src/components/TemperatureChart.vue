<script setup>
import { computed, watch, ref, onMounted, onUnmounted } from 'vue';
import { use } from 'echarts/core'; // Removed getInstanceByDom as it's not used
import { CanvasRenderer } from 'echarts/renderers';
import { LineChart } from 'echarts/charts';
import {
  TitleComponent,
  TooltipComponent,
  GridComponent,
  LegendComponent,
  DataZoomComponent
} from 'echarts/components';
import VChart from 'vue-echarts';

use([
  CanvasRenderer,
  LineChart,
  TitleComponent,
  TooltipComponent,
  GridComponent,
  LegendComponent,
  DataZoomComponent
]);

const props = defineProps({
  chartData: {
    type: Object,
    required: true,
    default: () => ({ timestamps: [], values: [] })
  },
  currentValue: { // Prop for the current value
    type: [String, Number],
    default: 'N/A'
  },
  stats: { // Prop for aggregated statistics
    type: Object,
    default: () => ({ min: 'N/A', max: 'N/A', avg: 'N/A', median: 'N/A' })
  },
  dataZoomResetKey: { // Key to trigger dataZoom reset
    type: Number,
    default: 0
  }
});

const emit = defineEmits(['stats-recalculate']);

const option = computed(() => ({
  title: {
    text: `Temperature: ${props.currentValue}°C`, // Dynamic title
    left: 'center',
    // Optionally, add subtext for stats, or display them elsewhere
    // subtext: `Min: ${props.stats.min}°C, Max: ${props.stats.max}°C, Avg: ${props.stats.avg}°C, Median: ${props.stats.median}°C`,
    // subtextStyle: {
    //   fontSize: 12
    // }
  },
  tooltip: {
    trigger: 'axis', // Tooltip trigger type for axisPointer link
    axisPointer: {
      type: 'line', // Other options: 'shadow', 'cross'
      link: [{ xAxisIndex: 'all' }] // Links this axisPointer to others in the same group by x-axis
    }
    // Formatter can be kept if custom tooltip content is needed beyond synchronized line
  },
  grid: {
    left: '3%',
    right: '4%',
    bottom: '10%', // Increased bottom margin for DataZoom
    containLabel: true
  },
  xAxis: {
    type: 'category',
    boundaryGap: false,
    data: props.chartData.timestamps
  },
  yAxis: {
    type: 'value',
    axisLabel: {
      formatter: '{value} °C'
    }
  },
  legend: {
    data: ['Temperature'],
    bottom: 0 // Position legend at the bottom
  },
  series: [
    {
      name: 'Temperature',
      type: 'line',
      smooth: true,
      data: props.chartData.values,
      itemStyle: {
        color: '#FF6347' // Tomato color for temperature
      },
      lineStyle: {
        width: 2
      },
      showSymbol: false, // Hide points by default for cleaner look
      emphasis: { // Show point on hover
        focus: 'series',
        itemStyle: {
          borderWidth: 2,
          borderColor: '#FF6347',
        }
      }
    }
  ],
  dataZoom: [
    {
      type: 'slider',
      xAxisIndex: 0,
      start: dataZoomStartPercent.value, // Use ref value
      end: dataZoomEndPercent.value,   // Use ref value
      bottom: '0%',
      height: 20
    },
    {
      type: 'inside',
      xAxisIndex: 0,
      start: dataZoomStartPercent.value, // Use ref value
      end: dataZoomEndPercent.value    // Use ref value
    }
  ],
}));

// Watch for data changes to ensure chart updates
watch(() => props.chartData, (newData) => {
  if (chartRef.value && chartRef.value.chart) {
    const chartInstance = chartRef.value.chart;
    // Preserve the existing tooltip configuration from the computed option
    const currentTooltipConfig = option.value.tooltip;

    chartInstance.setOption({
      xAxis: {
        data: newData.timestamps
      },
      series: [
        {
          name: 'Temperature',
          data: newData.values
        }
      ],
      tooltip: currentTooltipConfig, // Re-apply tooltip configuration
      dataZoom: [ // Explicitly set dataZoom with stored percentages
        {
          type: 'slider',
          xAxisIndex: 0,
          start: dataZoomStartPercent.value,
          end: dataZoomEndPercent.value,
          bottom: '0%',
          height: 20
        },
        {
          type: 'inside',
          xAxisIndex: 0,
          start: dataZoomStartPercent.value,
          end: dataZoomEndPercent.value
        }
      ]
    }, false); // notMerge = false to ensure other options are not inadvertently reset
  }
}, { deep: true });

watch(() => props.dataZoomResetKey, (newVal, oldVal) => {
  if (newVal !== oldVal) { // Check if the key actually changed
    dataZoomStartPercent.value = 0;
    dataZoomEndPercent.value = 100;
    // Optionally, force chart update if computed option doesn't trigger it automatically
    // This might be needed if the chart doesn't re-render just from dataZoom refs changing
    // if (chartRef.value && chartRef.value.chart) {
    //   chartRef.value.chart.setOption({
    //     dataZoom: [
    //       { type: 'slider', start: 0, end: 100 },
    //       { type: 'inside', start: 0, end: 100 }
    //     ]
    //   });
    // }
  }
});

const chartRef = ref(null);
const dataZoomStartPercent = ref(0);
const dataZoomEndPercent = ref(100);

const handleDataZoom = (params) => {
  // params directly contains start and end for the datazoom event
  if (typeof params.start === 'number' && typeof params.end === 'number') {
    dataZoomStartPercent.value = params.start;
    dataZoomEndPercent.value = params.end;

    // Recalculate stats based on the visible range
    if (chartRef.value && chartRef.value.chart) {
      const chartInstance = chartRef.value.chart;
      const currentOption = chartInstance.getOption();
      let visibleValues = [];

      if (currentOption && currentOption.dataZoom && currentOption.dataZoom.length > 0) {
        // dataZoom component can provide startValue and endValue which are data indices
        const dzInfo = currentOption.dataZoom[0]; // Assuming slider is the first one
        
        // Check if startValue and endValue are available and are numbers (indices)
        if (typeof dzInfo.startValue === 'number' && typeof dzInfo.endValue === 'number') {
          visibleValues = props.chartData.values.slice(dzInfo.startValue, dzInfo.endValue + 1);
        } else {
          // Fallback to percentage calculation if direct indices are not available
          const totalPoints = props.chartData.values.length;
          if (totalPoints > 0) {
            const startIndex = Math.floor(totalPoints * (dzInfo.start / 100));
            const endIndex = Math.ceil(totalPoints * (dzInfo.end / 100));
            visibleValues = props.chartData.values.slice(startIndex, endIndex);
          } else {
            visibleValues = [];
          }
        }
      } else {
         // If no dataZoom info, consider all data visible (should not happen if dataZoom is always on)
        visibleValues = [...props.chartData.values];
      }
      emit('stats-recalculate', visibleValues);
    }
  }
};

onMounted(() => {
  // Use a timeout to ensure the chart instance is available
  // vue-echarts might initialize the chart slightly after the component is mounted
  setTimeout(() => {
    if (chartRef.value && chartRef.value.chart) {
      const chartInstance = chartRef.value.chart;
      chartInstance.on('datazoom', handleDataZoom);

      // Initialize with current dataZoom state if chart is already rendered
      const initialOpt = chartInstance.getOption();
      if (initialOpt && initialOpt.dataZoom && initialOpt.dataZoom.length > 0) {
        // Assuming the first dataZoom component (slider) dictates the overall state
        const primaryDataZoomState = initialOpt.dataZoom[0];
        if (typeof primaryDataZoomState.start === 'number' && typeof primaryDataZoomState.end === 'number') {
          dataZoomStartPercent.value = primaryDataZoomState.start;
          dataZoomEndPercent.value = primaryDataZoomState.end;
          // Emit initial stats based on the full dataset or initial zoom
          emit('stats-recalculate', props.chartData.values.slice(
            Math.floor(props.chartData.values.length * primaryDataZoomState.start / 100),
            Math.ceil(props.chartData.values.length * primaryDataZoomState.end / 100)
          ));
        } else {
           emit('stats-recalculate', [...props.chartData.values]); // Full data if no zoom
        }
      } else {
         emit('stats-recalculate', [...props.chartData.values]); // Full data if no zoom state
      }
    } else {
      // console.warn("TemperatureChart: Chart instance not available on mount for datazoom listener.");
    }
  }, 100); // Small delay, adjust if necessary
});

onUnmounted(() => {
  if (chartRef.value && chartRef.value.chart) {
    const chartInstance = chartRef.value.chart;
    chartInstance.off('datazoom', handleDataZoom);
  }
});
</script>

<template>
  <div class="chart-container">
    <div class="chart-stats">
      <span>Min: {{ props.stats.min }}°C</span>
      <span>Max: {{ props.stats.max }}°C</span>
      <span>Avg: {{ props.stats.avg }}°C</span>
      <span>Median: {{ props.stats.median }}°C</span>
    </div>
    <v-chart class="chart" :option="option" autoresize :group="'sensorChartsGroup'" ref="chartRef" />
  </div>
</template>

<style scoped>
.chart-container {
  width: 100%;
  height: 100%;
  display: flex; /* Added to manage stats and chart layout */
  flex-direction: column; /* Stack stats above chart */
}
.chart-stats {
  padding: 5px 0;
  font-size: 0.8em;
  color: #333;
  display: flex;
  justify-content: space-around;
  flex-wrap: wrap; /* Allow wrapping on small screens */
  border-bottom: 1px solid #eee; /* Optional separator */
  margin-bottom: 5px; /* Optional space */
}
.chart-stats span {
  margin: 0 5px;
}
.chart {
  width: 100%;
  flex-grow: 1; /* Chart takes remaining space */
  min-height: 0; /* Important for flex-grow in a flex column */
}
</style>