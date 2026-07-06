<template>
    <header class="flex items-center gap-2 flex-nowrap whitespace-nowrap">
        <img :src="versionLogo" :style="logoStyle" class="h-[50px] shrink-0 drop-shadow-[0_3px_8px_rgba(0,0,0,0.4)]">
        <div class="flex-1"></div>
        <img v-if="type" :src="typeBadge" class="h-9 drop-shadow-[0_3px_8px_rgba(0,0,0,0.4)]">
        <slot/>
    </header>
</template>

<script setup lang="ts">
import {computed} from 'vue'
import {VERSION_CODE, LOGO_BBOX_LEFT, versionLogoSrc, typeBadgeSrc} from '@/components/maimai/utils/song_card'

const props = defineProps<{from?: string; type?: string}>()

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
