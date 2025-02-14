<template>
    <img :src="prefix + src[srcIndex]" v-if="srcIndex < src.length" @error="srcIndex++" v-bind="$attrs" ref="img"/>
    <img :src="prefix + fallback" v-if="fallback != null && srcIndex === src.length" v-bind="$attrs" ref="img">
</template>

<script setup lang="ts">
import {computed, PropType, ref} from 'vue';

const props = defineProps({
    src: {type: Array as PropType<string[]>, required: true},
    prefix: {type: String, required: false, default: ''},
    fallback: String,
});


const img = ref<HTMLImageElement | null>(null);

let srcIndex = ref(0)
let currentSrc = computed(() => img.value?.src);

function GetAverageRGB(x: number, y: number, w: number, h: number) {
    let blockSize = 3,
        defaultRGB = {r: 0, g: 0, b: 0}, // for non-supporting envs
        canvas = document.createElement('canvas'),
        context = canvas.getContext && canvas.getContext('2d'),
        data,
        i = -4,
        length,
        rgb = {r: 0, g: 0, b: 0},
        count = 0;

    if (!context) {
        return defaultRGB;
    }

    let imgEl = img.value as HTMLImageElement | null;

    if (imgEl == null) return defaultRGB;

    canvas.height = imgEl.height;
    canvas.width = imgEl.width;

    context.drawImage(imgEl, 0, 0);

    try {
        data = context.getImageData(x, y, w, h);
    } catch (e) {
        /* security error, img on diff domain */
        return defaultRGB;
    }

    length = data.data.length;

    while ((i += blockSize * 4) < length) {
        ++count;
        rgb.r += data.data[i];
        rgb.g += data.data[i + 1];
        rgb.b += data.data[i + 2];
    }

    // ~~ used to floor values
    rgb.r = ~~(rgb.r / count);
    rgb.g = ~~(rgb.g / count);
    rgb.b = ~~(rgb.b / count);

    return rgb;
}

defineExpose({
    GetAverageRGB,
    src: currentSrc,
})

</script>