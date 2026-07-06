<template>
    <header class="flex items-center gap-2 flex-nowrap whitespace-nowrap">
        <img :src="versionLogo" :style="logoStyle" class="h-[50px] shrink-0 drop-shadow-[0_3px_8px_rgba(0,0,0,0.4)]">
        <div class="flex-1"></div>
        <img :src="typeBadge" class="h-9 drop-shadow-[0_3px_8px_rgba(0,0,0,0.4)]">
        <span v-if="isNew" class="new-chip">NEW</span>
        <div v-if="bpm != null" class="bpm-pill">
            <svg class="w-[18px] h-[18px] translate-y-[1px]" viewBox="0 0 24 24" fill="none">
                <path d="M9.4 3h5.2c.5 0 .9.33 1 .8l3.1 14.6c.13.62-.34 1.2-1 1.2H6.3c-.66 0-1.13-.58-1-1.2L8.4 3.8c.1-.47.5-.8 1-.8z" stroke="currentColor" stroke-width="1.9" stroke-linejoin="round"/>
                <path d="M12 15.2 17.6 5.6" stroke="currentColor" stroke-width="1.9" stroke-linecap="round"/>
                <circle cx="12" cy="15.6" r="1.5" fill="currentColor"/>
            </svg>
            <span class="bpm-num tabular-nums">{{ bpm }}</span>
        </div>
        <span v-if="genre" class="meta-chip">{{ genreDisplayOf(genre) }}</span>
        <div class="id-pill tabular-nums">ID {{ songId }}</div>
    </header>
</template>

<script setup lang="ts">
import {computed} from 'vue'
import {VERSION_CODE, LOGO_BBOX_LEFT, versionLogoSrc, typeBadgeSrc, genreDisplayOf} from '@/components/maimai/utils/song_card'

const props = defineProps<{
    from?: string; type?: string; songId: number | string
    bpm?: number; genre?: string; isNew?: boolean
}>()

const versionLogo = computed(() => versionLogoSrc(props.from))
const typeBadge   = computed(() => typeBadgeSrc(props.type))

// 版本 logo 视觉左对齐：素材左侧透明留白按显示倍率补偿
const logoStyle = computed(() => {
    const code = VERSION_CODE[props.from ?? '']
    const trim = code ? (LOGO_BBOX_LEFT[code] ?? 0) * (60 / 160) : 0
    return {marginLeft: `${(-trim).toFixed(1)}px`}
})
</script>

<style scoped lang="postcss" src="@/assets/css/maimai/song_card.pcss"/>

<style scoped lang="postcss">
.id-pill, .bpm-pill { font-family: 'Torus', sans-serif; color: #fff; background: var(--pill-bg); border-radius: 9999px; box-shadow: var(--pill-shadow); }
.id-pill { font-weight: bold; font-size: 16px; letter-spacing: 0.06em; padding: 3px 12px; }
.bpm-pill { display: inline-flex; align-items: center; gap: 6px; padding: 3px 12px 3px 10px; }
.bpm-num { font-weight: bold; font-size: 16px; }
.meta-chip { font-family: 'SEGA NewRodin','LXGW WenKai',sans-serif; font-weight: bold; font-size: 14px; color: var(--chip-ink); padding: 4px 12px; border-radius: 9999px; background: var(--chip-bg); box-shadow: var(--chip-ring); white-space: nowrap; }
.new-chip { font-family: 'Torus',sans-serif; font-weight: bold; font-size: 12px; letter-spacing: 0.18em; padding: 4px 11px 3px 13px; border-radius: 9999px; background: var(--new-chip-bg); box-shadow: var(--new-chip-shadow); }
.tabular-nums { font-variant-numeric: tabular-nums; }
</style>
