<script setup lang="ts">
import {useRoute} from 'vue-router';
import {ref} from 'vue';
import axios from 'axios';
import {context_get} from "@/GlobalVars";
import OverPower from "@/components/chunithm/partial/OverPower.vue";
import {GroupSongInfo, Score} from "@/components/chunithm/utils/summary_t";

const route = useRoute()
const id    = ref(route.query.id)

const data_fetched = ref(false)

const grouped = ref([] as GroupSongInfo[][])
const scores  = ref({} as { [key: string]: Score })


axios.all([
    axios.get(context_get, {params: {id: id.value, name: 'GroupedSongs'}}),
    axios.get(context_get, {params: {id: id.value, name: 'Scores'}}),
]).then(data => {
    grouped.value = data[0].data
    scores.value  = data[1].data

    for (let i = 0; i < grouped.value.length; i++) {
        grouped.value[i].sort((a, b) => {
            if (a.Item2 != b.Item2) return a.Item2 - b.Item2;
            return a.Item3.Id - b.Item3.Id;
        }).reverse()
    }
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
    let fc  = '';

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
        const song  = group[i]
        const score = GetScore(song.Item3.Id, song.Item2)

        if (score) {
            if (FcRank(score.fc) < min) {
                min = FcRank(score.fc)
                fc  = score.fc
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
        const song  = group[i]
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
    let rank_key_points  = ['sssp', 'sss', 'ssp', 'ss', 'sp', 's', 'aaa', 'aa', 'a', 'bbb', 'bb', 'b', 'c', 'd']

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
            <div class="flex gap-10">
                <div class="justify-center flex items-center">
                    <div class="text-[100px]">
                        SUMMARY
                    </div>
                </div>
                <OverPower
                    :group="grouped.flatMap((g: GroupSongInfo[]) => g)"
                    :scores="grouped.flatMap((g: GroupSongInfo[]) => g).map((s: GroupSongInfo) => GetScore(s.Item3.Id, s.Item2))"
                    :detail="true"
                    class="justify-self-end"/>
            </div>
            <div v-for="group in grouped">
                <div class="group-title" :style="`color: ${GetFontColor(GetGroupMinFc(group), '#000000')}`">
                    {{ group[0].Item1 }}
                    <img :src="`/assets/chunithm/pic/rank_${GetGroupMinRank(group)}.png`" alt="" class="group-min-rank"
                         onerror="this.style.opacity=0">
                    <OverPower :group="group" :scores="group.map((s: GroupSongInfo) => GetScore(s.Item3.Id, s.Item2))"
                               class="justify-self-end"/>
                </div>
                <div class="row">
                    <template v-for="song in group">
                        <div v-for="score in [GetScore(song.Item3.Id, song.Item2)]" class="cell"
                             :style="`background-image: url('${GetBorder(score?.Rank ?? '')}')`">
                            <div class="cover"
                                 :style="`background-image: url('/assets/chunithm/cover/${song.Item3.Id}.png')`">
                            </div>
                            <div class="achievement" :style="`color: ${GetFontColor(score.fc)}`" v-if="score">
                                {{ score.score.toString().padStart(7, '0') }}
                            </div>
                            <div class="level-mark" :style="`background-color: ${GetLevelColor(song.Item2)}`"></div>
                        </div>
                    </template>
                </div>
            </div>
        </div>
    </div>
</template>

<style scoped lang="postcss" src="@/assets/css/chunithm/summary.pcss"/>