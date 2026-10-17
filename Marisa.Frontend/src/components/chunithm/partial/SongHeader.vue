<template>
    <div v-if="largeCover" class="song-layout">
        <SongMetaBar :song="song" class="song-meta-large"/>
        <div class="song-heading">
            <MaiSongHeading :title="song.Title" :artist="song.Artist" :max="110" :min="24"
                            :artist-size="23" :artist-top="0" :row-top="28"/>
        </div>
        <div ref="columns" class="song-columns">
            <div ref="artworkColumn" class="song-artwork" :style="{zoom: coverScale}">
                <MaiCover game="chunithm" :song-id="song.Id" :size="440" :frame-radius="30" :img-radius="24" class="song-cover-large"/>
                <slot name="artwork"/>
            </div>
            <section ref="chartColumn" class="song-charts" :style="{zoom: chartScale}">
                <slot/>
            </section>
        </div>
    </div>
    <template v-else>
        <SongMetaBar :song="song"/>
        <div class="flex items-end gap-5 mt-7">
            <MaiCover game="chunithm" :song-id="song.Id" :size="132" :frame-radius="22" :img-radius="16"/>
            <MaiSongHeading :title="song.Title" :artist="song.Artist" :max="84" :min="1" :artist-size="20" :artist-top="12"/>
        </div>
    </template>
</template>

<script setup lang="ts">
import {nextTick, onMounted, ref, watch} from 'vue'
import MaiCover from '@/components/maimai/MaiCover.vue'
import MaiSongHeading from '@/components/maimai/MaiSongHeading.vue'
import SongMetaBar from '@/components/chunithm/partial/SongMetaBar.vue'
import type {ChuSongHeaderData} from '@/components/chunithm/utils/song-card'
const props = withDefaults(defineProps<{song: ChuSongHeaderData; largeCover?: boolean}>(), {largeCover: false})
const columns = ref<HTMLElement | null>(null)
const artworkColumn = ref<HTMLElement | null>(null)
const chartColumn = ref<HTMLElement | null>(null)
const coverScale = ref(1)
const chartScale = ref(1)
let fitVersion = 0

async function fitColumns() {
    if (!props.largeCover) return
    const version = ++fitVersion
    coverScale.value = chartScale.value = 1
    await nextTick()
    try { await document.fonts.ready } catch { /* 测量兜底 */ }
    if (version !== fitVersion) return
    const container = columns.value
    const artwork = artworkColumn.value
    const charts = chartColumn.value
    const artworkLast = artwork?.lastElementChild
    const last = charts?.lastElementChild
    if (!container || !artwork || !artworkLast || !charts || !last) return
    // 只在未缩放时量测：Chrome 128 前 zoom 元素的 getBoundingClientRect 不是视觉尺寸
    const artworkHeight = artworkLast.getBoundingClientRect().bottom - artwork.getBoundingClientRect().top
    const chartHeight = last.getBoundingClientRect().bottom - charts.getBoundingClientRect().top
    const heightRatio = artwork.firstElementChild === artworkLast ? Math.max(1, chartHeight / artworkHeight) : chartHeight / artworkHeight
    const scale = (container.clientWidth - 28) / (452 * heightRatio + 624)
    chartScale.value = Math.min(1.15, Math.max(.8, scale))
    coverScale.value = (container.clientWidth - 28 - 624 * chartScale.value) / 452
}

onMounted(fitColumns)
watch(() => props.song, fitColumns, {flush: 'post'})
</script>

<style scoped lang="postcss">
.song-layout { margin-inline:44px; }
.song-meta-large { --pill-height:26px; --pill-lift:1.5px; gap:12px; height:68px; }
.song-meta-large :deep(.version-logo) { width:160px; height:68px; }
.song-meta-large :deep(.meta-pill) { font-size:17px; }
.song-meta-large :deep(.meta-chip) { max-width:210px; font-size:17px; }
.song-heading { margin-top:13px; width:1000px; max-width:100%; }
.song-heading :deep(.artist-line) { display:-webkit-box; -webkit-line-clamp:2; -webkit-box-orient:vertical; white-space:normal; line-height:33px; }
.song-columns { display:flex; gap:28px; align-items:flex-start; margin-top:26px; }
.song-artwork { display:flex; flex-direction:column; gap:24px; width:452px; flex-shrink:0; }
.song-cover-large { padding:6px; background:#ffffffbf; }
.song-charts { width:624px; flex-shrink:0; min-width:0; }
</style>
