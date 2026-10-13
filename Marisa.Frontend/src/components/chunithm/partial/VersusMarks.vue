<template>
    <span v-if="comboSrc || chainSrc || hasClear(clear)" class="chu-marks" :class="{'common-marks': family === 'common'}">
        <span v-if="comboSrc" class="mark-slot" data-mark-slot="combo">
            <img class="native-mark" :src="comboSrc" :alt="comboTitle" :title="comboTitle">
        </span>
        <span v-if="chainSrc" class="mark-slot" data-mark-slot="chain">
            <img class="native-mark" :src="chainSrc" :alt="chain === 'fullchain2' ? 'FULL CHAIN（铂）' : 'FULL CHAIN（金）'" :title="chain === 'fullchain2' ? 'FULL CHAIN（铂）' : 'FULL CHAIN（金）'">
        </span>
        <span v-if="hasClear(clear)" class="mark-slot" data-mark-slot="clear">
            <img v-if="clearSrc" class="native-mark" :src="clearSrc" :alt="clear?.toUpperCase()" :title="clear?.toUpperCase()">
            <span v-else-if="clear === 'failed'" class="failed-mark">FAILED</span>
        </span>
    </span>
</template>

<script setup lang="ts">
import {computed} from 'vue'
import {hasClear} from '@/components/chunithm/utils/versus'
const props = withDefaults(defineProps<{combo: string; chain: string; clear?: string | null; family?: 'result' | 'common'}>(), {clear: '', family: 'result'})
const pic = computed(() => `/assets/chunithm/pic/versus_${props.family === 'common' ? 'common_' : ''}`)
const comboSrc = computed(() => ['fullcombo', 'alljustice', 'alljusticecritical'].includes(props.combo) ? `${pic.value}${props.combo}.png` : '')
const chainSrc = computed(() => props.chain === 'fullchain' || props.chain === 'fullchain2' ? `${pic.value}${props.chain}.png` : '')
const clearSrc = computed(() => ['clear', 'hard', 'brave', 'absolute', 'catastrophy'].includes(props.clear ?? '') || (props.family === 'common' && props.clear === 'failed') ? `${pic.value}${props.clear}.png` : '')
const comboTitle = computed(() => ({fullcombo: 'FULL COMBO', alljustice: 'ALL JUSTICE', alljusticecritical: 'ALL JUSTICE CRITICAL'} as Record<string, string>)[props.combo])
</script>

<style scoped lang="postcss">
.chu-marks { display:inline-flex; align-items:center; justify-content:center; width:100%; gap:8px; flex-shrink:0; }
.mark-slot { display:grid; place-items:center; width:var(--chu-mark-width,92px); aspect-ratio:276 / 48; flex-shrink:0; }
.common-marks .mark-slot { aspect-ratio:134 / 26; }
.native-mark { display:block; width:100%; height:100%; object-fit:fill; }
.failed-mark { color:rgba(255,255,255,.5); font:700 11px/16px 'Torus',sans-serif; letter-spacing:.04em; }
</style>
