import {createApp} from 'vue'
import {createRouter, createWebHistory} from 'vue-router'

import '@/assets/css/tailwind.css'
import '@/ExtensionMethods'

import App from '@/App.vue'
import BestScores from "@/components/maimai/BestScores.vue";
import OngekiSong from "@/components/ongeki/OngekiSong.vue";
import OsuScore from "@/components/osu/OsuScore.vue";
import NotFound from "@/components/NotFound.vue";
import OsuRecommend from "@/components/osu/OsuRecommend.vue";
import OsuPreview from "@/components/osu/OsuPreview.vue";
import Recommend from "@/components/maimai/Recommend.vue";
import WordCloud from "@/components/WordCloud/WordCloud.vue";
import {default as ChunithmSummary} from "@/components/chunithm/Summary.vue";


const routes = [
    {path: '/', component: NotFound},
    {path: '/maimai/best', component: BestScores},
    {path: '/maimai/recommend', component: Recommend},
    {path: '/chunithm/summary', component: ChunithmSummary},
    {path: '/ongeki/song/:id', component: OngekiSong},
    {path: '/osu/score', component: OsuScore},
    {path: '/osu/recommend', component: OsuRecommend},
    {path: '/osu/preview', component: OsuPreview},
    {path: '/wordcloud', component: WordCloud},
    {path: '/:catchAll(.*)', redirect: '/'}
]

const router = createRouter({
    history: createWebHistory(),
    routes,
})

const app = createApp(App)

app.config.globalProperties.$host = 'http://localhost:14311'
app.use(router).mount('#app')


