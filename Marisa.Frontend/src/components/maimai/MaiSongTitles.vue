<template>
    <MaiCardShell v-if="song" class="mai-titles" :bg-key="topKey" :accent="themeMain">
            <!-- ── top meta bar（比 info 卡精简：只留类型徽章 + ID） ── -->
            <MaiSongMetaBar :from="song.From" :type="song.Type" :song-id="song.Id"/>

            <!-- ── cover + 标题 ── -->
            <div class="flex items-end gap-5 mt-7">
                <MaiCover :song-id="song.Id" :size="112" :frame-radius="19" :img-radius="14"/>
                <MaiSongHeading :title="song.Title" :artist="song.Artist" :max="72" :min="26" :player="player.Nickname"/>
            </div>

            <!-- ── section tag ── -->
            <div class="flex items-center gap-4 mt-8 mb-5">
                <span class="font-rodin text-[18px] tracking-[0.28em] text-white/70 whitespace-nowrap">TITLES</span>
                <div class="flex-1 h-[2px] rounded-full bg-white/20"></div>
                <span class="tt-stat tabular-nums">{{ achievedCount }} / {{ songTitles.length }} 已达成</span>
            </div>

            <!-- ── 称号列表：底板 + 条件 + 达成状态（— = 查分器数据无法判定） ── -->
            <div class="tt-list">
                <div v-for="t in songTitles" :key="t.id" class="tt-row">
                    <span class="tt-plate" :style="titlePlate(t)">{{ t.name }}</span>
                    <span class="tt-cond">{{ t.text }}</span>
                    <span v-if="titleAchieved(t) === true" class="tt-state tt-ok">已达成</span>
                    <span v-else-if="titleAchieved(t) === false" class="tt-state tt-no">未达成</span>
                    <span v-else class="tt-state tt-na">—</span>
                </div>
            </div>

            <footer class="mt-7">
                <span class="footer-text">MARISA BOT · SONG TITLES</span>
            </footer>
    </MaiCardShell>
</template>

<script setup lang="ts">
import {computed, ref} from 'vue'
import axios from 'axios'
import {useRoute} from 'vue-router'
import {context_get} from '@/GlobalVars'
import {achievementOrdinal, dxScoreStar, fcOrdinal, fsOrdinal} from '@/components/maimai/utils/ordinal'
import {themeMainOf, bgKeyOf, isUtageId} from '@/components/maimai/utils/song_card'
import MaiCardShell from '@/components/maimai/MaiCardShell.vue'
import MaiSongMetaBar from '@/components/maimai/MaiSongMetaBar.vue'
import MaiSongHeading from '@/components/maimai/MaiSongHeading.vue'
import MaiCover from '@/components/maimai/MaiCover.vue'

interface ChartScore {
    LevelIndex: number; Level: string; Constant: number; Charter: string; MaxDx: number
    Played: boolean; Achievement: number | null; Rank: string | null; Ra: number | null
    Fc: string | null; Fs: string | null; DxScore: number | null
}
interface ScoreData {
    Song: { Id: number; Title: string; Type: string; Artist: string; Genre: string; Bpm: number; From: string; IsNew: boolean }
    Player: { Nickname: string }
    Charts: ChartScore[]
}

const route = useRoute()
const data  = ref<ScoreData | null>(null)

const song   = computed(() => data.value?.Song ?? null)
const player = computed(() => data.value?.Player ?? {Nickname: ''})
const charts = computed(() => data.value?.Charts ?? [])

axios.get(context_get, {params: {id: route.query.id, name: 'SongScore'}}).then(res => {
    data.value = typeof res.data === 'string' ? JSON.parse(res.data) : res.data
})

// ── 称号静态表（按歌 id 查，削除曲 key 不存在=天然排除） ──
interface SongTitle {
    id: number; name: string; rare: string; text: string; norm: string
    diffIdx: number
    check: { dim: string; lv: number; scope: string } | null
}

const titlesDb = ref<Record<string, SongTitle[]> | null>(null)
axios.get('/assets/maimai/song_titles.json')
    .then(r => { titlesDb.value = r.data })
    .catch(() => { titlesDb.value = {} })

const RARE_ORDER: Record<string, number> = {Rainbow: 0, Gold: 1, Silver: 2, Bronze: 3, Normal: 4}
const songTitles = computed<SongTitle[]>(() => {
    if (!song.value || !titlesDb.value) return []
    const list = titlesDb.value[String(song.value.Id)] ?? []
    return [...list].sort((a, b) => (RARE_ORDER[a.rare] ?? 9) - (RARE_ORDER[b.rare] ?? 9) || a.id - b.id)
})
const achievedCount = computed(() => songTitles.value.filter(t => titleAchieved(t) === true).length)

function titleOrd(c: ChartScore, dim: string): number {
    switch (dim) {
        case 'rank': return achievementOrdinal(c.Achievement ?? 0)
        case 'fc':   return fcOrdinal(c.Fc ?? '')
        case 'fs':   return fsOrdinal(c.Fs ?? '')
        case 'dx':   return dxScoreStar(c.DxScore ?? 0, c.MaxDx)
        default:     return c.Played ? 1 : 0
    }
}

function titleAchieved(t: SongTitle): boolean | null {
    if (!t.check) return null
    const pool = t.diffIdx >= 0 ? charts.value.filter(c => c.LevelIndex === t.diffIdx) : charts.value
    if (!pool.length) return false
    const hit = (c: ChartScore) => c.Played && titleOrd(c, t.check!.dim) >= t.check!.lv
    return t.check.scope === 'all' ? pool.every(hit) : pool.some(hit)
}

const PIC = '/assets/maimai/pic'
function titlePlate(t: SongTitle) { return {backgroundImage: `url(${PIC}/shougou/UI_CMN_Shougou_${t.rare}.png)`} }

const isUtage = computed(() => isUtageId(song.value?.Id ?? 0))
const topIdx = computed(() => Math.max(0, charts.value.length - 1))
const topKey = computed(() => bgKeyOf(topIdx.value, isUtage.value))
const themeMain = computed(() => themeMainOf(topIdx.value, isUtage.value))
</script>

<style scoped lang="postcss" src="@/assets/css/maimai/song_card.pcss"/>

<style scoped lang="postcss">
.tabular-nums { font-variant-numeric: tabular-nums; }
.tt-stat { font-family: 'Torus','Microsoft YaHei',sans-serif; font-weight: bold; font-size: 15px; color: rgba(255,255,255,0.6); white-space: nowrap; }

/* ── 称号列表 ── */
.tt-list { display: flex; flex-direction: column; gap: 8px; }
/* 左侧不留内缩进，底板左缘与上方 TITLES 分隔行对齐（朋友 review 诉求） */
.tt-row { display: flex; align-items: center; gap: 16px; min-height: 44px; border-radius: 10px; padding: 7px 16px 7px 0; background: rgba(0,0,0,0.30); border: 1px solid rgba(255,255,255,0.08); }
/* 游戏内称号底板（UI_CMN_Shougou_* 276×36）：底层整图拉伸铺满（素材中段横向均匀，拉伸无损、
   只有端帽会变形），再用 ::before/::after 盖上按原比例缩放的端帽（素材实测端帽 16px，等比缩到
   高 32 后 ≈14px）。覆盖宽度取 24px ≥ 底层帽被拉到最宽时的 16/276×max-width ≈ 23.2px，保证长称号
   下拉变形的帽尾不外露；超出真实端帽的部分显示的是等比图的均匀中段，与底层无缝。端帽 z-index
   垫在文字之下，避免盖住贴边的文字。 */
/* line-height 27px（非 32）：雅黑字形在行盒内天然偏下 + 底板亮面因下缘厚唇偏上，实测文字比
   亮面中线低 2.5px，缩小行高把单行文字上提 (32-27)/2=2.5px 后与亮面竖直居中。 */
.tt-plate { position: relative; z-index: 0; flex: 0 0 auto; display: inline-block; min-width: 200px; max-width: 400px; height: 32px; line-height: 27px; padding: 0 22px; text-align: center; background-size: 100% 100%; font-family: 'Microsoft YaHei', sans-serif; font-weight: bold; font-size: 14px; color: #3b3b3b; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.tt-plate::before, .tt-plate::after { content: ''; position: absolute; z-index: -1; top: 0; bottom: 0; width: 24px; background-image: inherit; background-size: auto 100%; background-repeat: no-repeat; }
.tt-plate::before { left: 0; background-position: left center; }
.tt-plate::after { right: 0; background-position: right center; }
/* 超长条件（如歌曲组展开列全部曲名）完整换行展示，不省略 */
.tt-cond { flex: 1 1 auto; min-width: 0; font-family: 'Torus','Microsoft YaHei',sans-serif; font-weight: bold; font-size: 15px; line-height: 1.4; color: rgba(255,255,255,0.75); overflow-wrap: break-word; }
.tt-state { flex: 0 0 auto; font-family: 'Torus','Microsoft YaHei',sans-serif; font-weight: bold; font-size: 14px; letter-spacing: 0.08em; }
.tt-ok { color: #ffd700; }
.tt-no { color: rgba(255,255,255,0.38); }
.tt-na { color: rgba(255,255,255,0.22); }
</style>
