<template>
    <div class="mai-card relative overflow-hidden antialiased" :style="[cardBackground(bgKey), { width: width + 'px' }]">
        <div class="absolute inset-0 pointer-events-none stripe-layer"></div>
        <div class="absolute inset-0 pointer-events-none" :style="glowStyle"></div>
        <div class="relative px-12 pt-9" :class="padBottom">
            <slot/>
        </div>
    </div>
</template>

<script setup lang="ts">
import {computed} from 'vue'
import {cardBackground} from '@/components/maimai/utils/song_card'

const props = withDefaults(defineProps<{bgKey: string; accent: string; padBottom?: string; width?: number}>(), {padBottom: 'pb-10', width: 840})

const glowStyle = computed(() => ({
    background: `radial-gradient(720px 520px at 18% 8%, ${props.accent}2e 0%, transparent 70%),
                 radial-gradient(560px 480px at 92% 4%, ${props.accent}1c 0%, transparent 70%)`,
}))
</script>

<style scoped lang="postcss" src="@/assets/css/maimai/song_card.pcss"/>

<style scoped lang="postcss">
.stripe-layer { background: repeating-linear-gradient(-38deg, rgba(255,255,255,0.025) 0 3px, transparent 3px 26px); }
</style>
