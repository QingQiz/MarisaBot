import {chunithm_levelColors} from '@/GlobalVars'
import {bgKeyOf, cardBackground} from '@/components/maimai/utils/song_card'
import type {VersusStanding} from '@/components/versus/utils'

export interface ChuScore {
    State: 'played' | 'unplayed'
    Score: number | null
    Rank: string | null
    Rating: number | null
    FullCombo: string
    FullChain: string
    Clear: string | null
}
export interface ChuSong { Id: number; Title: string; Artist: string; Genre: string; Version: string; Bpm: string }
export interface ChuCell { Score: ChuScore; Place: number | null; Points: number; Matchups: number }
export interface ChuChart { Song: ChuSong; LevelIndex: number; LevelName: string; ChartIndex: number; Level: string; Constant: number; Cells: ChuCell[] }
export type {VersusStanding}
export {placeColor, placeSuffix, formatPoints} from '@/components/versus/utils'
export {rankIcon} from '@/components/chunithm/utils/score'

const scoreFormat = new Intl.NumberFormat('en-US')
export function formatScore(score: number | null) { return score == null ? '—' : scoreFormat.format(score) }
export function formatRating(rating: number | null) { return rating == null ? '—' : rating.toFixed(2) }
export function hasClear(clear?: string | null) { return ['clear', 'hard', 'brave', 'absolute', 'catastrophy', 'failed'].includes(clear ?? '') }
const ultimaAccent = '#e71845'
export function difficultyColor(index: number) { return index === 4 ? ultimaAccent : chunithm_levelColors[index] ?? '#c0c6d2' }
export function difficultyStyle(index: number) {
    return index === 4 ? {
        color: 'var(--chu-ultima-ink)',
        WebkitTextStroke: '0.02em #09080b',
        paintOrder: 'stroke fill',
        textShadow: '0 1px 1px #0009',
    } : {color: difficultyColor(index)}
}
export function backgroundOf(index: number) {
    return index === 4
        ? 'radial-gradient(ellipse at 100% 0%, #50102122, transparent 58%), linear-gradient(168deg, #242127 0%, #0d0b10 62%, #130a0f 100%)'
        : cardBackground(bgKeyOf(index, false)).background
}
export function versionLogo(version: string) {
    let key = version.trim().toLowerCase().replace(/!/g, '').replace(/\s+/g, '_').replace(/^chunithm_/, '')
    if (key === 'plus') key = 'chunithm_plus'
    return key ? `/assets/chunithm/pic/logo_${key}.png` : '/assets/chunithm/pic/logo.png'
}
export function fallbackLogo(event: Event) {
    const image = event.target as HTMLImageElement
    if (image.getAttribute('src') !== '/assets/chunithm/pic/logo.png') image.src = '/assets/chunithm/pic/logo.png'
}
