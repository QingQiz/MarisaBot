import {chunithm_levelColors} from '@/GlobalVars'
import {bgKeyOf, cardBackground} from '@/components/maimai/utils/song_card'

export interface ChuSongHeaderData {
    Id: number
    Title: string
    Artist: string
    Genre: string
    Version: string
    Bpm?: string
}

const ultimaAccent = '#e71845'
export function difficultyColor(index: number) {
    return index === 4 ? ultimaAccent : index === 5 ? '#bbc5d3' : chunithm_levelColors[index] ?? '#c0c6d2'
}
export function difficultyStyle(index: number) {
    return index === 4 ? {
        color: 'var(--chu-ultima-ink)',
        WebkitTextStroke: '0.02em #09080b',
        paintOrder: 'stroke fill',
        textShadow: '0 1px 1px #0009',
    } : {color: difficultyColor(index)}
}
export function chartSurface(index: number, fadeAt: number) {
    const color = difficultyColor(index), base = index === 4 ? '#09080bb3' : '#0000006b'
    return {'--rail-color': color, background: `linear-gradient(90deg,${color}26 0%,${base} ${fadeAt}%,${base} 100%)`}
}
export function cardTheme(index: number) {
    const accent = difficultyColor(index)
    return {class: ['chu-theme', {'chu-ultima': index === 4}], bgKey: 'DMY', background: backgroundOf(index), accent, style: {'--accent': accent}}
}
export function backgroundOf(index: number) {
    if (index === 5) return 'linear-gradient(135deg, #1c1e26 0%, #0b0e15 66%, #171a24 100%)'
    return index === 4
        ? 'radial-gradient(ellipse at 100% 0%, #50102122, transparent 58%), linear-gradient(168deg, #242127 0%, #0d0b10 62%, #130a0f 100%)'
        : cardBackground(bgKeyOf(index, false)).background
}
export function versionLogo(version: string) {
    let key = version.trim().toLowerCase().replace(/!/g, '').replace(/\s+/g, '_').replace(/^chunithm_/, '')
    if (key === 'plus') key = 'chunithm_plus'
    return key ? `/assets/chunithm/pic/logo_${key}.png` : '/assets/chunithm/pic/logo.png'
}
// genre：国服类别名统一显示为日服原名
const GENRE_JP: Record<string, string> = {
    '流行 & 动漫': 'POPS＆ANIME', 'POPS & ANIME': 'POPS＆ANIME', '东方Project': '東方Project', '其他游戏': 'VARIETY',
    '彩绿': 'イロドリミドリ', '音击舞萌': 'ゲキマイ', '原创': 'ORIGINAL',
}
export function genreDisplay(genre: string) {
    return GENRE_JP[genre] ?? genre
}
export function fallbackLogo(event: Event) {
    const image = event.target as HTMLImageElement
    if (image.getAttribute('src') !== '/assets/chunithm/pic/logo.png') image.src = '/assets/chunithm/pic/logo.png'
}
