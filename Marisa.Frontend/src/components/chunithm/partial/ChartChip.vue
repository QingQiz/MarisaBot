<template>
    <div class="chart-chip" :class="{'chip-ultima': index === 4, 'chip-we': index === 5}" :style="{'--chip-color': difficultyColor(index), '--level-chars': levelChars}">
        <span class="chip-name">
            <img v-if="index === 5" src="/assets/chunithm/pic/song_worldsend.png" alt="WORLD'S END">
            <template v-else>{{ chart.LevelName }}</template>
        </span>
        <span v-if="index !== 5" class="chip-level"><span>{{ chart.LevelStr || '—' }}</span></span>
    </div>
</template>

<script setup lang="ts">
import {computed} from 'vue'
import {chartDifficulty, type ChuSongBeatmap} from '@/components/chunithm/utils/song'
import {difficultyColor} from '@/components/chunithm/utils/song-card'

const props = withDefaults(defineProps<{chart: ChuSongBeatmap; levelChars?: number}>(), {levelChars: 3})
const index = computed(() => chartDifficulty(props.chart))
</script>

<style scoped lang="postcss">
.chart-chip { --level-width:calc(var(--level-chars) * .75em + 2.8em); display:inline-flex; align-items:stretch; height:2.54em; overflow:hidden; border-radius:.67em 0 .93em 0; box-shadow:0 2px 8px #0000005a; font-size:var(--chip-size,15px); }
.chip-name { position:relative; z-index:1; display:flex; align-items:center; justify-content:center; width:var(--name-width,8.8em); border-radius:0 0 .93em 0; background:var(--chip-color); color:#fff; font:900 1em/1 'SEGA NewRodin','Microsoft YaHei',sans-serif; letter-spacing:.04em; white-space:nowrap; text-shadow:0 1px 2px #0000004d; }
.chip-level { display:flex; align-items:center; justify-content:center; width:var(--level-width); margin-left:-.93em; padding-left:.93em; background:color-mix(in srgb,var(--chip-color) 84%,#fff); color:#fff; text-shadow:0 1px 2px #0000004d; }
.chip-level > span { font:700 1.2em/1 'Torus',sans-serif; }

.chip-ultima .chip-name { background:linear-gradient(180deg,#221419,#09080b); color:#ff3a5f; box-shadow:inset 0 0 0 1px #e7184559; text-shadow:0 0 8px #e7184566; }
.chip-ultima .chip-level { background:#7e183d; }

.chip-we .chip-name { width:calc(var(--name-width,8.8em) + var(--level-width) - .93em); background:linear-gradient(135deg,#2a2e3c,#161922 72%); }
.chip-we .chip-name::after { content:''; position:absolute; left:0; right:0; bottom:0; height:2px; background:var(--we-spectrum); }
.chip-we img { display:block; height:2em; width:auto; }
</style>
