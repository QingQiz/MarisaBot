<template>
    <div class="chart-chip" :class="{'chip-ultima': index === 4, 'chip-we': index === 5}" :style="{'--chip-color': difficultyColor(index), '--level-chars': levelChars}">
        <span class="chip-name">{{ chart.LevelName }}</span>
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

.chip-we .chip-name { width:calc(var(--name-width,8.8em) + var(--level-width) - .93em); background:none; }
.chip-we .chip-name::before {
    content:''; position:absolute; left:0; right:0; top:50%; z-index:-1; aspect-ratio:122/30; transform:translateY(-50%);
    background:
        linear-gradient(135deg,#84a448 44.95%,transparent 0) 56.92% 0/20.08% 100% no-repeat,
        linear-gradient(135deg,#c9ae0a 46.58%,transparent 0) 85.56% 0/23.36% 100% no-repeat,
        linear-gradient(135deg,#e3054d 59.77%,transparent 0) 100% 0/11.07% 100% no-repeat,
        linear-gradient(135deg,#9c06c0 16.78%,#04c1ca 0 34.7%,#98c433 0 52.63%,#d48808 0 70.56%,#bc06be 0 88.49%,#09d4bc 0);
}
</style>
