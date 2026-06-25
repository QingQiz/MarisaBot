<script setup lang="ts">
import OverPower from "@/components/chunithm/partial/OverPower.vue";
import { GroupSongInfo } from "../utils/summary_t";
import { computed } from "vue";
import { useOpData } from "./op_common";

// 版本时间序 (来源: Wiki 追加日順), 从新到旧
const versionOrder: [string, string, string][] = [
    ["CHUNITHM XVERSEX",    "XVERSEX","logo_xversex.png"],
    ["CHUNITHM XVERSE",     "XVERSE","logo_xverse.png"],
    ["CHUNITHM VERSE",      "VERSE","logo_verse.png"],
    ["CHUNITHM LUMINOUS PLUS","LUMINOUS+","logo_luminous_plus.png"],
    ["CHUNITHM LUMINOUS",   "LUMINOUS","logo_luminous.png"],
    ["CHUNITHM SUN PLUS",   "SUN+", "logo_sun_plus.png"],
    ["CHUNITHM SUN",        "SUN",  "logo_sun.png"],
    ["CHUNITHM NEW PLUS!!",  "NEW PLUS!!","logo_new_plus.png"],
    ["CHUNITHM NEW!!",      "NEW!!","logo_new.png"],
    ["CHUNITHM PARADISE LOST","PARADISE LOST","logo_paradise_lost.png"],
    ["CHUNITHM PARADISE",   "PARADISE","logo_paradise.png"],
    ["CHUNITHM CRYSTAL PLUS","CRYSTAL+","logo_crystal_plus.png"],
    ["CHUNITHM CRYSTAL",    "CRYSTAL","logo_crystal.png"],
    ["CHUNITHM AMAZON PLUS","AMAZON+","logo_amazon_plus.png"],
    ["CHUNITHM AMAZON",     "AMAZON","logo_amazon.png"],
    ["CHUNITHM STAR PLUS",  "STAR+","logo_star_plus.png"],
    ["CHUNITHM STAR",       "STAR", "logo_star.png"],
    ["CHUNITHM AIR PLUS",   "AIR+", "logo_air_plus.png"],
    ["CHUNITHM AIR",        "AIR",  "logo_air.png"],
    ["CHUNITHM PLUS",       "PLUS", "logo_chunithm_plus.png"],
    ["CHUNITHM",            "無印", "logo_chunithm.png"],
];

const { data_fetched, songs, filterBestOP, buildGroups } = useOpData();

function versionLabel(v: string): string {
    const key = v.replace(/!!/g, "");
    for (const [k, label] of versionOrder) {
        if (k.replace(/!!/g, "") === key) return label;
    }
    return stripChunithm(v) || v;
}

function versionLogoPath(v: string): string {
    const key = v.replace(/!!/g, "");
    for (const [k, , logo] of versionOrder) {
        if (k.replace(/!!/g, "") === key) return `/assets/chunithm/pic/${logo}`;
    }
    return "";
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
                <div class="all-label">ALL</div>
                <OverPower :scores="f.scores" :group="f.group" :detail="true"/>
            </div>
        </template>
        <template v-for="g in groups" :key="g.label">
            <div class="op-container">
                <img :src="versionLogoPath(g.label)" class="ver-logo" :alt="versionLabel(g.label)">
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
    & > :first-child {
        width: 180px;
        flex-shrink: 0;
    }
}
.all-label {
    @apply text-6xl;
}
.ver-logo {
    width: 100%;
    max-width: 170px;
    height: auto;
    object-fit: contain;
    filter: drop-shadow(0 2px 4px rgba(0,0,0,0.3));
}
</style>
