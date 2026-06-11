<script setup lang="ts">
import OverPower from "@/components/chunithm/partial/OverPower.vue";
import { GroupSongInfo } from "../utils/summary_t";
import { computed } from "vue";
import { useOpData } from "./op_common";

// 版本时间序 (来源: Wiki 追加日順)
// 按无感叹号键对版本做容错匹配
const versionOrder: [string, string][] = [
    ["CHUNITHM",            "無印"],
    ["CHUNITHM PLUS",       "PLUS"],
    ["CHUNITHM AIR",        "AIR"],
    ["CHUNITHM AIR PLUS",   "AIR+"],
    ["CHUNITHM STAR",       "STAR"],
    ["CHUNITHM STAR PLUS",  "STAR+"],
    ["CHUNITHM AMAZON",     "AMAZON"],
    ["CHUNITHM AMAZON PLUS","AMAZON+"],
    ["CHUNITHM CRYSTAL",    "CRYSTAL"],
    ["CHUNITHM CRYSTAL PLUS","CRYSTAL+"],
    ["CHUNITHM PARADISE",   "PARADISE"],
    ["CHUNITHM PARADISE LOST","PARADISE LOST"],
    ["CHUNITHM NEW!!",      "NEW!!"],
    ["CHUNITHM NEW PLUS!!",  "NEW PLUS!!"],
    ["CHUNITHM SUN",        "SUN"],
    ["CHUNITHM SUN PLUS",   "SUN+"],
    ["CHUNITHM LUMINOUS",   "LUMINOUS"],
    ["CHUNITHM LUMINOUS PLUS","LUMINOUS+"],
    ["CHUNITHM VERSE",      "VERSE"],
    ["CHUNITHM XVERSE",     "XVERSE"],
    ["CHUNITHM XVERSEX",    "XVERSEX"],
];

const { data_fetched, songs, filterBestOP, buildGroups } = useOpData();

function versionLabel(v: string): string {
    // 去掉感叹号做容错匹配
    const key = v.replace(/!!/g, "");
    for (const [k, label] of versionOrder) {
        if (k.replace(/!!/g, "") === key) return label;
    }
    return stripChunithm(v) || v;
}

function stripChunithm(v: string): string {
    return v.replace(/^CHUNITHM\s*/, "");
}

const groups = computed(() => {
    const raw = buildGroups(songs.value, (s: GroupSongInfo) => s.Item3.Version);
    return raw.sort((a, b) => {
        const la = versionLabel(a.label), lb = versionLabel(b.label);
        const ia = versionOrder.findIndex(v => v[1] === la);
        const ib = versionOrder.findIndex(v => v[1] === lb);
        if (ia >= 0 && ib >= 0) return ia - ib;
        if (ia >= 0) return -1;
        if (ib >= 0) return 1;
        return a.label.localeCompare(b.label);
    });
});
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
                <div class="label-text">{{ versionLabel(g.label) }}</div>
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
