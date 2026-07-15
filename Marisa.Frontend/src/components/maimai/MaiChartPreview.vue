<script setup lang="ts">
import {onMounted, onUnmounted, ref, shallowRef} from "vue";
import {useRoute} from "vue-router";
import {
    DIFFICULTY_COLORS,
    DIFFICULTY_NAMES,
    HI_SPEED_DEFAULT,
    MainRenderer,
    TimingTimeline,
    getAvailableDifficulties,
    parseMa2Chart,
    parseSimaiChart,
} from "@/vendor/maimai-chart-engine/src";
import type {Chart, ChartDifficulty} from "@/vendor/maimai-chart-engine/src";
import {normalizeMa2Commands} from "@/components/maimai/utils/ma2_compat";
import {host} from "@/GlobalVars";

const BEATS_PER_MEASURE = 4;
const UTAGE_ID_START = 100000;
const UTAGE_COLOR = "#E64FEB";
const HI_SPEED_MIN = 3;
const HI_SPEED_MAX = 9;
// 播放结束的宽限：最后一个小节末尾之后仍有 note 判定动画，略微延后停表
const END_GRACE_MS = 300;

const route = useRoute();

const songId = Number(route.query.id);
const isUtage = songId >= UTAGE_ID_START;

const loading = ref(true);
const error = ref("");
const title = ref("");
const playing = ref(false);
const currentMs = ref(0);
const totalMs = ref(0);
const hiSpeed = ref(HI_SPEED_DEFAULT);
const playbackSpeed = ref(1.0);
const availableDiffs = ref<ChartDifficulty[]>([]);
const selectedDiff = ref<ChartDifficulty | null>(null);
const chart = shallowRef<Chart | null>(null);

const canvasRef = ref<HTMLCanvasElement | null>(null);
const canvasBoxRef = ref<HTMLDivElement | null>(null);

let renderer: MainRenderer | null = null;
let timeline: TimingTimeline | null = null;
// 本地谱面（仓库静态资源）的游戏难度码列表；null 表示走 lxns 兜底
let localDiffs: number[] | null = null;
// lxns 兜底的 simai 文本（单文件含全部难度）
let simaiText: string | null = null;
let rafId: number | null = null;
// rAF 外推时钟锚点：seek/变速后置空，下一帧重新锚定
let anchorTimestamp: number | null = null;
let anchorMs = 0;
let resizeObserver: ResizeObserver | null = null;

async function fetchText(url: string): Promise<string | null> {
    try {
        const response = await fetch(url);
        if (!response.ok) return null;
        return await response.text();
    } catch {
        return null;
    }
}

function preloadImage(src: string): Promise<void> {
    return new Promise(resolve => {
        const img = new Image();
        img.onload = () => resolve();
        img.onerror = () => resolve();
        img.src = src;
    });
}

function formatTime(ms: number): string {
    const s = Math.max(0, Math.floor(ms / 1000));
    return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, "0")}`;
}

function diffName(d: ChartDifficulty): string {
    return isUtage ? "U·TA·GE" : DIFFICULTY_NAMES[d];
}

function diffStyle(d: ChartDifficulty) {
    const color = isUtage ? UTAGE_COLOR : DIFFICULTY_COLORS[d];
    return selectedDiff.value === d
        ? {background: color, borderColor: color, color: "#18181b"}
        : {borderColor: color, color};
}

function renderAt(ms: number) {
    if (!renderer || !chart.value || !timeline) return;
    renderer.renderFrame(chart.value, timeline.beatFromMs(ms), BEATS_PER_MEASURE);
}

function tick(timestamp: number) {
    if (!playing.value) return;

    if (anchorTimestamp === null) {
        anchorTimestamp = timestamp;
        anchorMs = currentMs.value;
    }

    const ms = anchorMs + (timestamp - anchorTimestamp) * playbackSpeed.value;
    if (ms >= totalMs.value + END_GRACE_MS) {
        currentMs.value = totalMs.value;
        pause();
        renderAt(totalMs.value);
        return;
    }

    currentMs.value = ms;
    renderAt(ms);
    rafId = requestAnimationFrame(tick);
}

function play() {
    if (!chart.value || playing.value) return;
    if (currentMs.value >= totalMs.value - 10) {
        currentMs.value = 0;
    }
    playing.value = true;
    renderer?.setIsPlaying(true);
    anchorTimestamp = null;
    rafId = requestAnimationFrame(tick);
}

function pause() {
    playing.value = false;
    renderer?.setIsPlaying(false);
    if (rafId !== null) {
        cancelAnimationFrame(rafId);
        rafId = null;
    }
}

function togglePlayback() {
    playing.value ? pause() : play();
}

function seekTo(ms: number) {
    currentMs.value = Math.max(0, Math.min(ms, totalMs.value));
    anchorTimestamp = null;
    if (!playing.value) renderAt(currentMs.value);
}

function onSeekInput(event: Event) {
    seekTo(Number((event.target as HTMLInputElement).value));
}

function onSpeedChange() {
    anchorTimestamp = null;
}

function adjustHiSpeed(delta: number) {
    hiSpeed.value = Math.max(HI_SPEED_MIN, Math.min(HI_SPEED_MAX, hiSpeed.value + delta));
    renderer?.setHiSpeed(hiSpeed.value);
    if (!playing.value) renderAt(currentMs.value);
}

async function selectDifficulty(d: ChartDifficulty) {
    if (selectedDiff.value === d && chart.value) return;
    pause();
    error.value = "";

    let parsed: Chart;
    try {
        if (localDiffs !== null) {
            const file = `${String(songId).padStart(6, "0")}_${String(d - 2).padStart(2, "0")}.ma2`;
            const text = await fetchText(`/assets/maimai/chart/${file}`);
            if (!text) {
                error.value = "谱面加载失败";
                return;
            }
            parsed = parseMa2Chart(normalizeMa2Commands(text), d);
        } else {
            parsed = parseSimaiChart(simaiText!, d);
        }
    } catch {
        error.value = "谱面解析失败";
        return;
    }

    selectedDiff.value = d;
    chart.value = parsed;
    if (!title.value) title.value = parsed.title;
    timeline = new TimingTimeline(parsed.bpm, parsed.bpmEvents);
    totalMs.value = timeline.msFromBeat(parsed.measures * BEATS_PER_MEASURE);
    seekTo(0);
}

async function init() {
    if (!Number.isFinite(songId) || songId <= 0) {
        error.value = "无效的歌曲 id";
        loading.value = false;
        return;
    }

    const indexText = await fetchText("/assets/maimai/chart/index.json");
    const index = indexText ? JSON.parse(indexText) : null;
    const entry = index?.[String(songId)];

    if (entry) {
        localDiffs = entry.diffs as number[];
        title.value = entry.title;
        availableDiffs.value = localDiffs.map(d => (d + 2) as ChartDifficulty);
    } else {
        // 本地未收录（dump 之后的新歌）：后端从 lxns 拉取并缓存
        simaiText = await fetchText(`${host}/MaiMai/Chart?id=${songId}`);
        if (!simaiText) {
            error.value = "谱面不存在";
            loading.value = false;
            return;
        }
        const available = getAvailableDifficulties(simaiText);
        availableDiffs.value = (Object.keys(available).map(Number) as ChartDifficulty[])
            .filter(d => available[d])
            .sort((a, b) => a - b);
    }

    if (availableDiffs.value.length === 0) {
        error.value = "谱面不存在";
        loading.value = false;
        return;
    }

    const queryDiff = Number(route.query.difficulty);
    const wanted = (queryDiff + 2) as ChartDifficulty;
    const initial = availableDiffs.value.includes(wanted)
        ? wanted
        : availableDiffs.value[availableDiffs.value.length - 1];

    await selectDifficulty(initial);
    if (title.value) document.title = `${title.value} - 谱面预览`;
    loading.value = false;
}

function onKeyDown(event: KeyboardEvent) {
    if (event.target instanceof HTMLInputElement) return;
    if (event.key === " ") {
        event.preventDefault();
        togglePlayback();
    }
}

onMounted(async () => {
    // 判定区贴图预载进浏览器缓存，保证 renderer 首帧就带 sensor 层
    await preloadImage("/assets/maimai/chart/sensor.webp");

    if (canvasRef.value) {
        renderer = new MainRenderer(canvasRef.value);
        renderer.setHiSpeed(hiSpeed.value);
    }

    if (canvasBoxRef.value) {
        resizeObserver = new ResizeObserver(() => {
            renderer?.resize();
            renderAt(currentMs.value);
        });
        resizeObserver.observe(canvasBoxRef.value);
    }

    window.addEventListener("keydown", onKeyDown);
    await init();
});

onUnmounted(() => {
    pause();
    resizeObserver?.disconnect();
    window.removeEventListener("keydown", onKeyDown);
});
</script>

<template>
    <div class="min-h-screen bg-neutral-900 text-neutral-100 flex flex-col items-center px-3 py-4">
        <div class="w-full max-w-[600px]">
            <div class="mb-2">
                <div class="text-lg font-bold truncate">{{ title || "谱面预览" }}</div>
                <div v-if="availableDiffs.length" class="mt-1.5 flex flex-wrap gap-1.5">
                    <button
                        v-for="d in availableDiffs" :key="d"
                        class="px-2.5 py-0.5 rounded-full text-xs font-bold border transition-colors"
                        :style="diffStyle(d)"
                        @click="selectDifficulty(d)"
                    >{{ diffName(d) }}
                    </button>
                </div>
            </div>

            <div ref="canvasBoxRef" class="relative w-full aspect-square bg-black rounded-lg overflow-hidden">
                <canvas ref="canvasRef" class="block mx-auto"/>
                <div
                    v-if="loading || error"
                    class="absolute inset-0 flex items-center justify-center bg-black/70 text-sm text-neutral-300"
                >{{ error || "加载中…" }}
                </div>
            </div>

            <div class="mt-3 flex items-center gap-3">
                <button
                    class="w-11 h-11 shrink-0 rounded-full bg-neutral-100 text-neutral-900 text-lg font-bold flex items-center justify-center"
                    :disabled="!chart"
                    @click="togglePlayback"
                >{{ playing ? "⏸" : "▶" }}
                </button>
                <input
                    class="flex-1 accent-neutral-100"
                    type="range" min="0" :max="Math.ceil(totalMs)" step="100"
                    :value="currentMs"
                    @input="onSeekInput"
                />
                <div class="shrink-0 text-xs tabular-nums text-neutral-400">
                    {{ formatTime(currentMs) }} / {{ formatTime(totalMs) }}
                </div>
            </div>

            <div class="mt-2.5 flex items-center gap-6 text-sm">
                <div class="flex items-center gap-2">
                    <span class="text-neutral-400 text-xs">流速</span>
                    <button class="w-7 h-7 rounded bg-neutral-700" @click="adjustHiSpeed(-0.5)">−</button>
                    <span class="w-8 text-center tabular-nums">{{ hiSpeed.toFixed(1) }}</span>
                    <button class="w-7 h-7 rounded bg-neutral-700" @click="adjustHiSpeed(0.5)">＋</button>
                </div>
                <div class="flex items-center gap-2">
                    <span class="text-neutral-400 text-xs">倍速</span>
                    <select
                        v-model.number="playbackSpeed"
                        class="bg-neutral-700 rounded px-1.5 py-1 text-sm"
                        @change="onSpeedChange"
                    >
                        <option :value="1">1.0×</option>
                        <option :value="0.75">0.75×</option>
                        <option :value="0.5">0.5×</option>
                        <option :value="0.25">0.25×</option>
                    </select>
                </div>
            </div>

            <div class="mt-4 text-[11px] text-neutral-500">
                渲染引擎：<a class="underline" href="https://github.com/Lxns-Network/maimai-prober-frontend">maimai-chart-engine</a>（Lxns Network，MIT License）
            </div>
        </div>
    </div>
</template>
