<template>
    <div class="flex-1 min-w-0 pb-1">
        <h1 ref="titleEl" class="mai-title" :style="{ fontSize: size + 'px' }">{{ title }}</h1>
        <div class="flex items-baseline justify-between gap-4" :style="{ marginTop: rowTop + 'px' }">
            <div class="artist-line" :style="{ fontSize: artistSize + 'px', marginTop: artistTop + 'px' }">{{ artist }}</div>
            <div v-if="player" class="player-inline shrink-0"><span class="player-label">PLAYER</span> {{ player }}</div>
        </div>
    </div>
</template>

<script setup lang="ts">
import {nextTick, onMounted, ref, watch} from 'vue'

const props = withDefaults(defineProps<{
    title: string; artist: string; max: number; min: number
    player?: string; rowTop?: number; artistSize?: number; artistTop?: number
}>(), {player: '', rowTop: 10, artistSize: 19, artistTop: 10})

// 标题真实测宽 auto-shrink（字体就绪后量测，逐字号收敛）
const titleEl = ref<HTMLElement | null>(null)
const size = ref(props.max)

async function fit() {
    size.value = props.max
    await nextTick()
    try { await (document as any).fonts.ready } catch { /* ignore */ }
    const el = titleEl.value
    for (let p = 0; el && p < 5 && el.scrollWidth > el.clientWidth; p++) {
        size.value = Math.max(props.min, Math.floor(size.value * el.clientWidth / el.scrollWidth) - 1)
        await nextTick()
    }
}

onMounted(fit)
watch(() => props.title, fit, {flush: 'post'})
</script>

<style scoped lang="postcss" src="@/assets/css/maimai/song_card.pcss"/>

<style scoped lang="postcss">
.artist-line { flex: 1; min-width: 0; font-family: 'Torus','SEGA Maru Gothic','LXGW WenKai',sans-serif; font-weight: bold; color: rgba(255,255,255,0.8); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.player-inline { font-family: 'Torus','Microsoft YaHei',sans-serif; font-weight: bold; font-size: 21px; color: #fff; text-shadow: 0 2px 4px rgba(0,0,0,0.5); white-space: nowrap; }
.player-label { font-family: 'Torus',sans-serif; font-weight: bold; font-size: 12px; letter-spacing: 0.18em; color: rgba(255,255,255,0.45); }
</style>
