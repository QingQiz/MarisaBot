<script setup lang="ts">
import {Score, GroupSongInfo} from "../utils/summary_t";
import {computed} from "vue";


let props = defineProps({
    group: {
        type: Array as () => GroupSongInfo[],
        required: true
    },
    scores: {
        type: Array as () => Score[],
        required: true
    },
    detail: {
        type: Boolean,
        default: false
    }
})

function OverPower(score: Score) {
    if (score.score == 0) return 0;

    let s = score.score <= 100_7500 ? score.ra : score.ds + 2;
    let r = score.fc == 'fullcombo' || score.fc == 'fullchain' || score.fc == 'fullchain2' ? 0.5 : 0;

    if (score.fc == 'alljustice') r = 1.0;
    if (score.score == 101_0000) r = 1.25;

    let e = score.score <= 100_7500 ? 0 : (score.score - 100_7500) * 0.0015;

    let op = s * 5 + r + e;
    return parseInt((op * 100 * 2).toString()) / 200.0;
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
        // 一般的
        0: 0,
        // fullcombo & fullchain & fullchain2
        1: 0,
        // aj
        2: 0,
        // ajc
        3: 0,
        // 没打的
        4: 0,
    }

    let opAll = 0;
    let opSum = 0;

    for (let i = 0; i < group.length; i++) {
        let song  = group[i]
        let score = scores[i]

        if (song.Item2 != 3 && song.Item2 != 4) continue;

        let constant = song.Item3.Constants[song.Item2]

        // 好像有一些垃圾数据
        if (constant < 10) continue;

        if (score) {
            let achievement = score.score
            let fc          = score.fc

            let rank = FcRank(achievement, fc)
            let op   = OverPower(score)
            opSum += op;
            groupOp[rank] += 1
        } else {
            groupOp[4] += 1
        }
        opAll += (constant) * 5 + 15;
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
    <div class="w-full">
        <div class="flex gap-2 w-full text-black">
            <div class="bar-title">
                <pre>{{ op[-2].toFixed(2).padStart(8, ' ') }}</pre>
                <pre>{{ op[-1].toFixed(2).padStart(8, ' ') }}</pre>
            </div>

            <div class="w-full">
                <div class="bar relative">
                    <div class="h-full ajc" :style="`width: ${OpWidth(3)}`"></div>
                    <div class="h-full aj" :style="`width: ${OpWidth(2)}`"></div>
                    <div class="h-full fc" :style="`width: ${OpWidth(1)}`"></div>
                    <div class="h-full pl" :style="`width: ${OpWidth(0)}`"></div>
                    <div class="absolute text-5xl inset-0 flex items-center place-content-center">
                        {{ (op[-2] / op[-1] * 100).toFixed(2) }}%
                    </div>
                </div>
                <div v-if="detail" class="detail">
                    <pre class="t-all">ALL: {{ opSum.toString() }}</pre>
                    <pre class="t-ajc">AJC: {{ op[3].toString() }}</pre>
                    <pre class="t-aj">AJ: {{ op[2].toString() }}</pre>
                    <pre class="t-fc">FC: {{ op[1].toString() }}</pre>
                    <pre class="t-pl">PL: {{ op[0].toString() }}</pre>
                    <pre class="t-np">NP: {{ op[4].toString() }}</pre>
                </div>
            </div>

        </div>
    </div>
</template>

<style scoped lang="postcss">

.bar-title {
    font-size: 35px;

    @apply font-console flex flex-col text-right;
}

.bar {
    width: 100%;
    height: 100px;

    @apply border-4 border-black flex bg-gray-500;
}

.detail {
    @apply flex justify-between;

    font-size: 30px;
}

.ajc {
    @apply bg-amber-200;
}

.aj {
    @apply bg-amber-400;
}

.fc {
    @apply bg-green-500;
}

.pl {
    @apply bg-gray-100;
}

.t-all {
    @apply text-black;
}

.t-ajc {
    @apply text-amber-200;
}

.t-aj {
    @apply text-amber-400;
}

.t-fc {
    @apply text-green-500;
}

.t-pl {
    @apply text-gray-300;
}

.t-np {
    @apply text-gray-500;
}
</style>