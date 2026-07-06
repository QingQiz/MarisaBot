<template>
    <MaiCardShell v-if="course" class="mai-dan" :bg-key="bgKey" :accent="accent">
        <!-- ── top meta bar：日服版本 logo（国服年版一档罩两代，区分不了 0.05 版本档） ── -->
        <header class="flex items-center gap-2 flex-nowrap whitespace-nowrap">
            <img :src="verLogo" @error="verLogo = LOGO_FALLBACK" :alt="verMeta.name" :style="logoStyle"
                 class="h-[50px] shrink-0 drop-shadow-[0_3px_8px_rgba(0,0,0,0.4)]">
            <div class="flex-1"></div>
            <span v-if="isFuture" class="future-chip" :style="{ color: accent, borderColor: accent }">{{ FUTURE_LABEL }}</span>
            <img :src="modeBadge" :alt="modeName" class="h-[52px] drop-shadow-[0_3px_8px_rgba(0,0,0,0.4)]">
        </header>

        <!-- ── dan heading（游戏内段位艺术字，已裁 alpha bbox） ── -->
        <div class="flex items-center justify-between gap-6 mt-5">
            <img :src="daniTitleSrc" :alt="dani" class="h-[92px] drop-shadow-[0_4px_14px_rgba(0,0,0,0.45)]">
            <div class="ver-name shrink-0">{{ verMeta.name }}</div>
        </div>

        <!-- ── section divider ── -->
        <div class="flex items-center gap-4 mt-5 mb-3">
            <span class="font-rodin text-[20px] tracking-[0.28em] text-white/70 whitespace-nowrap">DAN COURSE</span>
            <div class="flex-1 h-[2px] rounded-full bg-white/20"></div>
        </div>

        <!-- ── 判定规则行 ── -->
        <div class="flex items-center gap-2.5 mb-4">
            <span class="rule-chip"><img :src="lifeIcon" alt="" class="h-[17px] inline-block align-[-3px] mr-[2px]">LIFE <b class="tabular-nums">{{ course.life }}</b></span>
            <span class="rule-chip">回复 <b class="tabular-nums">+{{ course.recover }}</b></span>
            <div class="flex-1"></div>
            <span v-for="d in damages" :key="d.label" class="dmg-box">
                <img :src="d.box" :alt="d.label" class="h-[26px]">
                <b class="dmg-num tabular-nums">−{{ d.value }}</b>
            </span>
        </div>

        <!-- ── 4 tracks ── -->
        <div class="flex flex-col gap-[10px]">
            <div v-for="(s, i) in course.songs" :key="i" class="track-row">
                <span class="track-no font-rodin">{{ i + 1 }}</span>
                <MaiCover :song-id="s.id" :size="72" :frame-radius="13" :img-radius="9"/>
                <div class="flex-1 min-w-0">
                    <div class="t-title">{{ s.title }}</div>
                    <div class="t-artist">{{ s.artist }}</div>
                </div>
                <div class="shrink-0 text-right">
                    <div class="diff-chip" :style="{ color: diffColor(s), borderColor: diffColor(s) }">
                        {{ DIFF_NAMES[s.diffIdx] }} {{ s.level ?? '—' }}<template v-if="s.constant != null"> · {{ s.constant.toFixed(1) }}</template>
                    </div>
                    <div v-if="s.charter !== '-'" class="t-charter">{{ s.charter }}</div>
                </div>
            </div>
        </div>

        <footer class="mt-6 flex items-baseline justify-between gap-4">
            <span class="foot-note">谱面定数与谱师为现行数据（水鱼查分器口径）</span>
            <span class="footer-text shrink-0">MARISA BOT · DAN COURSE</span>
        </footer>
    </MaiCardShell>

    <div v-else-if="loaded" class="mai-dan mai-card w-[840px] px-12 py-10 antialiased">
        <div class="text-[26px] font-bold">未找到该段位数据</div>
    </div>
</template>

<script setup lang="ts">
import {computed, ref, watchEffect} from 'vue'
import axios from 'axios'
import {useRoute} from 'vue-router'
import MaiCardShell from '@/components/maimai/MaiCardShell.vue'
import MaiCover from '@/components/maimai/MaiCover.vue'
import {DIFF_NAMES, DIFF_COLORS, themeMainOf, bgKeyOf} from '@/components/maimai/utils/song_card'

interface DanSong {
    id: number; title: string; diffIdx: number; level: string | null
    constant: number | null; charter: string; artist: string
}
interface DanCourse {
    version: string; mode: number; dani: string; daniId: number
    life: number; recover: number; damage: number[]; songs: DanSong[]
}

// 版本档 → 显示名 + 日服 logo 素材号 + 素材左侧透明留白（alpha bbox 实测，用于视觉左对齐）
const VER_META: Record<string, { name: string; code: number; trim: number }> = {
    '1.17': {name: 'Splash PLUS',   code: 215, trim: 8},
    '1.20': {name: 'UNiVERSE',      code: 220, trim: 24},
    '1.25': {name: 'UNiVERSE PLUS', code: 225, trim: 23},
    '1.30': {name: 'FESTiVAL',      code: 230, trim: 19},
    '1.35': {name: 'FESTiVAL PLUS', code: 235, trim: 19},
    '1.40': {name: 'BUDDiES',       code: 240, trim: 42},
    '1.45': {name: 'BUDDiES PLUS',  code: 245, trim: 45},
    '1.50': {name: 'PRiSM',         code: 250, trim: 29},
    '1.55': {name: 'PRiSM PLUS',    code: 255, trim: 1},
    '1.60': {name: 'CiRCLE',        code: 260, trim: 17},
    '1.65': {name: 'CiRCLE PLUS',   code: 265, trim: 20},
}
const LOGO_FALLBACK = '/assets/maimai/version/maimaidx.png'
const FUTURE_VERSIONS = ['1.60', '1.65']
const FUTURE_LABEL = '未来版本'

const route = useRoute()
const ver  = String(route.query.ver ?? '1.55')
const dani = String(route.query.dani ?? '')

const course = ref<DanCourse | null>(null)
const loaded = ref(false)
axios.get('/assets/maimai/dan_courses.json').then(r => {
    course.value = (r.data as DanCourse[]).find(c => c.version === ver && c.dani === dani) ?? null
    loaded.value = true
})

const verMeta  = computed(() => VER_META[ver] ?? {name: ver, code: 0, trim: 0})
const verLogo  = ref('')
watchEffect(() => { verLogo.value = `/assets/maimai/version/jp/Ver${verMeta.value.code}.png` })
const logoStyle = computed(() => ({marginLeft: `${(-verMeta.value.trim * (50 / 160)).toFixed(1)}px`}))
const isFuture = computed(() => FUTURE_VERSIONS.includes(ver))
const modeName = computed(() => course.value?.mode === 2 ? '真段位認定' : '段位認定')

// 游戏内素材：段位艺术字按 daniId 对齐（01-10 普通 / 12-23 真系），圆章/心形按模式取色
const DAN = '/assets/maimai/dan'
const daniTitleSrc = computed(() => `${DAN}/UI_DNM_DaniTitle_${String(course.value?.daniId ?? 1).padStart(2, '0')}.png`)
const modeBadge = computed(() => course.value?.mode === 2
    ? `${DAN}/UI_DNM_Separator_ShinDani_Right.png`
    : `${DAN}/UI_DNM_Separator_Dani_Right.png`)
const lifeIcon = computed(() => course.value?.mode === 2
    ? `${DAN}/UI_DNM_Icon_Life_04.png`
    : `${DAN}/UI_DNM_Icon_Life_01.png`)

// PERFECT 全 course 均不扣血，官方胶囊只有 GREAT/GOOD/MISS 三件（数值叠加在胶囊右段）
const DAMAGE_BOXES = [null, 'UI_DNM_Box_Damage_01.png', 'UI_DNM_Box_Damage_02.png', 'UI_DNM_Box_Damage_03.png']
const DAMAGE_LABELS = ['PERFECT', 'GREAT', 'GOOD', 'MISS']
const damages = computed(() =>
    (course.value?.damage ?? [])
        .map((value, i) => ({label: DAMAGE_LABELS[i], value, box: `${DAN}/${DAMAGE_BOXES[i]}`}))
        .filter((d, i) => d.value > 0 && i > 0))

const topIdx = computed(() => Math.max(...(course.value?.songs.map(s => s.diffIdx) ?? [0])))
const bgKey  = computed(() => bgKeyOf(topIdx.value, false))
const accent = computed(() => themeMainOf(topIdx.value, false))

function diffColor(s: DanSong) { return DIFF_COLORS[s.diffIdx] ?? '#999' }
</script>

<style scoped lang="postcss" src="@/assets/css/maimai/song_card.pcss"/>

<style scoped lang="postcss">
.future-chip { font-family: 'Microsoft YaHei', sans-serif; font-weight: bold; font-size: 13px; padding: 3px 12px; border: 2px solid; border-radius: 9999px; background: rgba(8,8,16,0.4); white-space: nowrap; }

.ver-name { font-family: 'SEGA NewRodin','LXGW WenKai',sans-serif; font-weight: 700; font-size: 26px; color: rgba(255,255,255,0.65); text-shadow: 0 2px 4px rgba(0,0,0,0.4); white-space: nowrap; }

.rule-chip { font-family: 'Torus','Microsoft YaHei',sans-serif; font-size: 13px; font-weight: bold; color: rgba(255,255,255,0.7); padding: 3px 11px; border-radius: 9999px; background: rgba(255,255,255,0.08); box-shadow: inset 0 0 0 1px rgba(255,255,255,0.14); white-space: nowrap; }
.rule-chip b { color: #fff; font-weight: 900; }

/* 官方扣血胶囊（104×24）：数值叠加在右段心形侧 */
.dmg-box { position: relative; display: inline-block; line-height: 0; }
.dmg-num { position: absolute; top: 50%; right: 10px; transform: translateY(-50%); font-family: 'Torus',sans-serif; font-size: 13px; font-weight: 900; color: #fff; line-height: 1; }

.track-row { display: flex; align-items: center; gap: 16px; padding: 10px 16px 10px 12px; border-radius: 18px; background: rgba(255,255,255,0.05); box-shadow: inset 0 0 0 1px rgba(255,255,255,0.07); }
.track-no { font-size: 26px; font-weight: 900; color: rgba(255,255,255,0.35); width: 24px; text-align: center; }
.t-title { font-family: 'SEGA NewRodin','LXGW WenKai',sans-serif; font-weight: 700; font-size: 23px; color: #fff; text-shadow: 0 2px 4px rgba(0,0,0,0.5); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.t-artist { font-family: 'Torus','SEGA Maru Gothic','LXGW WenKai',sans-serif; font-weight: bold; font-size: 14px; color: rgba(255,255,255,0.55); margin-top: 3px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.diff-chip { display: inline-block; font-family: 'SEGA NewRodin','LXGW WenKai',sans-serif; font-size: 15px; font-weight: 900; padding: 2px 12px; border: 2px solid; border-radius: 9999px; background: rgba(8,8,16,0.4); white-space: nowrap; }
.t-charter { font-family: 'Torus','Microsoft YaHei',sans-serif; font-weight: bold; font-size: 12.5px; color: rgba(255,255,255,0.45); margin-top: 4px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; max-width: 300px; }

</style>
