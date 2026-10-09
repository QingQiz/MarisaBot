<script setup lang="ts">
import OverPower from "@/components/chunithm/partial/OverPower.vue";
import {useOpData} from "./op/op_common";

const { data_fetched, songs, maxConstMap, filterBestOP } = useOpData();

function GetSongsByConstRange(a: number, b: number) {
    return songs.value.filter(x => {
        const maxC = maxConstMap.value.get(x.Item3.Id) || 0;
        return maxC >= a && maxC < b;
    })
}

function GetConstRange(): [number, number, string][] {
    let res = [];

    for (let i = 10; i < 16; i += 0.5) {
        res.push([i, i + 0.5, Math.floor(i) == i ? i.toString() : Math.floor(i).toString() + '+'] as [number, number, string]);
    }

    res.reverse();

    return res;
}
</script>

<template>
    <div v-if="data_fetched" class="container">
        <template v-for="f in [filterBestOP(songs)]">
            <div class="op-container">
                <div>ALL</div>
                <OverPower :scores="f.scores" :group="f.group" :detail="true" :show-difficulty="true"/>
            </div>
        </template>
        <template v-for="range in GetConstRange()">
            <template v-for="s in [GetSongsByConstRange(range[0], range[1])]">
                <template v-for="f in [filterBestOP(s)]">
                    <div v-if="f.group.length != 0" class="op-container">
                        <div>{{ range[2] }}</div>
                        <OverPower :scores="f.scores" :group="f.group" :detail="true" :show-difficulty="true"/>
                    </div>
                </template>
            </template>
        </template>
    </div>
</template>

<style scoped lang="postcss">
.container {
    max-width: unset;
    width: 1200px;
    padding: 50px;

    @apply flex flex-col gap-16;
}

.op-container {
    @apply flex items-center;

    & div:first-child {
        @apply text-6xl w-[180px];
    }
}
</style>