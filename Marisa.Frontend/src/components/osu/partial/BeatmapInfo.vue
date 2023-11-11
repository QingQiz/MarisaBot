<template>
    <div class="song-info-with-icon">
        <!-- length -->
        <div>
            <img :src="`/assets/osu/icon-total_length.png`" alt="">
            {{ SecondsToTime(beatmap.total_length) }}
        </div>
        <!-- bpm -->
        <div>
            <img :src="`/assets/osu/icon-bpm.png`" alt="">
            {{ beatmap.bpm }}
        </div>
        <!-- circles -->
        <div>
            <img :src="`/assets/osu/icon-count_circles.png`" alt="">
            {{ beatmap.count_circles }}
        </div>
        <!-- sliders -->
        <div>
            <img :src="`/assets/osu/icon-count_sliders.png`" alt="">
            {{ beatmap.count_sliders }}
        </div>
    </div>
</template>

<style scoped>
.song-info-with-icon {
    @apply flex gap-7 text-[#ffd996] font-bold text-2xl
}

.song-info-with-icon > div {
    @apply flex items-center gap-2
}

.song-info-with-icon > div > img {
    width: 40px;
    height: 40px;
}
</style>

<script setup lang="ts">
import { BeatmapInfo } from '../Osu.Data';

let prop = defineProps<{ beatmap: BeatmapInfo }>()


function SecondsToTime(seconds: number): string {
    if (seconds > 3600) {
        return `${Math.floor(seconds / 3600)}:${SecondsToTime(seconds % 3600)}`;
    } else {
        return `${Math.floor(seconds / 60).toString().padStart(2, '0')}:${(seconds % 60).toString().padStart(2, '0')}`;
    }
}

</script>