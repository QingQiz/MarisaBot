<script setup lang="ts">
import {Score as ChuScore} from "@/components/chunithm/utils/best_t";
import FallbackImage from "@/components/utils/FallbackImage.vue";
import {computed, ref} from "vue";
import {chunithm_levelColors} from "@/GlobalVars";
import {GetContrastingTextColor, MakeRgba} from "@/utils/color";
import {ToFixedNoRound} from "@/utils/str";
import {rankIcon, normalizeCombo, chainOfFc} from "@/components/chunithm/utils/score";
import ScoreMarks from "@/components/chunithm/partial/ScoreMarks.vue";

let props = defineProps<{ score: ChuScore }>();

// b30/b50 卡片拿到的是原始 fc，借 chu vs 的 versus_* 素材族渲染标记
// （icon_* 素材里没有 alljusticecritical，AJC 会掉到空白兜底图）
const combo = computed(() => normalizeCombo(props.score.fc));
const chain = computed(() => chainOfFc(props.score.fc));

const imgEl = ref<typeof FallbackImage | null>(null);

let img_color = ref({r: -1, g: -1, b: -1})
let bg_color  = computed(() => {
    return `linear-gradient(to right, ${MakeRgba(img_color.value, 1)} 50%, ${MakeRgba(img_color.value, 0)})`
})

let text_color = computed(() => {
    return GetContrastingTextColor(img_color.value);
});

let level_color = computed(() => chunithm_levelColors[props.score.level_index ?? 0])

function UpdateColor() {
    img_color.value = imgEl.value?.GetAverageRGB(0, 0, 12, 200)
}
</script>

<template>
    <div class="w-[400px] h-[200px] relative">
        <fallback-image
            ref="imgEl" @load="UpdateColor"
            :src="[`${props.score.mid}.png`]" fallback="0.png"
            :prefix="'/assets/chunithm/cover/'"
            class="inline w-1/2 h-full rounded-r-3xl absolute left-1/2"/>
        <div class="absolute w-full h-full rounded-l-3xl" :style="['background-image:' + bg_color]">
            <div class="font-osu-web mt-4 ml-5 relative flex flex-col gap-[2px]" :style="['color:' + text_color]">
                <div class="text-3xl flex w-fit justify-center items-center"
                     :style="`background-color: ${level_color}`">
                    <div class="text-center bg-gray-400 text-black font-bold w-[75px] m-[1.5px] px-[5px]">
                        {{ ToFixedNoRound(score.ds, 1) }}
                    </div>
                    <div class="text-center w-[75px] text-white pl-[2.5px] px-[5px] font-bold">{{ ToFixedNoRound(score.ra, 2) }}</div>
                </div>
                <!-- title -->
                <div class="tracking-wider pb-2 text-4xl w-400 whitespace-nowrap overflow-hidden overflow-ellipsis">
                    {{ score.title }}
                </div>
                <!-- scores -->
                <div class="tracking-wide flex items-end -mt-4">
                    <span class="text-6xl font-bold">
                        {{ score.score.toString().padStart(7, '0').replace(/\B(?=(\d{4})+(?!\d))/g, ',') }}
                    </span>
                </div>
                <!-- footer -->
                <div class="h-fit flex gap-1.5 items-center mt-1">
                    <span class="card-marks">
                        <ScoreMarks family="common" :combo="combo" :chain="chain"/>
                    </span>
                    <img :src="rankIcon(score.Rank)" alt="" class="h-[30px]">
                </div>
            </div>

        </div>
    </div>
</template>

<style scoped lang="postcss">
/* 标记高度对齐右侧 30px 的评级图标：134 / 26 = 155 / 30 */
.card-marks {
    --chu-mark-width: 155px;
    /* 显式给定宽度：ScoreMarks 根元素是 width:100%，不给定宽会与 auto 宽度互相依赖 */
    width: 155px;
    display: flex;
    align-items: center;
    flex-shrink: 0;
}
</style>
