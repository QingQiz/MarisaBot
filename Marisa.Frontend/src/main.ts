import {createApp} from 'vue'
import {createRouter, createWebHistory} from 'vue-router'

import '@/assets/css/tailwind.css'
import '@/ExtensionMethods'

import App from '@/App.vue'
import ChuBestScores from "@/components/chunithm/BestScores.vue";
import MaiBestScores from "@/components/maimai/BestScores.vue";
import OngekiSong from "@/components/ongeki/OngekiSong.vue";
import OsuScore from "@/components/osu/OsuScore.vue";
import NotFound from "@/components/NotFound.vue";
import Help from "@/components/help/Help.vue";
import OsuRecommend from "@/components/osu/OsuRecommend.vue";
import OsuPreview from "@/components/osu/OsuPreview.vue";
import Recommend from "@/components/maimai/Recommend.vue";
import MaiMaiSummary from "@/components/maimai/Summary.vue";
import {default as ChunithmSummary} from "@/components/chunithm/Summary.vue";
import OverPowerAll from "@/components/chunithm/OverPowerAll.vue";
import OpBase from "@/components/chunithm/op/OpBase.vue";
import OpGenre from "@/components/chunithm/op/OpGenre.vue";
import OpLevel from "@/components/chunithm/op/OpLevel.vue";
import OpVersion from "@/components/chunithm/op/OpVersion.vue";
import {default as ChunithmPreview} from "@/components/chunithm/Preview.vue";
import ChunithmSong from "@/components/chunithm/ChunithmSong.vue";
import MaiSong from "@/components/maimai/MaiSong.vue";
import MaiSongScore from "@/components/maimai/MaiSongScore.vue";
import MaiSongTitles from "@/components/maimai/MaiSongTitles.vue";


const routes = [
    {path: '/', component: NotFound},
    {path: '/help', component: Help},
    {path: '/maimai/best', component: MaiBestScores},
    {path: '/maimai/recommend', component: Recommend},
    {path: '/maimai/summary', component: MaiMaiSummary},
    {path: '/maimai/song', component: MaiSong},
    {path: '/maimai/song-score', component: MaiSongScore},
    {path: '/maimai/song-titles', component: MaiSongTitles},
    {path: '/chunithm/best', component: ChuBestScores},
    {path: '/chunithm/summary', component: ChunithmSummary},
    {path: '/chunithm/overpower', component: OverPowerAll},
    {path: '/chunithm/op-base', component: OpBase},
    {path: '/chunithm/op-genre', component: OpGenre},
    {path: '/chunithm/op-level', component: OpLevel},
    {path: '/chunithm/op-version', component: OpVersion},
    {path: '/chunithm/preview', component: ChunithmPreview},
    {path: '/chunithm/song', component: ChunithmSong},
    {path: '/ongeki/song/:id', component: OngekiSong},
    {path: '/osu/score', component: OsuScore},
    {path: '/osu/recommend', component: OsuRecommend},
    {path: '/osu/preview', component: OsuPreview},
    {path: '/:catchAll(.*)', redirect: '/'}
]

const router = createRouter({
    history: createWebHistory(),
    routes,
})

const app = createApp(App)

app.config.globalProperties.$host = window.location.origin
app.use(router).mount('#app')


