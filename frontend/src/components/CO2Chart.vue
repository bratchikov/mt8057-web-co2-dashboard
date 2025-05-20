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
  stats: { // Prop for aggregate statistics
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
    text: `CO2 Levels: ${props.currentValue} ppm`, // Dynamic title
    left: 'center'
  },
  tooltip: {
    trigger: 'axis', // Tooltip trigger type for axisPointer link
    axisPointer: {
      type: 'line', // Other options: 'shadow', 'cross'
      link: [{ xAxisIndex: 'all' }] // Links this axisPointer to others in the same group by x-axis
    }
    // Formatter can be kept if custom tooltip content is needed
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
      formatter: '{value} ppm'
    }
  },
  legend: {
    data: ['CO2'],
    bottom: 0 // Position legend at the bottom
  },
  series: [
    {
      name: 'CO2',
      type: 'line',
      smooth: true,
      data: props.chartData.values,
      itemStyle: {
        color: '#1E90FF' // DodgerBlue color for CO2
      },
      lineStyle: {
        width: 2
      },
      showSymbol: false, // Hide points by default
      emphasis: { // Show point on hover
        focus: 'series',
        itemStyle: {
          borderWidth: 2,
          borderColor: '#1E90FF',
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

// Watch for data changes
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
          name: 'CO2',
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
    }, false);
  }
}, { deep: true });

watch(() => props.dataZoomResetKey, (newVal, oldVal) => {
  if (newVal !== oldVal) {
    dataZoomStartPercent.value = 0;
    dataZoomEndPercent.value = 100;
    // Optional: Force chart update if needed, similar to TemperatureChart
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
  if (typeof params.start === 'number' && typeof params.end === 'number') {
    dataZoomStartPercent.value = params.start;
    dataZoomEndPercent.value = params.end;

    // Recalculate stats based on the visible range
    if (chartRef.value && chartRef.value.chart) {
      const chartInstance = chartRef.value.chart;
      const currentOption = chartInstance.getOption();
      let visibleValues = [];

      if (currentOption && currentOption.dataZoom && currentOption.dataZoom.length > 0) {
        const dzInfo = currentOption.dataZoom[0];
        
        if (typeof dzInfo.startValue === 'number' && typeof dzInfo.endValue === 'number') {
          visibleValues = props.chartData.values.slice(dzInfo.startValue, dzInfo.endValue + 1);
        } else {
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
        visibleValues = [...props.chartData.values];
      }
      emit('stats-recalculate', visibleValues);
    }
  }
};

onMounted(() => {
  setTimeout(() => {
    if (chartRef.value && chartRef.value.chart) {
      const chartInstance = chartRef.value.chart;
      chartInstance.on('datazoom', handleDataZoom);

      const initialOpt = chartInstance.getOption();
      if (initialOpt && initialOpt.dataZoom && initialOpt.dataZoom.length > 0) {
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
      // console.warn("CO2Chart: Chart instance not available on mount for datazoom listener.");
    }
  }, 100);
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
      <span>Min: {{ props.stats.min }} ppm</span>
      <span>Max: {{ props.stats.max }} ppm</span>
      <span>Avg: {{ props.stats.avg }} ppm</span>
      <span>Median: {{ props.stats.median }} ppm</span>
    </div>
    <v-chart class="chart" :option="option" autoresize :group="'sensorChartsGroup'" ref="chartRef" />
  </div>
</template>

<style scoped>
.chart-container {
  width: 100%;
  height: 100%;
  display: flex; /* Added to allow chart-stats and chart to be laid out */
  flex-direction: column; /* Stack stats above chart */
}
.chart-stats {
  padding: 5px 10px;
  font-size: 0.9em;
  color: #555;
  background-color: #f9f9f9;
  border-bottom: 1px solid #eee;
  text-align: center;
  display: flex;
  justify-content: space-around; /* Distribute space between stat items */
  flex-wrap: wrap; /* Allow wrapping if not enough space */
}
.chart-stats span {
  margin: 0 5px; /* Add some spacing between stat items */
}
.chart {
  width: 100%;
  flex-grow: 1; /* Allow chart to take remaining space */
  /* height: 100%; Removed as flex-grow will manage height */
}
</style>