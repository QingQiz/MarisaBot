<template>
    <img :src="src[srcIndex]" v-if="srcIndex < src.length" @error="srcIndex++" alt v-bind="$attrs" ref="imgEl"/>
    <img :src="fallback" v-if="srcIndex === src.length" alt v-bind="$attrs" ref="imgEl">
</template>

<script lang="ts">
import {defineComponent} from 'vue';

export default defineComponent({
    name: "FallbackImage",
    props: {
        src: Array,
        fallback: String,
    },
    data() {
        return {
            srcIndex: 0
        }
    },
    expose: ["GetAverageRGB"],
    methods: {
        GetAverageRGB(x: number, y: number, w: number, h: number) {
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

            let imgEl = this.$refs.imgEl as HTMLImageElement;

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
    }
})
</script>