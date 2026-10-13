<template>
    <header class="chu-meta">
        <img :src="logo" @error="logo = '/assets/chunithm/pic/logo.png'" class="version-logo" alt="CHUNITHM">
        <div class="meta-spacer"></div>
        <span v-if="song.Bpm" class="meta-pill"><small>BPM</small>{{ song.Bpm }}</span>
        <span v-if="song.Genre" class="meta-chip">{{ song.Genre }}</span>
        <span class="meta-pill">ID {{ song.Id }}</span>
    </header>
</template>

<script setup lang="ts">
import {ref, watchEffect} from 'vue'
import {versionLogo, type ChuSong} from '@/components/chunithm/utils/versus'
const props = defineProps<{song: ChuSong}>()
const logo = ref('')
watchEffect(() => { logo.value = versionLogo(props.song.Version) })
</script>

<style scoped lang="postcss">
.chu-meta { display:flex; align-items:center; gap:8px; white-space:nowrap; }
.version-logo { width:112px; height:50px; object-fit:contain; flex-shrink:0; filter:drop-shadow(0 3px 8px #0006); }
.meta-spacer { flex:1; }
.meta-pill { display:inline-flex; align-items:center; gap:7px; padding:3px 12px; border-radius:99px; color:#fff; background:var(--pill-bg); box-shadow:var(--pill-shadow); font:700 14px 'Torus',sans-serif; }
.meta-pill small { font-size:10px; color:#ffffff9c; }
.meta-chip { max-width:170px; overflow:hidden; text-overflow:ellipsis; border-radius:99px; padding:4px 12px; color:var(--chip-ink); background:var(--chip-bg); box-shadow:var(--chip-ring); font:700 13px 'Microsoft YaHei',sans-serif; }
</style>
