<script setup lang="ts">
import { ref } from "vue";
import { useRoute } from "vue-router";
import axios from "axios";
import { wordcloud_get } from "../../GlobalVars";
import VueWordCloud from 'vuewordcloud';


const route = useRoute()
let group = ref(route.query.GroupId)
let days = ref(route.query.days)

let data_fetched = ref(false)

let scentences = ref([])
let dict = ref({} as Record<string, number>)
let items = ref([] as [string, number][])

axios.get(wordcloud_get, {
    params: {
        GroupId: group.value,
        days: days.value
    }
}).then((response) => {
    scentences.value = response.data
    // statisic words
    if (scentences.value) {
        let disable1 = ["是", "�", "�", ",", "。", "，", "[", "]","(", ")", "（", "）"]
        let disable2 = "urdpc"
        for (let words of scentences.value) {
            for (let wf of words['Item2']) {
                let w = wf["Word"]
                let f = wf["Flag"]

                if (disable1.indexOf(w) !== -1) {
                    continue
                }
                if (disable2.indexOf(f[0]) !== -1) {
                    continue
                }
                if (w in dict.value) {
                    dict.value[w] += 1
                } else {
                    dict.value[w] = 1
                }
            }
        }

        // sort dict by value
        items.value = Object.keys(dict.value).map(function (key) {
            return [key, dict.value[key]];
        });
        items.value.sort(function (first, second) {
            return second[1] - first[1];
        });

        items.value = items.value.slice(0, 300)
    }
}).finally(() => {
    data_fetched.value = true

})

let color = function () {
    var colors = ['#d99cd1', '#c99cd1', '#b99cd1', '#a99cd1']

    return function () {
        return colors[Math.floor(Math.random() * colors.length)]
    };
}
</script>

<template>
    <div v-if="data_fetched">
        <div class=" flex w-screen h-screen">
            <VueWordCloud :words="items" :spacing="1 / 4" :color="color()"/>
        </div>
        <div>
            <div>
                {{ group }}
                {{ days }}
            </div>
            <div>
                <div v-for="i in scentences">
                    {{ i['Item1'] }}
                    {{ i['Item2'] }}
                </div>
            </div>
        </div>
    </div>
</template>



<style scoped></style>