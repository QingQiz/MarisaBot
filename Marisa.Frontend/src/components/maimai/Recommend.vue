<script setup lang="ts">

import { ref } from "vue";
import { useRoute } from "vue-router";
import axios from "axios";
import { context_get } from "@/GlobalVars";
import RecommendCard from "@/components/maimai/partial/RecommendCard.vue";

const route = useRoute()
const id = ref(route.query.id)

const data_fetched = ref(false)

const current = ref({} as Scores)
const recommend = ref({} as Scores)

axios.all([
    axios.get(context_get, { params: { id: id.value, name: 'current' } }),
    axios.get(context_get, { params: { id: id.value, name: 'recommend' } }),
]).then(data => {
    current.value = data[0].data
    recommend.value = data[1].data
}).finally(() => {
    data_fetched.value = true
})

function GetDiff(scores1: Score[], scores2: Score[]) {
    let diff: [Score | null, Score | null][] = []

    for (let current of scores1) {
        let find = scores2.findIndex(x => x.Item1.Id == current.Item1.Id && x.Item2 == current.Item2);

        if (find == -1) {
            diff.push([current, null]);
        } else {
            diff.push([current, scores2[find]]);
        }
    }

    for (let current of scores2) {
        let find = scores1.findIndex(x => x.Item1.Id == current.Item1.Id && x.Item2 == current.Item2);

        if (find == -1) {
            diff.push([null, current]);
        }
    }

    diff.sort((a, b) => {
        if (b[1] != null && a[1] != null) {
            return b[1].Item4 - a[1].Item4;
        }

        if (b[1] != null && a[0] != null) {
            return b[1].Item4 - a[0].Item4;
        }

        if (b[0] != null && a[1] != null) {
            return b[0].Item4 - a[1].Item4;
        }

        return 0;
    });

    return diff;
}


</script>

<template>
    <template v-if="data_fetched">
        <div style="width: 1200px">
            <div v-for="item in GetDiff(current.NewScores, recommend.NewScores)">
                <RecommendCard :a="item[0]" :b="item[1]" />
            </div>
            <hr>
            <div v-for="item in GetDiff(current.OldScores, recommend.OldScores)">
                <RecommendCard :a="item[0]" :b="item[1]" />
            </div>
        </div>
    </template>
</template>

<script lang="ts">
import { defineComponent } from "vue";
import { logicalExpression } from "@babel/types";


export interface Scores {
    OldScores: Score[];
    NewScores: Score[];
}

export interface Score {
    Item1: Song;
    Item2: number;
    Item3: number;
    Item4: number;
}

export interface Song {
    Type: string;
    Charts: Chart[];
    Info: Info;
    Id: number;
    Title: string;
    Artist: string;
    Constants: number[];
    Levels: string[];
    Charters: string[];
    Bpm: number;
    Version: string;
}

export interface Chart {
    Notes: number[];
}

export interface Info {
    Title: string;
    Artist: string;
    Genre: string;
    Bpm: number;
    ReleaseDate: string;
    From: string;
    IsNew: boolean;
}


export default defineComponent({
    name: 'Recommend',
})
</script>