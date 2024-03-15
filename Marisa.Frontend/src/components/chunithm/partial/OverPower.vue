<script setup lang="ts">
import {Score, GroupSongInfo} from "@/components/chunithm/Summary.vue";
import {computed} from "vue";


let props = defineProps<{
    group: GroupSongInfo[],
    scores: Score[]
}>()

function OverPower(achievement: number, constant: number, fc: string) {
    if (achievement == 0) return 0;

    let s = achievement <= 100_7500 ? constant : constant + 2;
    let r = fc == 'fullcombo' || fc == 'fullchain' || fc == 'fullchain2' ? 0.5 : 0;

    if (fc == 'alljustice') r = 1.0;
    if (achievement == 101_0000) r = 1.25;

    let e = achievement <= 100_7500 ? 0 : (achievement - 100_7500) * 0.0015;

    return s * 5 + r + e;
}

function GroupOverPower(group: GroupSongInfo[], scores: Score[]) {
    function FcRank(score: number, fc: string) {
        if (score == 101_0000) return 3;
        switch (fc) {
            case 'fullcombo':
            case 'fullchain':
            case 'fullchain2':
                return 1
            case 'alljustice':
                return 2
            default:
                return 0
        }
    }

    let groupOp: { [key: number]: number } = {
        0: 0,
        1: 0,
        2: 0,
        3: 0,
        // 没打的
        4: 0,
    }

    let opAll = 0;
    let opSum = 0;

    for (let i = 0; i < group.length; i++) {
        let song  = group[i]
        let score = scores[i]

        if (song.Item2 <= 2) continue;

        let constant = song.Item3.Constants[song.Item2]

        if (score) {
            let achievement = score.score
            let fc          = score.fc

            let rank = FcRank(achievement, fc)
            let op   = OverPower(achievement, constant, fc)
            opSum += op;
            groupOp[rank] += 1
        } else {
            groupOp[4] += 1
        }
        opAll += OverPower(101_0000, constant, 'alljustice')
    }

    groupOp[-1] = opAll;
    groupOp[-2] = opSum;
    return groupOp
}

let op = computed(() => GroupOverPower(props.group, props.scores));

let opSum = computed(() => {
    let sum = 0;
    for (let i = 0; i <= 4; i++) {
        sum += op.value[i]
    }
    return sum;
})

function OpWidth(idx: number) {
    return `${op.value[idx] / opSum.value * 100}%`
}

</script>

<template>
    <div class="flex gap-2 w-full text-black">
        <div class="bar-title">
            <div>
                {{ op[-2].toFixed(2) }}
            </div>
            <div>
                {{ op[-1].toFixed(2) }}
            </div>
        </div>

        <div class="bar relative">
            <div class="h-full bg-amber-200" :style="`width: ${OpWidth(3)}`"></div>
            <div class="h-full bg-amber-400" :style="`width: ${OpWidth(2)}`"></div>
            <div class="h-full bg-green-500" :style="`width: ${OpWidth(1)}`"></div>
            <div class="h-full bg-gray-100" :style="`width: ${OpWidth(0)}`"></div>
            <div class="absolute text-5xl inset-0 flex items-center place-content-center">
                {{ (op[-2] / op[-1] * 100).toFixed(2) }}%
            </div>
        </div>
    </div>
</template>

<style scoped>

.bar-title {
    font-size: 35px;

    @apply font-console flex flex-col text-right;
}

.bar {
    width: 100%;
    height: 100px;

    @apply border-4 border-black flex bg-gray-500;
}
</style>