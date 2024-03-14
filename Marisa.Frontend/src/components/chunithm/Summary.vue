<script setup lang="ts">
import { useRoute } from 'vue-router';
import { ref } from 'vue';
import axios from 'axios';
import { context_get } from "@/GlobalVars";

const route = useRoute()
const id = ref(route.query.id)

const data_fetched = ref(false)

const grouped = ref([] as any[])
const scores = ref({} as { [key: string]: Score })


axios.all([
    axios.get(context_get, { params: { id: id.value, name: 'GroupedSongs' } }),
    axios.get(context_get, { params: { id: id.value, name: 'Scores' } }),
]).then(data => {
    grouped.value = data[0].data
    scores.value = data[1].data
}).finally(() => {
    data_fetched.value = true
})

function GetScore(id: number, level: number) {
    return scores.value[`(${id}, ${level})`]
}

function GetLevelColor(level: number) {
    // green, yellow, red, purple, black, light purple
    const color = [
        '#52e72b',
        '#ffa801',
        '#ff5a66',
        '#c64fe4',
        '#000000',
        '#dbaaff'
    ]
    if (level >= 0 && level <= 4) return color[level]
    else return color[5]
}

function GetBorder(rank: string) {
    function fallback() {
        switch (rank) {
            case 'sp':
                return 's'
            case 'ssp':
                return 'ss';
            default:
                return rank
        }
    }

    return `/assets/chunithm/pic/border_${fallback()}.png`
}

function GetFontColor(fc: string, fallback = '#FFFFFF') {
    switch (fc) {
        case 'fullcombo':
            return '#76BA1B'
        case 'fullchain':
            return '#ACDF87'
        case 'fullchain2':
            return '#A4DE02'
        case 'alljustice':
            return '#FFDF00'
        default:
            return fallback
    }
}

function GetGroupMinFc(group: any[]) {
    let min = 5
    let fc = '';

    function FcRank(fc: string) {
        switch (fc) {
            case 'fullcombo':
                return 1
            case 'fullchain':
                return 2
            case 'fullchain2':
                return 3
            case 'alljustice':
                return 4
            default:
                return 0
        }
    }

    for (let i = 0; i < group.length; i++) {
        const song = group[i]
        const score = GetScore(song.Item3.Id, song.Item2)

        if (score) {
            if (FcRank(score.fc) < min) {
                min = FcRank(score.fc)
                fc = score.fc
            }
        } else {
            return ''
        }
    }
    return fc;
}

function GetGroupMinRank(group: any[]) {
    let min = 1145141919;

    for (let i = 0; i < group.length; i++) {
        const song = group[i]
        const score = GetScore(song.Item3.Id, song.Item2)

        if (score) {
            if (score.score < min) {
                min = score.score
            }
        } else {
            return ''
        }
    }

    let score_key_points = [100_9000, 100_7500, 100_5000, 100_0000, 99_0000, 97_5000, 95_0000, 92_5000, 90_0000, 80_0000, 70_0000, 60_0000, 50_0000, 0]
    let rank_key_points = ['sssp', 'sss', 'ssp', 'ss', 'sp', 's', 'aaa', 'aa', 'a', 'bbb', 'bb', 'b', 'c', 'd']

    for (let i = 0; i < score_key_points.length; i++) {
        if (min >= score_key_points[i]) {
            return rank_key_points[i]
        }
    }
}

</script>

<template>
    <div class="config" v-if="data_fetched">
        <div class="group">
            <div v-for="g1 in grouped">
                <div class="group-title" :style="`color: ${GetFontColor(GetGroupMinFc(g1), '#000000')}`">
                    {{ g1[0].Item1 }}
                    <img :src="`/assets/chunithm/pic/rank_${GetGroupMinRank(g1)}.png`" alt="" class="group-min-rank"
                        onerror="this.style.display='none'">
                </div>
                <div class="row">
                    <div v-for="song in g1" :set="score = GetScore(song.Item3.Id, song.Item2)" class="cell"
                        :style="`background-image: url('${GetBorder(score?.Rank ?? '')}')`">
                        <div class="cover"
                            :style="`background-image: url('/assets/chunithm/cover/${song.Item3.Id}.png')`">
                        </div>
                        <div class="achievement" :style="`color: ${GetFontColor(score.fc)}`" v-if="score">
                            {{ score.score.toString().padStart(7, '0') }}
                        </div>
                        <div class="level-mark" :style="`background-color: ${GetLevelColor(song.Item2)}`"></div>
                    </div>
                </div>
            </div>
        </div>
    </div>
</template>

<style scoped>
.config {
    --group-gap: 50px;
    --group-col-count: 8;
    --group-title-font-size: 100px;

    --cell-width: 200px;
    --cell-inset: 13px;
    --cell-gap: 30px;

    --achievemnet-height: 50px;
    --achievemnet-font-size: 43px;

    /* 小三角的尺寸 */
    --level-mark-size: 30px;

    padding: var(--cell-gap);

    @apply font-osu-web;
}

.group-title {
    font-size: var(--group-title-font-size);
    display: flex;
    @apply flex items-center place-content-between gap-5;
}

.group-min-rank {
    height: var(--group-title-font-size);
}

.level-mark {
    position: absolute;
    width: var(--level-mark-size);
    height: var(--level-mark-size);

    top: var(--cell-inset);
    left: var(--cell-inset);

    clip-path: polygon(0% 0%, 0% 100%, 100% 0%);
}

.cover {
    @apply h-full bg-cover;
}

.cell {
    position: relative;
    width: var(--cell-width);
    height: var(--cell-width);

    padding: var(--cell-inset);

    @apply bg-cover;
}

.row {
    display: grid;
    grid-template-columns: repeat(var(--group-col-count), var(--cell-width));
    gap: var(--cell-gap);
}

.group {
    display: flex;
    flex-direction: column;
    gap: var(--group-gap);
}

.achievement {
    width: calc(var(--cell-width) - 2 * var(--cell-inset));
    height: var(--achievemnet-height);

    bottom: var(--cell-inset);
    left: var(--cell-inset);
    right: var(--cell-inset);

    font-size: var(--achievemnet-font-size);

    @apply font-console;
    @apply absolute bg-black;
    @apply flex flex-col place-content-center;
    @apply text-center;
}
</style>

<script lang="ts">
interface Score {
    cid: number;
    ds: number;
    fc: string;
    level: string;
    level_index: number;
    level_label: string;
    mid: number;
    ra: number;
    score: number;
    title: string;
    Rank: string;
}

</script>