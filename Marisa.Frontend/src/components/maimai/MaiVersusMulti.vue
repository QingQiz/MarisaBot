<template>
    <MaiCardShell v-if="data" class="mai-versus-multi" :bg-key="bgKey" :accent="accent" :style="{'--accent': accent}">
        <MaiSongMetaBar :from="song.From" :type="song.Type" :song-id="song.Id"
                        :bpm="song.Bpm" :genre="song.Genre" :is-new="song.IsNew"/>
        <div class="flex items-end gap-5 mt-7">
            <MaiCover :song-id="song.Id" :size="132" :frame-radius="22" :img-radius="16"/>
            <MaiSongHeading :title="song.Title" :artist="song.Artist" :max="84" :min="1"
                            :artist-size="20" :artist-top="12"/>
        </div>
        <div class="flex items-center gap-4 mt-8 mb-4">
            <span class="section-tag">多人对战</span>
            <span class="difficulty" :style="{color: diffColor}">{{ diffName }} {{ data.Chart.Level }}<small>{{ data.Chart.Constant.toFixed(1) }}</small></span>
            <div class="flex-1 h-[2px] rounded-full bg-white/20"></div>
            <span v-if="data.Round" class="round-badge">ROUND<b>{{ pad(data.Round) }}</b></span>
        </div>

        <div class="racers">
            <div v-for="(player, index) in data.Players" :key="index" class="racer mai-accent-rail"
                 :class="{leader: player.Place === 1, unplayed: player.Place == null}"
                 :style="{'--rail-color': placeColor(player.Place)}">
                <div class="place" :style="{color: placeColor(player.Place)}">
                    <template v-if="player.Place != null"><b>{{ player.Place }}</b><small>{{ placeSuffix(player.Place) }}</small></template>
                    <b v-else>—</b>
                </div>
                <div class="who" :class="{stamped: player.Stamp}">
                    <span class="name">{{ player.Name }}</span>
                    <span v-if="player.Score.State === 'played'" class="marks">
                        <img v-if="player.Score.Fc" :src="markIcon(player.Score.Fc)" alt="">
                        <img v-if="player.Score.Fs" :src="markIcon(player.Score.Fs)" alt="">
                        <img v-if="stars(player.Score)" :src="starIcon(stars(player.Score))" alt="">
                    </span>
                    <span v-if="player.Stamp" class="stamp-slot"><span class="loser-stamp">菜</span></span>
                </div>
                <template v-if="player.Score.State === 'played'">
                    <div class="achv">
                        <div class="achievement">{{ player.Score.Achievement!.toFixed(4) }}<small>%</small></div>
                        <div v-if="player.Place! > 1" class="gap">−{{ (best - player.Score.Achievement!).toFixed(4) }}%</div>
                    </div>
                    <img class="rank" :src="rankIcon(player.Score.Rank)" alt="">
                    <div class="dx">
                        <span>DX SCORE</span>
                        <b>{{ player.Score.DxScore }}<small>/{{ data.Chart.MaxDx }}</small></b>
                        <span class="ra">Ra <b>{{ player.Score.Rating }}</b></span>
                    </div>
                </template>
                <div v-else class="no-play">No Play Record</div>
            </div>
        </div>

        <MaiVersusStandings v-if="data.Standings.length" :standings="data.Standings" :rounds="data.Round" :accent="accent"/>

        <footer class="mt-7 flex items-end justify-between">
            <span class="footer-text">MARISA BOT · VERSUS</span>
            <span v-if="data.Round" class="next-hint">发送「开始」进行下一轮</span>
        </footer>
    </MaiCardShell>
</template>

<script setup lang="ts">
import {computed, ref} from 'vue'
import axios from 'axios'
import {useRoute} from 'vue-router'
import {context_get} from '@/GlobalVars'
import {dxScoreStar} from '@/components/maimai/utils/ordinal'
import {DIFF_COLORS, DIFF_NAMES, bgKeyOf, themeMainOf} from '@/components/maimai/utils/song_card'
import {markIcon, placeColor, placeSuffix, rankIcon, starIcon, type VersusChart, type VersusScore, type VersusStanding} from '@/components/maimai/utils/versus'
import MaiCardShell from '@/components/maimai/MaiCardShell.vue'
import MaiSongMetaBar from '@/components/maimai/MaiSongMetaBar.vue'
import MaiSongHeading from '@/components/maimai/MaiSongHeading.vue'
import MaiCover from '@/components/maimai/MaiCover.vue'
import MaiVersusStandings from '@/components/maimai/MaiVersusStandings.vue'

interface Player { Name: string; Place: number | null; Score: VersusScore; Stamp: boolean }
interface MultiData { Chart: VersusChart; Round: number; Players: Player[]; Standings: VersusStanding[] }

const route = useRoute()
const data = ref<MultiData | null>(null)
axios.get(context_get, {params: {id: route.query.id, name: 'versusMulti'}}).then(res => {
    data.value = typeof res.data === 'string' ? JSON.parse(res.data) : res.data
})
const song = computed(() => data.value!.Chart.Song)
const levelIndex = computed(() => data.value?.Chart.LevelIndex ?? 3)
const bgKey = computed(() => bgKeyOf(levelIndex.value, false))
const accent = computed(() => themeMainOf(levelIndex.value, false))
const diffName = computed(() => DIFF_NAMES[levelIndex.value])
const diffColor = computed(() => DIFF_COLORS[levelIndex.value])
const best = computed(() => Math.max(...data.value!.Players.map(p => p.Score.Achievement ?? 0)))
function stars(score: VersusScore) { return dxScoreStar(score.DxScore ?? 0, data.value?.Chart.MaxDx ?? 0) }
function pad(n: number) { return n.toString().padStart(2, '0') }
</script>

<style scoped lang="postcss" src="@/assets/css/maimai/song_card.pcss"/>
<style scoped lang="postcss">
.section-tag { font-family:'Microsoft YaHei',sans-serif; font-weight:bold; font-size:21px; letter-spacing:.1em; border-radius:9999px; padding:4px 20px; background:var(--accent); color:#fff; box-shadow:0 0 0 2px rgba(255,255,255,.8); white-space:nowrap; }
.difficulty { font:900 23px 'SEGA NewRodin',sans-serif; letter-spacing:.03em; white-space:nowrap; }
.difficulty small { margin-left:10px; font:700 17px 'Torus',sans-serif; color:rgba(255,255,255,.7); }
.round-badge { display:flex; align-items:baseline; gap:6px; font:700 12px 'Torus',sans-serif; letter-spacing:.24em; color:rgba(255,255,255,.55); }
.round-badge b { font-size:26px; letter-spacing:.04em; color:#fff; }

.racers { display:flex; flex-direction:column; gap:8px; }
.racer {
    --rail-opacity:.85;
    display:grid;
    grid-template-columns:64px minmax(0,1fr) 196px 72px 112px;
    align-items:center;
    column-gap:14px;
    min-height:78px;
    padding:10px 18px 10px 22px;
    border:1px solid rgba(255,255,255,.13);
    border-radius:14px;
    background:linear-gradient(105deg,rgba(8,8,16,.6),rgba(8,8,16,.28));
    overflow:hidden;
}
.racer.leader {
    min-height:92px;
    border-color:rgba(255,227,140,.7);
    background:linear-gradient(105deg,rgba(255,215,94,.16),rgba(8,8,16,.35) 46%),linear-gradient(105deg,rgba(8,8,16,.55),rgba(8,8,16,.3));
    box-shadow:inset 0 0 0 1px rgba(255,255,255,.08),0 0 24px rgba(255,215,94,.22);
}
.racer.unplayed { --rail-opacity:.3; background:rgba(8,8,16,.35); }
.place { display:flex; align-items:baseline; justify-content:center; gap:2px; font-family:'Torus',sans-serif; }
.place b { font-size:34px; font-weight:900; line-height:1; }
.place small { font-size:12px; font-weight:700; letter-spacing:.06em; }
.leader .place b { font-size:42px; text-shadow:0 0 14px rgba(255,215,94,.45); }
.who { position:relative; min-width:0; display:flex; flex-direction:column; gap:6px; }
.who.stamped { padding-right:62px; }
.name { overflow:hidden; text-overflow:ellipsis; white-space:nowrap; font:700 20px 'Microsoft YaHei',sans-serif; }
.leader .name { font-size:22px; }
.marks { display:flex; align-items:center; gap:6px; height:24px; }
.marks img { display:block; max-height:24px; max-width:60px; }
.achv { display:flex; flex-direction:column; align-items:flex-end; gap:3px; }
.achievement { font:900 30px/1 'Torus',sans-serif; font-variant-numeric:tabular-nums; letter-spacing:.01em; }
.leader .achievement { font-size:34px; color:#ffe27a; }
.achievement small { margin-left:2px; font-size:15px; opacity:.6; }
.gap { font:700 12px 'Torus',sans-serif; font-variant-numeric:tabular-nums; color:rgba(255,255,255,.42); }
.rank { width:72px; height:32px; object-fit:contain; display:block; }
.dx { display:flex; flex-direction:column; align-items:flex-end; gap:1px; white-space:nowrap; }
.dx>span { font:700 10px 'Torus',sans-serif; letter-spacing:.06em; color:rgba(255,255,255,.45); }
.dx>b { font:800 17px 'Torus',sans-serif; font-variant-numeric:tabular-nums; }
.dx small { margin-left:3px; font-size:11px; color:rgba(255,255,255,.5); }
.dx .ra b { font-size:12px; color:rgba(255,255,255,.8); }
.no-play { grid-column:3 / 6; justify-self:end; padding-right:6px; font:700 18px 'SEGA NewRodin',sans-serif; letter-spacing:.22em; color:rgba(255,255,255,.3); }
.stamp-slot { position:absolute; right:0; top:50%; width:50px; height:50px; margin-top:-25px; --stamp-rotate:-12deg; }
.stamp-slot .loser-stamp { font-size:32px; }
.next-hint { font:700 13px 'Microsoft YaHei',sans-serif; color:rgba(255,255,255,.55); }
</style>
