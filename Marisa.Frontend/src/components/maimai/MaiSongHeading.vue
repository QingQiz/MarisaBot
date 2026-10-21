<template>
    <div class="flex-1 min-w-0 pb-1">
        <h1 ref="titleEl" class="mai-title" :style="{ fontSize: size + 'px' }">{{ title }}</h1>
        <div class="flex items-baseline justify-between gap-4" :style="{ marginTop: rowTop + 'px' }">
            <div class="artist-line" :style="{ fontSize: artistSize + 'px', marginTop: artistTop + 'px' }"><slot name="sub">{{ artist }}</slot></div>
            <div v-if="player" class="player-inline shrink-0"><span class="player-label">PLAYER</span> {{ player }}</div>
        </div>
    </div>
</template>

<script setup lang="ts">
import {nextTick, onBeforeUnmount, onMounted, ref, watch} from 'vue'

const props = withDefaults(defineProps<{
    title: string; artist: string; max: number; min: number
    player?: string; rowTop?: number; artistSize?: number; artistTop?: number
}>(), {player: '', rowTop: 10, artistSize: 19, artistTop: 10})

// 字体就绪后按实际可用宽度缩字；标题或容器宽度变化时重新从最大字号测量。
const titleEl = ref<HTMLElement | null>(null)
const size = ref(props.max)
let fitVersion = 0
let resizeObserver: ResizeObserver | undefined

async function fit() {
    const version = ++fitVersion
    size.value = props.max
    await nextTick()
    const el = titleEl.value
    if (!el) return
    try {
        const style = getComputedStyle(el)
        await document.fonts.load(`${style.fontWeight} ${props.max}px ${style.fontFamily}`, props.title)
        await document.fonts.ready
    } catch { /* 字体加载失败时仍按实际回退字体测宽。 */ }
    if (version !== fitVersion || !el.isConnected || el.clientWidth === 0) return
    while (el.scrollWidth > el.clientWidth && size.value > props.min) {
        const next = Math.floor(size.value * el.clientWidth / el.scrollWidth) - 1
        size.value = Math.max(props.min, Math.min(size.value - 1, next))
        await nextTick()
        if (version !== fitVersion) return
    }
}

onMounted(() => {
    fit()
    const container = titleEl.value?.parentElement
    if (!container) return
    let width = container.clientWidth
    resizeObserver = new ResizeObserver(() => {
        if (container.clientWidth === width) return
        width = container.clientWidth
        fit()
    })
    resizeObserver.observe(container)
})
onBeforeUnmount(() => {
    fitVersion++
    resizeObserver?.disconnect()
})
watch(() => [props.title, props.max, props.min], fit, {flush: 'post'})
</script>

<style scoped lang="postcss" src="@/assets/css/maimai/song_card.pcss"/>

<style scoped lang="postcss">
.artist-line { flex: 1; min-width: 0; font-family: 'Torus','SEGA Maru Gothic','LXGW WenKai',sans-serif; font-weight: bold; color: rgba(255,255,255,0.8); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.player-inline { font-family: 'Torus','Microsoft YaHei',sans-serif; font-weight: bold; font-size: 21px; color: #fff; text-shadow: 0 2px 4px rgba(0,0,0,0.5); white-space: nowrap; }
.player-label { font-family: 'Torus',sans-serif; font-weight: bold; font-size: 12px; letter-spacing: 0.18em; color: rgba(255,255,255,0.45); }
</style>
