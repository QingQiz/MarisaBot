<template>
    <header class="chu-meta">
        <img :src="logo" @error="logo = '/assets/chunithm/pic/logo.png'" class="version-logo" alt="CHUNITHM">
        <div class="meta-spacer"></div>
        <span v-if="song.Bpm" class="meta-pill meta-bpm">
            <svg class="bpm-icon" viewBox="0 0 24 24" fill="none" aria-label="BPM">
                <path d="M9.4 3h5.2c.5 0 .9.33 1 .8l3.1 14.6c.13.62-.34 1.2-1 1.2H6.3c-.66 0-1.13-.58-1-1.2L8.4 3.8c.1-.47.5-.8 1-.8z" stroke="currentColor" stroke-width="1.9" stroke-linejoin="round"/>
                <path d="M12 15.2 17.6 5.6" stroke="currentColor" stroke-width="1.9" stroke-linecap="round"/>
                <circle cx="12" cy="15.6" r="1.5" fill="currentColor"/>
            </svg>
            <span class="meta-bpm-value">{{ song.Bpm }}</span>
        </span>
        <span v-if="song.Genre" class="meta-chip" :class="{'meta-chip-cjk': /[^\x00-\x7f]/.test(song.Genre)}">{{ song.Genre }}</span>
        <span class="meta-pill">ID {{ song.Id }}</span>
    </header>
</template>

<script setup lang="ts">
import {ref, watchEffect} from 'vue'
import {versionLogo, type ChuSongHeaderData} from '@/components/chunithm/utils/song-card'
const props = defineProps<{song: ChuSongHeaderData}>()
const logo = ref('')
watchEffect(() => { logo.value = versionLogo(props.song.Version) })
</script>

<style scoped lang="postcss">
.chu-meta { --pill-height:23px; --pill-lift:0px; display:flex; align-items:center; gap:8px; white-space:nowrap; }
.version-logo { width:112px; height:50px; object-fit:contain; flex-shrink:0; filter:drop-shadow(0 3px 8px #0006); }
.meta-spacer { flex:1; }
.meta-pill { display:inline-flex; align-items:center; gap:.45em; padding:calc(3px - var(--pill-lift)) 12px calc(3px + var(--pill-lift)); border-radius:99px; color:#fff; background:var(--pill-bg); box-shadow:var(--pill-shadow); font:700 14px 'Torus',sans-serif; }
.bpm-icon { width:1.15em; height:1.15em; flex-shrink:0; transform:translateY(.07em); }
.meta-bpm { min-width:0; max-width:260px; }
.meta-bpm-value { min-width:0; white-space:pre-wrap; overflow-wrap:anywhere; }
.meta-chip { display:inline-block; height:var(--pill-height); max-width:170px; overflow:hidden; text-overflow:ellipsis; border-radius:99px; padding:0 13px calc(var(--pill-lift) * 2); color:var(--chip-ink); background:var(--chip-bg); box-shadow:inset var(--chip-ring); font:700 14px 'Torus',sans-serif; line-height:calc(var(--pill-height) - var(--pill-lift) * 2); letter-spacing:.04em; }
.meta-chip-cjk { font-family:'SEGA NewRodin','LXGW WenKai',sans-serif; letter-spacing:.02em; }
</style>
