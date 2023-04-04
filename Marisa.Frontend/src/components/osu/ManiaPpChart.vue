<template>
    <div class="flex flex-col">
        <div class="grid-ppAcc">
            <div>pp-acc</div>
            <div></div>
            <div>pp</div>
            <template v-for="d in ppDisplay">
                <div :class="{'current-pp': d[1]}">{{ (d[0] * 100).toFixed(2) }}%</div>
                <div class="bg-[#808080] relative h-[5px]">
                    <div class="absolute left-0 top-0 bottom-0" :style="`background-color: ${d[4]}; width: ${d[3]}%`"/>
                </div>
                <div :class="{'current-pp': d[1]}">{{ (d[2] as number).toFixed(2) }}</div>
            </template>
        </div>
        <div v-if="ppConfig" class="text-center text-xl font-bold">
            <span class="text-[#ff66ab]">pp</span>
            = {{ (ppConfig['ppMax'] as number).toFixed(2) }} × (5 ×
            <span class="text-[#ff66ab]">pp-acc</span>
            - 4) × {{ ppConfig['length'] }} ×
            {{ ppConfig['multiplier'] }}
        </div>
    </div>
</template>

<script lang="ts" setup>

import {computed, ref} from "vue";
import axios from "axios";
import {osu_maniaPpChart_builder, PpAcc} from "@/GlobalVars";

const ppConfig = ref(null);

const props = defineProps<{
    beatmapsetId: number,
    beatmapId: number,
    beatmapChecksum: string,
    mods: string[],

    count_100: number;
    count_300: number;
    count_50: number;
    count_geki: number;
    count_katu: number;
    count_miss: number;
}>();

axios.get(osu_maniaPpChart_builder(props.beatmapsetId, props.beatmapChecksum, props.beatmapId, props.mods, GetTotalHits()))
    .then(data => ppConfig.value = data.data);

const ppDisplay = computed(() => {
    const range = (start: number, stop: number, step: number) =>
        Array.from({length: (stop - start) / step + 1}, (_, i) => start + (i * step))

    let max = CalcPP(1)
    let r   = range(0.9, 1.01, 0.02).map(x => [x, 0])
    r.push([CalcPpAcc(), 1])
    r.sort((a, b) => a[0] - b[0])
    r.reverse()
    return r
        .map(x => [x[0], x[1], CalcPP(x[0])])
        .map(x => [x[0], x[1], x[2], x[2] / max * 100, x[1] === 0 ? '#fc2' : '#ff66ab',])
})

function GetTotalHits() {
    return props.count_100 + props.count_300 + props.count_50 + props.count_geki + props.count_katu + props.count_miss;
}

function CalcPP(acc: number) {
    if (ppConfig.value == null) return NaN;
    return ppConfig.value!['ppMax'] * ppConfig.value!['multiplier'] * (Math.max(5 * acc - 4, 0)) * ppConfig.value!['length']
}

function CalcPpAcc() {
    return PpAcc(props.count_geki, props.count_300, props.count_katu, props.count_100, props.count_50, props.count_miss);
}

</script>

<style scoped>
.grid-ppAcc {
    @apply grid items-center gap-x-5 place-content-center text-center;
    grid-template-columns: auto 1fr auto;
    grid-template-rows: auto;
}

.current-pp {
    @apply font-bold;
    color: #ff66ab;
}
</style>