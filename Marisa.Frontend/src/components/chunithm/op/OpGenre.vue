<script setup lang="ts">
import { computed } from "vue";
import { GroupSongInfo } from "../utils/summary_t";
import { useOpData } from "./op_common";
import OpBoard, { type OpRow } from "./OpBoard.vue";
import { genreDisplay } from "../utils/song-card";

const genreOrder = ["ORIGINAL", "ゲキマイ", "VARIETY", "イロドリミドリ", "東方Project", "niconico", "POPS＆ANIME"];

const { data_fetched, songs, filterBestOP, buildGroups } = useOpData();

const groups = computed(() => {
    const raw = buildGroups(songs.value, (s: GroupSongInfo) => genreDisplay(s.Item3.Genre));
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

const rows = computed<OpRow[]>(() => groups.value);
</script>

<template>
    <OpBoard v-if="data_fetched" class="genre-tags" :charts="allBest.group" :scores="allBest.scores"
             :rows="rows"/>
</template>
