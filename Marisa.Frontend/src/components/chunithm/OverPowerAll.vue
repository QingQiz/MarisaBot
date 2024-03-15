<script setup lang="ts">
import OverPower from "@/components/chunithm/partial/OverPower.vue";
import {ref} from "vue";
import {Score, GroupSongInfo} from "@/components/chunithm/Summary.vue";
import axios from "axios";
import {context_get} from "@/GlobalVars";
import {useRoute} from "vue-router";


const route = useRoute()
const id    = ref(route.query.id)

const data_fetched = ref(false)

const songs  = ref([] as GroupSongInfo[])
const scores = ref({} as { [key: string]: Score })

axios.all([
    axios.get(context_get, {params: {id: id.value, name: 'OverPowerSongs'}}),
    axios.get(context_get, {params: {id: id.value, name: 'OverPowerScores'}}),
]).then(data => {
    songs.value  = data[0].data
    scores.value = data[1].data
}).finally(() => {
    data_fetched.value = true
})

function GetScore(id: number, level: number) {
    return scores.value[`(${id}, ${level})`]
}

</script>

<template>
    <div v-if="data_fetched" class="container">
        <OverPower :scores="songs.map(x => GetScore(x.Item3.Id, x.Item2))" :group="songs"></OverPower>
    </div>
</template>

<style scoped>
.container {
    width: 1200px;
    padding: 50px;
}
</style>