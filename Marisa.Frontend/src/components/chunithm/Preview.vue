<script setup lang="ts">

import {ref} from "vue";
import {cat_1, cat_2, cat_3, Chart, Parse, SplitChartByStep} from "@/components/chunithm/utils/parser";
import {useRoute} from "vue-router";
import {context_get} from "@/GlobalVars";
import axios from "axios";

const route = useRoute()

let id           = ref(route.query.id)
let name         = ref(route.query.name)
let data_fetched = ref(false);

let chart    = ref({} as Chart);
let tick_max = ref(0);
let step     = 0;
let split_to = 20;


axios(name.value ? `/assets/chunithm/chart/${name.value}.c2s` : context_get, {params: {id: id.value, name: 'chart'}})
    .then(data => {
        let [c, t] = Parse(data.data);
        step       = Math.floor(t / split_to)

        SplitChartByStep(c, step, t);

        [chart.value, tick_max.value] = [c, t + 10];
    })
    .finally(() => data_fetched.value = true);


function GetIndex() {
    let res = [];

    for (let i = 0; i < Math.floor((tick_max.value + step - 1) / step); i++) {
        res.push(i);
    }
    return res;
}

function GetNotes(key: string, i: number) {
    if (!chart.value[key]) return [];

    return chart.value[key].filter(note => note[0] >= i * step && note[0] < (i + 1) * step);
}
</script>


<template>
    <div v-if="data_fetched" class="config stage-container">
        <div class="stage" :style="`--tick-min: ${i * step}; --tick-max: ${(i + 1) * step}`"
             v-for="i in GetIndex()">
            <div v-for="type in cat_3">
                <div v-for="note in GetNotes(type, i)"
                     :class="type"
                     :style="`--tick:${note[0]}; --cell:${note[1]}; --width:${note[2]}; --tick-end:${note[3]}; --target-cell:${note[4]}; --target-width:${note[5]}`">
                </div>
            </div>
            <div v-for="type in cat_2">
                <div v-for="note in GetNotes(type, i)"
                     :class="type"
                     :style="`--tick:${note[0]}; --cell:${note[1]}; --width:${note[2]}; --tick-end:${note[3]}`">
                </div>
            </div>
            <div v-for="type in cat_1">
                <div v-for="note in GetNotes(type, i)"
                     :class="type"
                     :style="`--tick:${note[0]}; --cell:${note[1]}; --width:${note[2]}`">
                </div>
            </div>
            <div v-for="note  in GetNotes('BEAT_1', i)"
                 class="BEAT_1"
                 :style="`--tick:${note[0]};`">
                #{{ note[1] }}
            </div>
            <div v-for="note in GetNotes('BPM', i)"
                 class="BPM"
                 :style="`--tick:${note[0]};`">
                {{ note[1] }}
            </div>
            <div v-for="note in GetNotes('BEAT_2', i)"
                 class="BEAT_2"
                 :style="`--tick:${note[0]};`">
            </div>
        </div>
    </div>
</template>

<style scoped lang="postcss" src="../../assets/css/chunithm/preview.pcss"></style>
