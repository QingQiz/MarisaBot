<template>
    <div class="card-outer relative h-[200px] w-[400px] rounded-3xl overflow-hidden isolate"
         :style="outer_style">
        <div class="card-inner absolute inset-[2px] overflow-hidden bg-white/85 rounded-[22px] mai-glass-fade">
            <!-- cover (right side) -->
            <fallback-image
                ref="imgEl" @load="UpdateColor"
                :src="available_cover" fallback="/assets/maimai/cover/0.png"
                class="absolute right-0 top-0 h-full w-[200px] object-cover"/>

            <!-- cover-color tint: opaque on left, fades into cover on right -->
            <div class="absolute inset-0 z-10 pointer-events-none"
                 :style="`background: linear-gradient(to right, ${MakeRgba(img_color, 1)} 0%, ${MakeRgba(img_color, 1)} 50%, ${MakeRgba(img_color, 0)} 100%)`"></div>

            <!-- level color accent stripe — half-inset rounded pill -->
            <div class="absolute left-[6px] top-[15px] bottom-[15px] w-[6px] z-30 rounded-full"
                 :style="{ background: level_color }"></div>

            <!-- text content -->
            <div class="absolute inset-y-0 left-0 z-30 w-[270px] pl-5 pr-2 py-1.5 flex flex-col justify-between"
                 :style="{ color: text_color }">
                <div class="flex items-center gap-2">
                    <div class="px-3 py-0.5 rounded-md text-3xl font-bold tabular-nums leading-tight"
                         :style="{ background: level_color, color: '#fff', boxShadow: '0 2px 8px rgba(0,0,0,0.3)' }">
                        {{ score.ds.toFixed(1) }}
                    </div>
                    <div class="text-3xl font-extrabold tabular-nums" :style="{ textShadow: text_shadow }">
                        {{ score.ra }}
                    </div>
                </div>

                <div class="text-4xl font-black leading-tight whitespace-nowrap overflow-hidden overflow-ellipsis tracking-tight mai-title ml-[2px] mr-[-72px]"
                     :style="{ textShadow: text_shadow }">
                    {{ score.title }}
                </div>

                <div class="flex items-baseline tabular-nums"
                     :style="{ textShadow: text_shadow, transform: 'translateY(-6px)' }">
                    <span class="text-6xl font-black leading-none">
                        {{ score.achievements < 100 ? '0' : '' }}{{ Math.floor(score.achievements) }}.
                    </span>
                    <span class="text-4xl font-extrabold leading-none">
                        {{ score.achievements.toFixed(4).split('.')[1] }}
                    </span>
                    <span class="text-2xl font-bold opacity-75 ml-0.5 leading-none">%</span>
                </div>

                <div class="flex items-center gap-1.5">
                    <img :src="`/assets/maimai/pic/type_${score.type.toLowerCase()}.png`" alt=""
                         class="h-10 drop-shadow-[0_2px_4px_rgba(0,0,0,0.45)]">
                    <img :src="`/assets/maimai/pic/rank_${score.rate}.png`" alt=""
                         class="h-10 drop-shadow-[0_2px_4px_rgba(0,0,0,0.45)]">
                    <img v-if="score.fc !== ''" :src="`/assets/maimai/pic/icon_${score.fc}.png`" alt=""
                         class="h-10 drop-shadow-[0_2px_4px_rgba(0,0,0,0.45)]">
                    <img v-if="score.fs !== ''" :src="`/assets/maimai/pic/icon_${score.fs}.png`" alt=""
                         class="h-10 drop-shadow-[0_2px_4px_rgba(0,0,0,0.45)]">
                </div>
            </div>
        </div>

        <!-- specular highlight overlay — top edge sheen + diagonal corner hotspot -->
        <div class="absolute inset-0 z-[35] pointer-events-none rounded-3xl mai-glass-shine"></div>

        <!-- rim lighting / inner highlight overlay (above content) -->
        <div class="absolute inset-0 z-40 pointer-events-none rounded-3xl"
             :style="{ boxShadow: rim_inset }"></div>
    </div>
</template>

<script setup lang="ts">
import {computed, ref} from "vue";
import FallbackImage from "@/components/utils/FallbackImage.vue";
import {maimai_alternativeCover, maimai_levelColors} from "@/GlobalVars";
import {Score as MaiScore} from "@/components/maimai/utils/best_t";
import {GetContrastingTextColor, MakeRgba} from "@/utils/color";
import {getDisplacementFilter} from "@/components/maimai/utils/liquidGlass";

const props = defineProps<{ score: MaiScore }>()
const imgEl = ref<typeof FallbackImage | null>(null)

let img_color = ref({r: 240, g: 210, b: 220})

let level_color = computed(() => maimai_levelColors[props.score.level_index ?? 0])
let available_cover = computed(() => maimai_alternativeCover(props.score.song_id ?? 0))

const text_color = computed(() => GetContrastingTextColor(img_color.value))

const text_shadow = computed(() => text_color.value === '#000000'
    ? '0 1px 0 rgba(255,255,255,0.55), 0 0 6px rgba(255,255,255,0.4)'
    : '0 1px 3px rgba(0,0,0,0.55), 0 1px 1px rgba(0,0,0,0.6)')

// Liquid-glass refraction filter (SVG feDisplacementMap with mild chromatic aberration)
const liquid_filter = computed(() => {
    const url = getDisplacementFilter({width: 400, height: 200, radius: 24, depth: 9, strength: 150, chromaticAberration: 14})
    return `url("${url}") saturate(1.2) brightness(1.05)`
})

const outer_style = computed(() => ({
    boxShadow: '0 0 0 1px rgba(255,255,255,0.55), 0 10px 26px -8px rgba(80,30,90,0.45)',
    backdropFilter: liquid_filter.value,
    ['-webkit-backdrop-filter' as string]: liquid_filter.value,
}))

// Inner rim highlight — top + bottom + sides give the iOS-style glass edge lighting
const rim_inset = 'inset 0 1.5px 0 rgba(255,255,255,0.95), inset 0 -1px 0 rgba(255,255,255,0.4), inset 1px 0 0 rgba(255,255,255,0.45), inset -1px 0 0 rgba(255,255,255,0.3)'

function UpdateColor() {
    const c = imgEl.value?.GetAverageRGB(0, 0, 12, 200)
    if (c && (c.r > 0 || c.g > 0 || c.b > 0)) {
        img_color.value = c
    }
}
</script>

<style scoped>
/* feather the inner card's outer edge so it blends softly into the glass rim */
.mai-glass-fade {
    -webkit-mask-image:
        linear-gradient(to right, transparent 0, #000 1.5px, #000 calc(100% - 1.5px), transparent 100%),
        linear-gradient(to bottom, transparent 0, #000 1.5px, #000 calc(100% - 1.5px), transparent 100%);
    -webkit-mask-composite: source-in;
            mask-image:
        linear-gradient(to right, transparent 0, #000 1.5px, #000 calc(100% - 1.5px), transparent 100%),
        linear-gradient(to bottom, transparent 0, #000 1.5px, #000 calc(100% - 1.5px), transparent 100%);
            mask-composite: intersect;
}

/* Song title — SEGA Maru Gothic DB with stroke-thickened weight to match maimai in-game UI */
.mai-title {
    font-family: 'SEGA Maru Gothic', 'Torus', 'Hiragino Kaku Gothic ProN', 'Microsoft YaHei', sans-serif;
    -webkit-font-smoothing: antialiased;
    -moz-osx-font-smoothing: grayscale;
    -webkit-text-stroke: 2.2px currentColor;
    paint-order: stroke fill;
}

/* Specular highlight: top sheen + diagonal corner shine — kept very soft */
.mai-glass-shine {
    background:
        /* top horizontal sheen — subtle, fades fast */
        linear-gradient(180deg, rgba(255,255,255,0.22) 0%, rgba(255,255,255,0.06) 5%, rgba(255,255,255,0) 14%),
        /* diagonal hotspot — gentle light from top-left */
        linear-gradient(135deg, rgba(255,255,255,0.18) 0%, rgba(255,255,255,0.04) 22%, rgba(255,255,255,0) 50%, rgba(255,255,255,0) 78%, rgba(255,255,255,0.05) 100%);
    mix-blend-mode: screen;
}
</style>
