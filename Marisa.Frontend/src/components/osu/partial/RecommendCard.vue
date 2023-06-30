<script setup lang="ts">
import {RecommendData} from "@/components/osu/Osu.Data";
import {computed} from "vue";
import {GetDiffColor, GetDiffTextColor, osu_image_builder, osu_modIcon_builder} from "@/GlobalVars";
import * as d3 from "d3";

const props = defineProps<{ data: RecommendData }>();

const listUrl  = computed(() => props.data.mapCoverUrl.replace('cover.', 'list@2x.'));
const coverUrl = computed(() => props.data.mapCoverUrl);

// layout definition
const cardWidth  = 1000;
const cardHeight = 300;

const lUrlCss = `url('${osu_image_builder(listUrl.value)}')`
const rUrlCss = `url('${osu_image_builder(coverUrl.value)}')`

const lWidthCss  = `${cardHeight}px`
const lHeightCss = `${cardHeight}px`
const rWidthCss  = `${cardWidth - cardHeight}px`
const rHeightCss = `${cardHeight}px`

const radiusVal = 20;
const radius    = `${radiusVal}px`
const radiusN   = `${-radiusVal}px`

const totalWidth  = `${cardWidth + radiusVal}px`
const totalHeight = `${cardHeight}px`

const diffColor     = GetDiffColor(props.data.difficulty)
const diffTextColor = GetDiffTextColor(props.data.difficulty)

// functions
const percentageColorSpectrum = d3.scaleLinear<string>()
    .domain([0, 0.5, 1])
    .range(['#0f0', '#ff0', '#f00'])
    .interpolate(d3.interpolateRgb.gamma(2.2))

function GetPercentageColor(percentage: number) {
    if (isNaN(percentage)) percentage = 0;
    return percentageColorSpectrum(percentage);
}

const ppColorSpectrum = d3.scaleLinear<string>()
    .domain([0, 25, 50])
    .clamp(true)
    .range(['#f00', '#ff0', '#0f0'])
    .interpolate(d3.interpolateRgb.gamma(2.2))

function GetPpColor(pp: number) {
    if (isNaN(pp)) pp = 0;
    return ppColorSpectrum(pp);
}

</script>

<template>
    <div class="card">
        <div class="flex">
            <div class="l">
            </div>
            <div class="r text-shadow font-osu-web text-white flex flex-col place-content-between">
                <div class="text-4xl ellipsis">
                    {{ props.data.mapName }}
                </div>
                <div class="text-3xl">
                    {{ props.data.mapLink }}
                </div>
                <div class="flex items-center gap-3">
                    <div class="osu-star-rating">
                        {{ props.data.difficulty.toFixed(2) }}
                    </div>
                    <img v-for="i in props.data.mod.filter(x => x!== 'NM')"
                         :src="osu_modIcon_builder(i, false)" alt class="h-[30px]">
                </div>
                <div class="flex flex-col gap-1">
                    <div class="osu-table">
                        <div>
                            <div>
                                <slot name="predict-key"></slot>
                            </div>
                            <div>
                                <slot name="predict-val"></slot>
                            </div>
                        </div>
                        <div>
                            <div>Record Breaking</div>
                            <div :style="`color: ${GetPercentageColor(props.data.newRecordPercent)}`">
                                {{ props.data.newRecordPercent.toPercentage() }}
                            </div>
                        </div>
                        <div>
                            <div>BP Probability</div>
                            <div :style="`color: ${GetPercentageColor(props.data.passPercent)}`">
                                {{ props.data.passPercent.toPercentage() }}
                            </div>
                        </div>
                    </div>
                    <div class="osu-table">
                        <div>
                            <div>predict PP</div>
                            <div>{{ props.data.predictPP.toFixed(2) }}</div>
                        </div>
                        <div>
                            <div>PP Increment</div>
                            <div :style="`color: ${GetPpColor(props.data.ppIncrement)}`">
                                {{ props.data.ppIncrement.toFixed(2) }}
                            </div>
                        </div>
                        <div>
                            <div>PP Increment except</div>
                            <div :style="`color: ${GetPpColor(props.data.ppIncrementExpect)}`">
                                {{ props.data.ppIncrementExpect.toFixed(2) }}
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    </div>
</template>

<style scoped>
.osu-table > div > div:nth-child(1) {
    @apply text-base capitalize
}

.osu-table > div > div:nth-child(2) {
    @apply text-2xl;
    text-shadow: none;
}

.card {
    width: v-bind(totalWidth);
    height: v-bind(totalHeight);
}

.l {
    @apply bg-cover bg-center;
    background-image: v-bind(lUrlCss);
    width: v-bind(lWidthCss);
    height: v-bind(lHeightCss);
    border-radius: v-bind(radius) 0 0 v-bind(radius);
    z-index: -30;
}

.r {
    @apply relative;
    width: v-bind(rWidthCss);
    height: v-bind(rHeightCss);
    padding: v-bind(radius) 0;
}

.r:before {
    @apply absolute inset-0 bg-cover bg-center;
    background-image: v-bind(rUrlCss);
    border-radius: 0 v-bind(radius) v-bind(radius) 0;
    content: '';
    z-index: -20;
}

.r:after {
    @apply absolute top-0 bottom-0;
    background-color: hsla(200, 10%, 30%, 95%);
    content: '';
    right: v-bind(radiusN);
    left: v-bind(radiusN);
    border-radius: v-bind(radius);
    z-index: -10;
}

.text-shadow {
    text-shadow: 0 1px 3px rgba(0, 0, 0, .75)
}

.ellipsis {
    white-space: nowrap;
    text-overflow: ellipsis;
    overflow: hidden;
}

.osu-star-rating {
    background-color: v-bind(diffColor);
    color: v-bind(diffTextColor);
}
</style>

