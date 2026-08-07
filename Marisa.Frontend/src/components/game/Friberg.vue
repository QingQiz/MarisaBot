<script setup lang="ts">
import {ref} from "vue";
import axios from "axios";
import {context_get} from "@/GlobalVars";
import {useRoute} from "vue-router";
import {VERSION_CODE, LOGO_BBOX_LEFT, versionLogoSrc} from "@/components/maimai/utils/song_card";

// 版本时间序 (来源: OpVersion.vue), 从新到旧
const chuVersionOrder: [string, string][] = [
    ["CHUNITHM XVERSEX",        "logo_xversex.png"],
    ["CHUNITHM XVERSE",         "logo_xverse.png"],
    ["CHUNITHM VERSE",          "logo_verse.png"],
    ["CHUNITHM LUMINOUS PLUS",  "logo_luminous_plus.png"],
    ["CHUNITHM LUMINOUS",       "logo_luminous.png"],
    ["CHUNITHM SUN PLUS",       "logo_sun_plus.png"],
    ["CHUNITHM SUN",            "logo_sun.png"],
    ["CHUNITHM NEW PLUS!!",     "logo_new_plus.png"],
    ["CHUNITHM NEW!!",          "logo_new.png"],
    ["CHUNITHM PARADISE LOST",  "logo_paradise_lost.png"],
    ["CHUNITHM PARADISE",       "logo_paradise.png"],
    ["CHUNITHM CRYSTAL PLUS",   "logo_crystal_plus.png"],
    ["CHUNITHM CRYSTAL",        "logo_crystal.png"],
    ["CHUNITHM AMAZON PLUS",    "logo_amazon_plus.png"],
    ["CHUNITHM AMAZON",         "logo_amazon.png"],
    ["CHUNITHM STAR PLUS",      "logo_star_plus.png"],
    ["CHUNITHM STAR",           "logo_star.png"],
    ["CHUNITHM AIR PLUS",       "logo_air_plus.png"],
    ["CHUNITHM AIR",            "logo_air.png"],
    ["CHUNITHM PLUS",           "logo_chunithm_plus.png"],
    ["CHUNITHM",                "logo_chunithm.png"],
];

function chuVersionLogoPath(v: string): string {
    const key = v.replace(/!!/g, "");
    for (const [k, logo] of chuVersionOrder) {
        if (k.replace(/!!/g, "") === key) return `/assets/chunithm/pic/${logo}`;
    }
    return "";
}

interface FribergCell {
    Value: string;
    Status: string;
    Arrow: string;
}

interface FribergRow {
    Title: FribergCell;
    Artist: FribergCell;
    Genre: FribergCell;
    Version: FribergCell;
    Constant: FribergCell;
    Bpm: FribergCell;
}

const route = useRoute()
const id    = ref(route.query.id)

const data_fetched = ref(false)
const game   = ref('maimai')
const rows   = ref([] as FribergRow[])
const tries  = ref({Tries: 0, Max: 0})

axios.all([
    axios.get(context_get, {params: {id: id.value, name: 'FribergGame'}}),
    axios.get(context_get, {params: {id: id.value, name: 'FribergRows'}}),
    axios.get(context_get, {params: {id: id.value, name: 'FribergTries'}}),
]).then(data => {
    game.value = data[0].data
    rows.value = data[1].data
    tries.value = data[2].data
}).finally(() => {
    data_fetched.value = true
})

const columns: { key: keyof FribergRow, label: string }[] = [
    {key: 'Title',    label: '曲名'},
    {key: 'Artist',   label: '作者'},
    {key: 'Genre',    label: '流派'},
    {key: 'Version',  label: '版本'},
    {key: 'Constant', label: '定数'},
    {key: 'Bpm',      label: 'BPM'},
]

function cellClass(status: string) {
    switch (status) {
        case 'correct':
            return 'correct'
        case 'near':
            return 'near'
        default:
            return 'wrong'
    }
}

// 版本栏：maimai 用官方版本 logo 图片，chunithm 用 logo_*.png，未命中回退文本
function versionImg(src: string) {
    if (game.value === 'maimai') return versionLogoSrc(src)
    return chuVersionLogoPath(src) || null
}

function versionLogoStyle(src: string) {
    if (game.value !== 'maimai') return {}
    const code = VERSION_CODE[src ?? '']
    const trim = code ? (LOGO_BBOX_LEFT[code] ?? 0) * (60 / 160) : 0
    return {marginLeft: `${(-trim).toFixed(1)}px`}
}
</script>

<template>
    <div v-if="data_fetched" class="container">
        <div class="title">{{ game === 'maimai' ? '弗一把（舞萌版）' : '弗一把（中二版）' }}</div>
        <div class="subtitle">猜歌游戏 · 剩余次数 {{ tries.Max - tries.Tries }} / {{ tries.Max }}</div>
        <div class="grid">
            <div class="row header">
                <div v-for="c in columns" :key="c.key" class="cell">{{ c.label }}</div>
            </div>
            <div v-for="(row, i) in rows" :key="i" class="row">
                <div v-for="c in columns" :key="c.key" class="cell"
                     :class="cellClass(row[c.key].Status)">
                    <template v-if="c.key === 'Version' && versionImg(row[c.key].Value)">
                        <img :src="versionImg(row[c.key].Value)" :style="versionLogoStyle(row[c.key].Value)"
                             class="version-logo">
                        <span v-if="row[c.key].Arrow" class="arrow">{{ row[c.key].Arrow }}</span>
                    </template>
                    <template v-else>
                        {{ row[c.key].Value }}<span v-if="row[c.key].Arrow" class="arrow">{{ row[c.key].Arrow }}</span>
                    </template>
                </div>
            </div>
            <div v-if="rows.length === 0" class="row">
                <div class="cell empty" style="grid-column: 1 / -1">等待第一次猜测...</div>
            </div>
        </div>
    </div>
</template>

<style scoped>
.container {
    width: 1000px;
    padding: 50px;
    background: #000000;
    color: #ffffff;
    display: flex;
    flex-direction: column;
    gap: 16px;
}

.title {
    font-size: 48px;
    letter-spacing: 8px;
    text-align: center;
}

.subtitle {
    font-size: 20px;
    color: #888888;
    text-align: center;
}

.grid {
    border: 2px solid #3a3a3c;
    border-radius: 8px;
    overflow: hidden;
}

.row {
    display: grid;
    grid-template-columns: 1.6fr 1.2fr 1fr 1.2fr 0.9fr 0.9fr;
    border-bottom: 2px solid #3a3a3c;
    background: #000000;
}

.row:last-child {
    border-bottom: none;
}

.row.header {
    background: #1a1a1a;
    font-size: 18px;
    color: #999999;
}

.cell {
    padding: 14px 10px;
    border-right: 2px solid #3a3a3c;
    text-align: center;
    font-size: 20px;
    line-height: 1.5;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
}

.cell:last-child {
    border-right: none;
}

.cell.correct {
    background: #6aaa64;
    color: #ffffff;
}

.cell.near {
    background: #c9a958;
    color: #000000;
}

.cell.wrong {
    background: #000000;
    color: #ffffff;
}

.cell.empty {
    color: #555555;
}

.arrow {
    font-weight: bold;
    margin-left: 4px;
}

.version-logo {
    height: 34px;
    vertical-align: middle;
}
</style>
