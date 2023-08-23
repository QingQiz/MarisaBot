<template>
    <div class="relative w-[400px] h-[200px] ">
        <!-- cover -->
        <fallback-image
            ref="imgEl" @load="UpdateColor"
            :src="available_cover" fallback="/assets/maimai/cover/0.png"
            class="inline w-[200px] h-[200px] rounded-r-3xl ml-[200px]"/>
        <div class="absolute top-0 left-0 right-0 w-[400px] h-[200px] rounded-l-3xl"
             :style="['background-image:' + bg_color]">
            <div class="font-osu-web mt-4 ml-5 relative flex flex-col gap-[2px]" :style="['color:' + text_color]">
                <div class="text-3xl flex w-fit justify-center items-center"
                     :style="`background-color: ${level_color}`">
                    <div class="text-center bg-gray-400 text-black font-bold w-[65px] m-[1.5px] px-[5px]">
                        {{ score.ds.toFixed(1) }}
                    </div>
                    <div class="text-center w-[65px] text-white pl-[2.5px] px-[5px] font-bold">{{ score.ra }}</div>
                </div>
                <!-- title -->
                <div class="tracking-wider pb-2 text-4xl w-400 whitespace-nowrap overflow-hidden overflow-ellipsis">
                    {{ score.title }}
                </div>
                <!-- 私の中の幻想的世界観及びその顕現を想起させたある現実での出来事に関する一考察-->
                <!-- achievement -->
                <div class="tracking-wide flex items-end -mt-4">
                    <span class="text-6xl font-bold">
                        {{ score.achievements < 100 ? '0' : '' }}{{ score.achievements.toString().split('.')[0] }}.
                    </span>
                    <span class="text-4xl mb-[2px]">
                        {{ score.achievements.toFixed(4).split('.')[1] }}
                    </span>
                </div>
                <!-- footer -->
                <div class="h-fit flex gap-1.5 items-center mt-1">
                    <img :src="`/assets/maimai/pic/type_${score.type.toLowerCase()}.png`" alt class="h-[35px]">
                    <img :src="`/assets/maimai/pic/rank_${score.rate}.png`" alt class="inline-block h-[35px]">
                    <img :src="`/assets/maimai/pic/icon_${score.fc === '' ? 'blank' : score.fc}.png`" alt
                         class="h-[35px]">
                    <img :src="`/assets/maimai/pic/icon_${score.fs === '' ? 'blank' : score.fs}.png`" alt
                         class="h-[35px]">
                </div>
            </div>
        </div>
    </div>
</template>

<script setup lang="ts">

import {computed, ref} from "vue";
import FallbackImage from "@/components/utils/FallbackImage.vue";
import {maimai_alternativeCover, maimai_levelColors} from "@/GlobalVars";
import {Score as MaiScore} from "@/components/maimai/BestScores.vue";

const props = defineProps<{ score: MaiScore }>();
const imgEl = ref<typeof FallbackImage | null>(null);

let img_color = ref({r: -1, g: -1, b: -1})

let level_color = computed(() => maimai_levelColors[props.score.level_index ?? 0])

let text_color = computed(() => {
    let {r, g, b} = img_color.value
    if (r * 0.299 + g * 0.587 + b * 0.114 > 186) {
        return '#000000'
    } else {
        return '#ffffff';

    }
});

let bg_color = computed(() => {
    return `linear-gradient(to right, ${MakeRgba(img_color.value, 1)} 50%, ${MakeRgba(img_color.value, 0)})`
})

let available_cover = computed(() => maimai_alternativeCover(props.score.song_id ?? 0));

function UpdateColor() {
    img_color.value = imgEl.value?.GetAverageRGB(0, 0, 12, 200)
}

function MakeRgba(rgb: { r: number, g: number, b: number }, a ?: number) {
    if (a == undefined) {
        return `rgb(${rgb.r},${rgb.g},${rgb.b})`
    }
    return `rgba(${rgb.r},${rgb.g},${rgb.b},${a})`
}

</script>