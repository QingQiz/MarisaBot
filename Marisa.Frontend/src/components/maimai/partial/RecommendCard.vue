<script setup lang="ts">

import { Score } from "@/components/maimai/Recommend.vue";
import { ref } from "vue";

const props = defineProps<{ a: Score | null, b: Score | null }>();

</script>

<template>
    <div class="flex">
        <div class="min-w-[500px]">
            <div class="flex gap-3">
                <div>
                    {{ a?.Item4 }}

                </div>
                <div>
                    {{ a?.Item3.toFixed(4) }}
                </div>
                <div>
                    {{ a?.Item1.Title }}

                </div>

            </div>
        </div>
        <div class="min-w-[500px]">
            <div class="flex gap-3">
                <div>
                    {{ b?.Item4 }}

                </div>
                <div>
                    {{ b?.Item3.toFixed(4) }}
                </div>
                <div>
                    {{ b?.Item1.Title }}
                </div>
            </div>
        </div>
    </div>
</template>