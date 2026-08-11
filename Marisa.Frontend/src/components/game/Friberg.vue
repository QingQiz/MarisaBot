<script setup lang="ts">
import {computed, ref} from "vue";
import axios from "axios";
import {context_get} from "@/GlobalVars";
import {useRoute} from "vue-router";

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
    Extra: FribergCell;
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

const columns = computed(() => [
    {key: 'Title',    label: '曲名'},
    {key: 'Artist',   label: '作者'},
    {key: 'Genre',    label: '流派'},
    {key: 'Version',  label: '版本'},
    {key: 'Constant', label: 'Mas定数'},
    {key: 'Bpm',      label: 'BPM'},
    {key: 'Extra',    label: game.value === 'maimai' ? 'ReM谱面' : 'Ult谱面'},
] as { key: keyof FribergRow, label: string }[])

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
                    {{ row[c.key].Value }}<span v-if="row[c.key].Arrow" class="arrow">{{ row[c.key].Arrow }}</span>
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
    width: max-content;
    max-width: 1400px;
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
    display: grid;
    grid-template-columns: repeat(7, max-content);
    border: 2px solid #3a3a3c;
    border-radius: 8px;
    overflow: hidden;
}

/* 行不再自建 grid，子单元格直接参与 .grid 布局，整表列对齐 */
.row {
    display: contents;
}

.row.header .cell {
    background: #1a1a1a;
    font-size: 18px;
    color: #999999;
}

.cell {
    max-width: 260px;
    min-width: 60px;
    padding: 14px 10px;
    border-right: 2px solid #3a3a3c;
    border-bottom: 2px solid #3a3a3c;
    text-align: center;
    font-size: 20px;
    line-height: 1.5;
    word-break: break-all;
    overflow-wrap: anywhere;
    display: flex;
    align-items: center;
    justify-content: center;
}

.cell:nth-child(7n) {
    border-right: none;
}

.cell:nth-last-child(-n+7) {
    border-bottom: none;
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
</style>
