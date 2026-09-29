<template>
    <MaiCardShell v-if="data" class="mai-versus" :bg-key="bgKey" :accent="accent"
                  :style="{'--accent': accent, '--winner-glow': accent + '38'}">
        <MaiSongMetaBar :from="data.Song.From" :type="data.Song.Type" :song-id="data.Song.Id"
                        :bpm="data.Song.Bpm" :genre="data.Song.Genre" :is-new="data.Song.IsNew"/>
        <div class="flex items-end gap-5 mt-7">
            <MaiCover :song-id="data.Song.Id" :size="132" :frame-radius="22" :img-radius="16"/>
            <MaiSongHeading :title="data.Song.Title" :artist="data.Song.Artist" :max="84" :min="1"
                            :artist-size="20" :artist-top="12"/>
        </div>
        <div class="flex items-center gap-4 mt-8 mb-4">
            <span class="section-tag">玩家对战</span>
            <div class="flex-1 h-[2px] rounded-full bg-white/20"></div>
        </div>
        <div class="difficulty-line">
            <span class="difficulty" :style="{color: diffColor}">{{ diffName }} {{ data.Level }}</span>
        </div>
        <div class="players">
            <div v-for="(player, index) in data.Players" :key="index" class="player mai-accent-rail" :class="{winner: isWinner(index), loser: isRateLoser(index)}">
                <div class="player-head">
                    <span class="player-name">{{ player.Nickname }}</span>
                    <span class="player-status" aria-label="对战结果">
                        <b v-if="isWinner(index)" class="win-badge">WIN</b>
                        <span v-if="isRateLoser(index)" class="loser-stamp" aria-label="完成率落后">菜</span>
                    </span>
                </div>
                <template v-if="player.Played && player.Score">
                    <div class="score-main">
                        <div class="achievement">{{ player.Score.Achievement.toFixed(4) }}<small>%</small></div>
                        <div class="rank-slot"><img :src="rankIcon(player.Score)" class="rank" alt=""></div>
                    </div>
                    <div class="metrics">
                        <div><span>Ra</span><b>{{ player.Score.Rating }}</b></div>
                        <div><span>DX SCORE</span><b>{{ player.Score.DxScore }}<small>/{{ data.MaxDx }}</small></b></div>
                        <div><span>DX%</span><b>{{ dxRate(player.Score) }}%</b></div>
                    </div>
                    <div class="marks">
                        <img v-if="player.Score.Fc" :src="fcIcon(player.Score.Fc)" alt="">
                        <img v-if="player.Score.Fs" :src="fsIcon(player.Score.Fs)" alt="">
                        <img v-if="starN(player.Score)" :src="starIcon(player.Score)" alt="">
                    </div>
                </template>
                <div v-else class="unplayed"><span class="unplayed-mark">—</span>未游玩</div>
            </div>
        </div>
        <footer class="mt-7"><span class="footer-text">MARISA BOT · VERSUS</span></footer>
    </MaiCardShell>
</template>

<script setup lang="ts">
import {computed, ref} from 'vue'
import axios from 'axios'
import {useRoute} from 'vue-router'
import {context_get} from '@/GlobalVars'
import {dxScoreStar} from '@/components/maimai/utils/ordinal'
import {DIFF_NAMES, DIFF_COLORS, bgKeyOf, themeMainOf} from '@/components/maimai/utils/song_card'
import MaiCardShell from '@/components/maimai/MaiCardShell.vue'
import MaiSongMetaBar from '@/components/maimai/MaiSongMetaBar.vue'
import MaiSongHeading from '@/components/maimai/MaiSongHeading.vue'
import MaiCover from '@/components/maimai/MaiCover.vue'

interface Score { Achievement: number; Rank: string; Rating: number; DxScore: number; Fc: string; Fs: string }
interface Player { Nickname: string; Played: boolean; Score: Score | null }
interface VersusData { Song: {Id: number; Title: string; Type: string; Artist: string; Genre: string; Bpm: number; From: string; IsNew: boolean}; LevelIndex: number; Level: string; Constant: number; MaxDx: number; Players: Player[]; Winner: string; WinnerIndex: number }
const route = useRoute()
const data = ref<VersusData | null>(null)
axios.get(context_get, {params: {id: route.query.id, name: 'versus'}}).then(res => { data.value = typeof res.data === 'string' ? JSON.parse(res.data) : res.data })
const bgKey = computed(() => bgKeyOf(data.value?.LevelIndex ?? 3, false))
const accent = computed(() => themeMainOf(data.value?.LevelIndex ?? 3, false))
const diffName = computed(() => DIFF_NAMES[data.value?.LevelIndex ?? 3])
const diffColor = computed(() => DIFF_COLORS[data.value?.LevelIndex ?? 3])
const PIC = '/assets/maimai/pic'
function rankIcon(score: Score) { return `${PIC}/rank_${score.Rank.toLowerCase().replaceAll('+', 'p')}.png` }
function fcIcon(name: string) { return `${PIC}/icon_${name}.png` }
function fsIcon(name: string) { return `${PIC}/icon_${name}.png` }
function starN(score: Score) { return dxScoreStar(score.DxScore, data.value?.MaxDx ?? 0) }
function starIcon(score: Score) { return `${PIC}/music_icon_dxstar_${starN(score)}.png` }
function dxRate(score: Score) { return data.value?.MaxDx ? (score.DxScore / data.value.MaxDx * 100).toFixed(1) : '0.0' }
function isWinner(index: number) { return data.value?.WinnerIndex === index }
/**
 * The loser stamp is deliberately derived from Achievement only.  In
 * particular, DX score never participates in deciding whether the stamp is
 * shown.  A missing score is not treated as a zero completion rate: the stamp
 * is reserved for a real rate-vs-rate comparison.
 */
function isRateLoser(index: number) {
    const current = data.value?.Players[index]
    const other = data.value?.Players[1 - index]
    if (!current?.Played || !current.Score || !other?.Played || !other.Score) return false
    return current.Score.Achievement < other.Score.Achievement
}
</script>

<style scoped lang="postcss" src="@/assets/css/maimai/song_card.pcss"/>
<style scoped lang="postcss">
.section-tag { font-family: 'Microsoft YaHei',sans-serif; font-weight: bold; font-size: 21px; letter-spacing: .1em; border-radius: 9999px; padding: 4px 20px; background: var(--accent); color: #fff; box-shadow: 0 0 0 2px rgba(255,255,255,.8); white-space: nowrap; }
.difficulty-line { display:flex; align-items:center; margin-bottom:12px; min-height:32px; }
.difficulty { font-family:'SEGA NewRodin',sans-serif; font-size:25px; font-weight:900; letter-spacing:.03em; }
.players { display:grid; grid-template-columns:1fr 1fr; gap:14px; }
.mai-versus {
    --status-left:280.33px;
    --status-top:15.33px;
    --status-width:71.33px;
    --status-height:46px;
    --stamp-rotate:10deg;
    --rank-left:275px;
    --rank-top:99.67px;
    --rank-width:76px;
    --rank-height:34px;
}
.player { --rail-color:var(--accent); --rail-opacity:.5; min-width:0; min-height:228px; padding:18px 22px 16px; border:1px solid rgba(255,255,255,.15); border-radius:16px; background:linear-gradient(105deg,rgba(8,8,16,.58),rgba(8,8,16,.25)); overflow:hidden; }
.player-head {
    display:flex;
    align-items:center;
    min-height:var(--status-height);
    padding-right:76px;
    font:700 21px 'Microsoft YaHei',sans-serif;
}
.player-name { min-width:0; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
.player-status,
.rank-slot {
    position:absolute;
    display:flex;
    align-items:center;
    justify-content:center;
}
.player-status { left:var(--status-left); top:var(--status-top); width:var(--status-width); height:var(--status-height); }
.win-badge {
    width:100%;
    height:100%;
    display:flex;
    align-items:center;
    justify-content:center;
    font:700 14px 'Torus',sans-serif;
    letter-spacing:.12em;
    color:#ffe45c;
}
.loser-stamp {
    position:relative;
    z-index:2;
    width:100%;
    height:100%;
    display:grid;
    place-items:center;
    color:#fff4ed;
    background:#d55759;
    border:0;
    border-radius:5px;
    font-family:'Noto Serif JP',serif;
    font-size:30px;
    font-weight:800;
    line-height:1;
    letter-spacing:0;
    transform:rotate(var(--stamp-rotate));
    pointer-events:none;
    user-select:none;
}
.loser-stamp::before { content:''; position:absolute; inset:3px; border:0.75px solid #fff4ed; border-radius:2px; opacity:.95; }
.loser-stamp::after { content:''; position:absolute; inset:0; border:1px solid #f3aaa2; border-radius:5px; opacity:.7; }
.player.winner { --rail-opacity:1; border-color:rgba(255,255,255,.78); box-shadow:inset 0 0 0 1px rgba(255,255,255,.13),0 0 22px var(--winner-glow); }
/* Center the achievement on the fixed rank slot and keep the metrics below it. */
.score-main { margin-top:calc(var(--rank-top) + var(--rank-height) / 2 - 19.5px - 18px - var(--status-height)); display:flex; align-items:center; min-height:39px; padding-right:76px; }
.achievement { font:900 39px 'Torus',sans-serif; letter-spacing:.02em; line-height:1; }
.achievement small { margin-left:3px; font-size:17px; opacity:.65; }
.rank-slot { left:var(--rank-left); top:var(--rank-top); width:var(--rank-width); height:var(--rank-height); }
.rank { width:100%; height:100%; object-fit:contain; display:block; }
.metrics { display:grid; grid-template-columns:repeat(3,minmax(0,1fr)); gap:0; margin-top:15px; padding:9px 0 8px; border-top:1px solid rgba(255,255,255,.14); border-bottom:1px solid rgba(255,255,255,.14); }
.metrics div + div { border-left:1px solid rgba(255,255,255,.12); }
.metrics div { min-width:0; display:flex; flex-direction:column; align-items:center; text-align:center; gap:3px; padding:0 6px; }
.metrics span { font:700 11px 'Torus',sans-serif; letter-spacing:.04em; color:rgba(255,255,255,.5); white-space:nowrap; }
.metrics b { font:800 18px 'Torus',sans-serif; white-space:nowrap; }
.metrics small { margin-left:4px; font-size:11px; color:rgba(255,255,255,.55); }
.marks { margin-top:9px; height:32px; display:flex; align-items:center; justify-content:center; gap:8px; }
.marks img { display:block; max-height:32px; max-width:76px; }
.unplayed { height:142px; display:flex; align-items:center; justify-content:center; gap:10px; font:700 21px 'SEGA NewRodin',sans-serif; letter-spacing:.14em; color:rgba(255,255,255,.34); }
.unplayed-mark { font:400 35px 'Torus',sans-serif; color:rgba(255,255,255,.22); }
</style>

