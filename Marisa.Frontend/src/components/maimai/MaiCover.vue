<template>
    <div class="cover-frame shrink-0" :style="{ borderRadius: frameRadius + 'px' }">
        <img :src="src" @error="src = COVER_FALLBACK" alt="" class="block object-cover"
             :style="{ width: size + 'px', height: size + 'px', borderRadius: imgRadius + 'px' }">
    </div>
</template>

<script setup lang="ts">
import {ref, watchEffect} from 'vue'
import {coverSrcOf, COVER_FALLBACK} from '@/components/maimai/utils/song_card'

const props = defineProps<{songId: number; size: number; frameRadius: number; imgRadius: number}>()

const src = ref('')
watchEffect(() => { src.value = coverSrcOf(props.songId) })
</script>

<style scoped lang="postcss" src="@/assets/css/maimai/song_card.pcss"/>

<style scoped lang="postcss">
.cover-frame { padding: 5px; background: rgba(255,255,255,0.78); box-shadow: 0 0 0 1px rgba(255,255,255,0.8), 0 8px 20px -12px rgba(0,0,0,0.5); }
</style>
