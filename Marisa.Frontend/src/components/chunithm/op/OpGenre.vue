<script setup lang="ts">
import OverPower from "@/components/chunithm/partial/OverPower.vue";
import { GroupSongInfo } from "../utils/summary_t";
import { computed } from "vue";
import { useOpData } from "./op_common";

const genreLabel: Record<string, string> = {
    "ORIGINAL": "原创",
    "ゲキマイ": "音击舞萌",
    "VARIETY": "其他游戏",
    "イロドリミドリ": "彩绿",
    "東方Project": "东方",
    "niconico": "nico\nnico",
    "POPS & ANIME": "流行\n动漫",
    "POPS&ANIME": "流行\n动漫",
    "流行&动漫": "流行\n动漫",
};

const genreOrder = ["ORIGINAL", "ゲキマイ", "VARIETY", "イロドリミドリ", "東方Project", "niconico", "POPS & ANIME"];

const { data_fetched, songs, filterBestOP, buildGroups } = useOpData();

const groups = computed(() => {
    const raw = buildGroups(songs.value, (s: GroupSongInfo) => s.Item3.Genre);
    // 按 genreOrder 排序, 未识别的放最后
    return raw.sort((a, b) => {
        const ia = genreOrder.indexOf(a.label), ib = genreOrder.indexOf(b.label);
        if (ia >= 0 && ib >= 0) return ia - ib;
        if (ia >= 0) return -1;
        if (ib >= 0) return 1;
        return a.label.localeCompare(b.label);
    });
});

function displayLabel(raw: string): string {
    return genreLabel[raw] || raw;
}
</script>

<template>
    <div v-if="data_fetched" class="container">
        <template v-for="f in [filterBestOP(songs)]">
            <div class="op-container">
                <div>ALL</div>
                <OverPower :scores="f.scores" :group="f.group" :detail="true"/>
            </div>
        </template>
        <template v-for="g in groups" :key="g.label">
            <div class="op-container">
                <div class="label-text">{{ displayLabel(g.label) }}</div>
                <OverPower :scores="g.scores" :group="g.group" :detail="true"/>
            </div>
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
        @apply text-6xl w-[180px] whitespace-pre-line leading-tight;
    }
}
</style>
