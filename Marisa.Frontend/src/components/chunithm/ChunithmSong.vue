<script setup lang="ts">
import axios from "axios";
import {ref, computed, onMounted, watch, nextTick} from "vue";
import {useRoute} from "vue-router";
import {context_get} from "@/GlobalVars";

const route = useRoute()
let id = ref(route.query.id)

let song        = ref({} as any)
let data_loaded = ref(false)
axios.get(context_get, {params: {id: id.value, name: 'SongData'}})
    .then(response => { song.value = response.data })
    .finally(() => data_loaded.value = true)

const DIFF_COLORS: Record<string, string> = {
    "BASIC"      : "rgb(82, 231, 43)",
    "ADVANCED"   : "rgb(255, 168, 1)",
    "EXPERT"     : "rgb(255, 90, 102)",
    "MASTER"     : "rgb(198, 79, 228)",
    "ULTIMA"     : "#FF3A3A",
    "WORLD'S END": "rgb(180, 140, 220)",
};

const DIFF_LIGHT: Record<string, string> = {
    "BASIC"      : "rgb(112, 241, 63)",
    "ADVANCED"   : "rgb(255, 188, 31)",
    "EXPERT"     : "rgb(255, 110, 122)",
    "MASTER"     : "rgb(218, 99, 248)",
    "ULTIMA"     : "#222",
    "WORLD'S END": "rgb(200, 160, 240)",
};

// 版本 → logo 图片映射
const VERSION_LOGO: Record<string, string> = {
    "CHUNITHM":             "logo_chunithm.png",
    "CHUNITHM PLUS":        "logo_chunithm_plus.png",
    "CHUNITHM AIR":         "logo_air.png",
    "CHUNITHM AIR PLUS":    "logo_air_plus.png",
    "CHUNITHM STAR":        "logo_star.png",
    "CHUNITHM STAR PLUS":   "logo_star_plus.png",
    "CHUNITHM AMAZON":      "logo_amazon.png",
    "CHUNITHM AMAZON PLUS": "logo_amazon_plus.png",
    "CHUNITHM CRYSTAL":     "logo_crystal.png",
    "CHUNITHM CRYSTAL PLUS": "logo_crystal_plus.png",
    "CHUNITHM PARADISE":    "logo_paradise.png",
    "CHUNITHM PARADISE LOST":"logo_paradise_lost.png",
    "CHUNITHM NEW!!":        "logo_new.png",
    "CHUNITHM NEW PLUS!!":   "logo_new_plus.png",
    "CHUNITHM SUN":         "logo_sun.png",
    "CHUNITHM SUN PLUS":    "logo_sun_plus.png",
    "CHUNITHM LUMINOUS":    "logo_luminous.png",
    "CHUNITHM LUMINOUS PLUS":"logo_luminous_plus.png",
    "CHUNITHM VERSE":       "logo_verse.png",
    "CHUNITHM XVERSE":      "logo_xverse.png",
    "CHUNITHM XVERSEX":     "logo_xversex.png",
};

const versionLogo = computed(() => {
    const v = song.value.Version ?? "";
    const fn = VERSION_LOGO[v] ?? VERSION_LOGO[Object.keys(VERSION_LOGO).find(k => v.includes(k)) ?? ""];
    return fn ? `/assets/chunithm/pic/${fn}` : "";
});

function bpmDomain(): string {
    const bpms = song.value.Beatmaps?.map((c: any) => {
        const m = c.Bpm?.match(/^([\d.]+)/);
        return m ? parseFloat(m[1]) : 0;
    }).filter((x: number) => x > 0) || [];
    return bpms[0]?.toString() || '';
}

function calcTol(N: number) {
    function cells(buffer: number) {
        const raw = buffer * N;
        const atk = Math.floor(raw / 51);
        const jst = Math.floor(raw - atk * 51);
        return { attack: atk, justice: jst };
    }
    return { sss: cells(0.25), sssp: cells(0.10) };
}

const masterTol = computed(() => {
    const bm = song.value.Beatmaps?.[3];
    if (!bm || bm.Constant <= 0) return null;
    return calcTol(bm.MaxCombo);
});

const ultimaTol = computed(() => {
    const bm = song.value.Beatmaps?.[4];
    if (!bm || bm.Constant <= 0) return null;
    return calcTol(bm.MaxCombo);
});

const chartCount = computed(() => song.value.Beatmaps?.length || 0);

// ── 标题自动缩字 ──
const titleEl   = ref<HTMLElement | null>(null);
const titleSize = ref(62);
const TITLE_MIN = 28;

async function fitTitle() {
    await nextTick();
    const el = titleEl.value;
    if (!el) return;
    el.style.fontSize = '62px';
    try { await document.fonts.ready } catch {}
    await nextTick();
    for (let pass = 0; pass < 4 && el.scrollWidth > el.clientWidth; pass++) {
        const next = Math.floor(62 * el.clientWidth / el.scrollWidth);
        titleSize.value = Math.max(TITLE_MIN, Math.min(next, titleSize.value - 1));
        await nextTick();
        if (titleSize.value <= TITLE_MIN) break;
    }
}

watch(() => song.value.Title, () => { titleSize.value = 62; nextTick(fitTitle); });

onMounted(() => { nextTick(fitTitle); });
</script>

<template>
    <div v-if="!data_loaded" class="flex items-center justify-center h-[600px] bg-[#0e0418]">
        <div class="text-white/40 text-3xl font-bold tracking-widest">CHUNITHM Song {{ id }}</div>
    </div>
    <div v-else class="chu-song w-[1240px] antialiased">
        <div class="stripe-layer"></div>
        <div class="inner">
            <!-- ── 顶栏：版本 logo → BPM → Genre → ID ── -->
            <header class="top-bar">
                <img v-if="versionLogo" :src="versionLogo" class="ver-logo" alt="">
                <span v-else class="version-tag">{{ song.Version?.replace(/^CHUNITHM\s*/, "") || "" }}</span>
                <div class="flex-1"></div>
                <div class="bpm-pill">
                    <svg class="bpm-icon" viewBox="0 0 24 24" fill="none">
                        <path d="M9.4 3h5.2c.5 0 .9.33 1 .8l3.1 14.6c.13.62-.34 1.2-1 1.2H6.3c-.66 0-1.13-.58-1-1.2L8.4 3.8c.1-.47.5-.8 1-.8z" stroke="currentColor" stroke-width="1.9" stroke-linejoin="round"/>
                        <path d="M12 15.2 17.6 5.6" stroke="currentColor" stroke-width="1.9" stroke-linecap="round"/>
                        <circle cx="12" cy="15.6" r="1.5" fill="currentColor"/>
                    </svg>
                    <span class="bpm-num">{{ bpmDomain() }}</span>
                </div>
                <span class="meta-chip">{{ song.Genre }}</span>
                <div class="id-pill">ID {{ song.Id }}</div>
            </header>

            <!-- ── 标题 + 曲师 ── -->
            <h1 ref="titleEl" class="song-title" :style="{ fontSize: titleSize + 'px' }">{{ song.Title }}</h1>
            <div class="artist-line">{{ song.Artist }}</div>

            <!-- ── 双列 ── -->
            <div class="two-col">
                <div class="left-col">
                    <div class="cover-frame">
                        <img :src="`/assets/chunithm/cover/${song.Id}.png`"
                             @error="(e: any) => e.target.src = '/assets/chunithm/cover/0.png'"
                             class="cover-img" />
                    </div>

                    <div v-if="masterTol || ultimaTol" class="tolerance-section">
                        <div v-if="masterTol" class="tol-block master">
                            <div class="tol-head">
                                <span class="tol-head-title">RANK TOLERANCE</span>
                                <span class="tol-head-sub">MASTER</span>
                            </div>
                            <div class="tol-rows">
                                <div class="tol-cell">
                                    <img src="/assets/chunithm/pic/rank_sss.png" class="tol-icon" alt="SSS">
                                    <span class="tol-thresh">{{ masterTol.sss.attack }}</span>
                                    <span class="tol-delta">+{{ masterTol.sss.justice }}</span>
                                </div>
                                <div class="tol-cell">
                                    <img src="/assets/chunithm/pic/rank_sssp.png" class="tol-icon" alt="SSS+">
                                    <span class="tol-thresh">{{ masterTol.sssp.attack }}</span>
                                    <span class="tol-delta">+{{ masterTol.sssp.justice }}</span>
                                </div>
                            </div>
                        </div>
                        <div v-if="ultimaTol" class="tol-block ultima">
                            <div class="tol-head">
                                <span class="tol-head-title">RANK TOLERANCE</span>
                                <span class="tol-head-sub">ULTIMA</span>
                            </div>
                            <div class="tol-rows">
                                <div class="tol-cell">
                                    <img src="/assets/chunithm/pic/rank_sss.png" class="tol-icon" alt="SSS">
                                    <span class="tol-thresh">{{ ultimaTol.sss.attack }}</span>
                                    <span class="tol-delta">+{{ ultimaTol.sss.justice }}</span>
                                </div>
                                <div class="tol-cell">
                                    <img src="/assets/chunithm/pic/rank_sssp.png" class="tol-icon" alt="SSS+">
                                    <span class="tol-thresh">{{ ultimaTol.sssp.attack }}</span>
                                    <span class="tol-delta">+{{ ultimaTol.sssp.justice }}</span>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>

                <!-- ── 谱面表格 ── -->
                <div class="right-col">
                    <div class="section-head">
                        <span class="section-tag">谱面信息</span>
                        <span class="chart-data-label">CHART DATA</span>
                        <span class="section-line"></span>
                        <span class="section-count">{{ chartCount }} CHARTS</span>
                    </div>

                    <div class="chart-rows">
                        <div v-for="(c, i) in song.Beatmaps" :key="i"
                             class="chart-row"
                              :class="{ 'row-ultima': c.LevelName === 'ULTIMA' }"
                              :style="c.LevelName !== 'ULTIMA' ? { boxShadow: `inset 4px 0 0 ${DIFF_COLORS[c.LevelName] || '#fff'}` } : {}">
                            <div class="chart-row-top">
                                <div class="diff-chip">
                                    <span class="diff-name"
                                          :style="c.LevelName === 'ULTIMA'
                                            ? { backgroundColor: '#000', color: '#fff' }
                                            : { backgroundColor: DIFF_COLORS[c.LevelName] || '#888', color: '#fff' }">
                                        {{ c.LevelName }}
                                    </span>
                                    <span class="diff-level"
                                          :style="c.LevelName === 'ULTIMA'
                                            ? { backgroundColor: '#000', color: '#fff' }
                                            : { backgroundColor: DIFF_LIGHT[c.LevelName] || '#555', color: '#222' }">
                                        {{ c.LevelStr }}
                                    </span>
                                </div>
                                <div class="charter-text">{{ c.Charter }}</div>
                            </div>
                            <div v-if="c.Constant > 0" class="const-badge"
                                 :class="{ 'const-ultima': c.LevelName === 'ULTIMA' }"
                                 :style="c.LevelName === 'ULTIMA' ? {} : { color: DIFF_COLORS[c.LevelName] || '#fff' }">
                                {{ c.Constant.toFixed(1) }}
                            </div>
                            <div class="note-row">
                                <div class="note-cell">
                                    <div class="note-label">COMBO</div>
                                    <div class="note-value">{{ c.MaxCombo.toLocaleString() }}</div>
                                </div>
                                <div class="note-cell" v-if="c.MaxCombo > 0">
                                    <div class="note-label">MISS</div>
                                    <div class="note-value note-loss-value">{{ (10000 / c.MaxCombo * 101).toFixed(1) }}</div>
                                </div>
                                <div class="note-cell" v-if="c.MaxCombo > 0">
                                    <div class="note-label">ATK</div>
                                    <div class="note-value note-loss-value">{{ (10000 / c.MaxCombo * 51).toFixed(1) }}</div>
                                </div>
                                <div class="note-cell" v-if="c.MaxCombo > 0">
                                    <div class="note-label">JST</div>
                                    <div class="note-value note-loss-value">{{ (10000 / c.MaxCombo).toFixed(1) }}</div>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>

            <span class="footer-text">MARISA BOT · CHUNITHM SONG</span>
        </div>
    </div>
</template>

<style scoped lang="postcss">
.chu-song { color: #fff; background-color: #0e0418; position: relative; overflow: hidden; }

.chu-song img { max-width: none; }

.stripe-layer {
    position: absolute;
    inset: 0;
    background: repeating-linear-gradient(-38deg, rgba(255,255,255,0.028) 0 3px, transparent 3px 26px);
    pointer-events: none;
    z-index: 0;
}

.inner { position: relative; z-index: 1; padding: 44px 48px 32px 48px; }

/* ── 顶栏 ── */
.top-bar { display: flex; align-items: center; gap: 12px; margin-bottom: 14px; margin-left: 44px; margin-right: 44px; }

.ver-logo { height: 90px; width: auto; filter: drop-shadow(0 2px 4px rgba(0,0,0,0.3)); }

.version-tag {
    font-weight: bold; font-size: 20px; letter-spacing: 0.15em; color: #fff;
    background: linear-gradient(135deg, #5a2878 0%, #3a1550 100%);
    padding: 6px 20px; border-radius: 9999px;
    box-shadow: 0 2px 10px rgba(90, 40, 120, 0.4);
}

.flex-1 { flex: 1; }

.bpm-pill {
    display: inline-flex; align-items: center; gap: 9px; color: #fff;
    background: rgba(35, 37, 69, 0.82); padding: 5px 18px 5px 14px;
    border-radius: 9999px; box-shadow: 0 4px 12px rgba(35, 37, 69, 0.25);
}

.bpm-icon { width: 20px; height: 20px; }

.bpm-num {
    font-family: 'Torus', sans-serif; font-weight: bold;
    font-size: 22px; line-height: 1.2;
}

.meta-chip {
    font-family: 'SEGA NewRodin', 'SEGA Maru Gothic', sans-serif;
    font-weight: bold; font-size: 17px; letter-spacing: 0.05em; color: #454867;
    padding: 5px 16px; border-radius: 9999px;
    background: rgba(255,255,255,0.72);
    box-shadow: 0 0 0 1px rgba(255,255,255,0.85), 0 3px 10px rgba(90,40,120,0.15);
    backdrop-filter: blur(8px);
}

.id-pill {
    font-family: 'Torus', sans-serif; font-weight: bold; font-size: 22px;
    letter-spacing: 0.08em; color: #fff; background: rgba(35, 37, 69, 0.82);
    padding: 5px 18px; border-radius: 9999px;
    box-shadow: 0 4px 12px rgba(35, 37, 69, 0.25);
}

/* ── 标题 + 曲师 ── */
.song-title {
    font-family: 'SEGA NewRodin', 'SEGA Maru Gothic', 'LXGW WenKai', sans-serif;
    font-weight: 700; line-height: 1; color: #fff;
    text-shadow: 0 2px 4px rgba(0,0,0,0.5), 0 4px 22px rgba(0,0,0,0.4);
    white-space: nowrap; overflow: hidden;
    padding-block: 8px 10px; margin-block: -8px -10px;
    margin-left: 44px;
}

.artist-line {
    font-family: 'Torus', 'SEGA Maru Gothic', 'LXGW WenKai', sans-serif;
    font-size: 23px; font-weight: bold; line-height: 33px;
    color: rgba(255,255,255,0.82);
    margin-left: 44px; margin-top: 28px; margin-bottom: 26px;
}

/* ── 双列 ── */
.two-col { display: flex; gap: 28px; margin: 0 44px; }

.left-col { width: 452px; flex-shrink: 0; display: flex; flex-direction: column; }

.cover-frame {
    padding: 6px; border-radius: 30px; background: rgba(255,255,255,0.75);
    box-shadow: 0 0 0 1px rgba(255,255,255,0.8), 0 8px 20px -12px rgba(0,0,0,0.5);
}

.cover-img { display: block; width: 440px; height: 440px; object-fit: cover; border-radius: 24px; }

/* ── 容错 ── */
.tolerance-section { display: flex; flex-direction: column; gap: 12px; margin-top: 20px; }

.tol-block { border-radius: 12px; background: rgba(255,255,255,0.05); overflow: hidden; box-shadow: inset 3px 0 0 var(--tc-accent); }
.tol-block.master { --tc-accent: rgb(198, 79, 228); }
.tol-block.ultima { --tc-accent: #FF3A3A; }

.tol-head { display: flex; align-items: baseline; padding: 10px 16px 6px 16px; }

.tol-head-title {
    font-family: 'Microsoft YaHei', sans-serif; font-weight: bold; font-size: 15px;
    letter-spacing: 0.08em; color: rgba(255,255,255,0.5);
}

.tol-head-sub {
    font-family: 'Microsoft YaHei', sans-serif; font-weight: bold; font-size: 16px;
    color: #fff; margin-left: 8px;
}

.tol-rows { display: flex; padding: 4px 0 10px 0; }

.tol-cell { flex: 1; display: flex; align-items: baseline; justify-content: center; gap: 8px; }

.tol-icon { height: 28px; width: auto; filter: drop-shadow(0 2px 3px rgba(0,0,0,0.4)); }

.tol-thresh { font-weight: bold; font-size: 30px; color: #22c55e; }

.tol-delta { font-weight: bold; font-size: 26px; color: #f59e0b; }

/* ── 谱面表格 ── */
.right-col { flex: 1; min-width: 0; }

.section-head { display: flex; align-items: center; gap: 12px; height: 44px; margin-bottom: 14px; }

.section-tag {
    font-family: 'Microsoft YaHei', sans-serif; font-weight: bold; font-size: 22px;
    letter-spacing: 0.1em; background: #5a2878; color: #fff; border-radius: 9999px;
    padding: 4px 20px; box-shadow: 0 0 0 2px rgba(255,255,255,0.7), 0 3px 10px rgba(90,40,120,0.3);
}

.chart-data-label {
    font-family: 'SEGA NewRodin', 'SEGA Maru Gothic', sans-serif;
    font-weight: 900; font-size: 20px; letter-spacing: 0.25em;
    color: rgba(255,255,255,0.75); white-space: nowrap;
}

.section-line { flex: 1; height: 2px; border-radius: 10px; background: rgba(255,255,255,0.15); }

.section-count { font-weight: bold; font-size: 17px; letter-spacing: 0.18em; color: rgba(255,255,255,0.5); }

.chart-rows { display: flex; flex-direction: column; gap: 10px; }

.chart-row { position: relative; height: 112px; border-radius: 10px; overflow: hidden; background: rgba(255,255,255,0.04); }

.row-ultima { border-left: 4px solid #FF3A3A; }

.chart-row-top { display: flex; align-items: stretch; }

.diff-chip {
    display: inline-flex; align-items: stretch; border-radius: 10px 0 14px 0;
    overflow: hidden; box-shadow: 0 2px 8px rgba(0,0,0,0.35);
}

.diff-name {
    display: flex; align-items: center; padding: 0 18px; font-weight: bold;
    font-size: 20px; letter-spacing: 0.06em; color: #fff;
    width: 136px; justify-content: center; box-sizing: border-box;
}

.diff-level {
    display: flex; align-items: center; padding: 0 18px; font-weight: bold;
    font-size: 22px; width: 64px; justify-content: center; box-sizing: border-box;
}

.charter-text {
    display: flex; align-items: center; margin-left: auto; margin-right: 14px;
    font-weight: bold; color: #fff; white-space: nowrap; overflow: hidden;
}

.const-badge {
    position: absolute; right: 12px; bottom: 10px; font-weight: bold; font-size: 30px;
    padding: 4px 14px; border-radius: 12px; background: rgba(0,0,0,0.5);
    text-shadow: 0 0 8px currentColor; box-shadow: 0 0 12px rgba(255,255,255,0.15);
}

.const-ultima {
    color: #fff;
    border: 2px solid #FF3A3A;
    box-shadow: none;
    text-shadow: none;
}

.note-row {
    position: absolute; left: 14px; right: 120px; top: 44px; bottom: 0;
    display: flex; align-items: flex-end; justify-content: space-evenly; padding-bottom: 12px;
}

.note-cell { display: flex; flex-direction: column; align-items: center; }

.note-label { font-size: 13px; color: rgba(255,255,255,0.6); letter-spacing: 0.1em; margin-bottom: 2px; }

.note-value { font-weight: bold; font-size: 22px; color: #fff; }

.note-loss-value { font-size: 18px; color: rgba(255,255,255,0.65); font-family: 'Torus', sans-serif; }

.footer-text {
    display: block; margin-top: 20px; margin-left: 44px; font-size: 15px;
    letter-spacing: 0.25em; color: rgba(255,255,255,0.25);
}
</style>
