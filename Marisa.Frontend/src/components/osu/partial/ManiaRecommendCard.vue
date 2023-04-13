<script setup lang="ts">
import {ManiaRecommendData} from "@/components/osu/Osu.Data";
import RecommendCard from "@/components/osu/partial/RecommendCard.vue";

const props = defineProps<{ data: ManiaRecommendData }>();
</script>

<template>
    <recommend-card :data="props.data">
        <template #predict-key>predict PP acc</template>
        <template #predict-val>
            {{ props.data.predictAccuracy.toPercentage() }}
        </template>
    </recommend-card>
</template>