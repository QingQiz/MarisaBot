<script setup lang="ts">
import { computed } from "vue";
import { GroupSongInfo } from "../utils/summary_t";
import { useOpData } from "./op_common";
import OpBoard, { type OpRow } from "./OpBoard.vue";

const genreLabel: Record<string, string> = {
    "ORIGINAL": "原创",
    "ゲキマイ": "音击舞萌",
    "VARIETY": "其他游戏",
    "イロドリミドリ": "彩绿",
    "東方Project": "东方",
    "东方Project": "东方",
    "niconico": "niconico",
    "POPS & ANIME": "流行动漫",
    "POPS&ANIME": "流行动漫",
    "流行&动漫": "流行动漫",
};

const genreOrder = ["ORIGINAL", "ゲキマイ", "VARIETY", "イロドリミドリ", "東方Project", "niconico", "POPS & ANIME"];

const { data_fetched, songs, filterBestOP, buildGroups } = useOpData();

// 数据里的类别名带空格/简繁不一（"流行 & 动漫"、"东方Project"），查表前先把空格去掉再试一次，
// 否则会漏查而显示原文，标签宽度就长短不齐了
function displayLabel(raw: string): string {
    return genreLabel[raw] ?? genreLabel[raw.replace(/\s+/g, '')] ?? raw;
}

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

const allBest = computed(() => filterBestOP(songs.value));

const rows = computed<OpRow[]>(() =>
    groups.value.map(g => ({ label: displayLabel(g.label), group: g.group, scores: g.scores })));
</script>

<template>
    <OpBoard v-if="data_fetched" class="genre-tags" :charts="allBest.group" :scores="allBest.scores"
             :rows="rows"/>
</template>
