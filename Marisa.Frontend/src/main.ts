import {createApp} from 'vue'
import {createRouter, createWebHistory} from 'vue-router'

import App from '@/App.vue'
import Best from "@/components/maimai/Best.vue";
import NotFound from "@/components/NotFound.vue";

import './assets/css/tailwind.css'

const routes = [
    {path: '/', component: NotFound},
    {path: '/maimai/best', component: Best},
    {path: '/:catchAll(.*)', redirect:'/'}

]

const router = createRouter({
    history: createWebHistory(),
    routes,
})

const app = createApp(App)
app.config.globalProperties.$host = 'http://localhost:14311'
app.use(router).mount('#app')


