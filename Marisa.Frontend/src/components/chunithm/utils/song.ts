import type {ChuSongHeaderData} from './song-card'

export interface ChuSongBeatmap {
    LevelName: string
    LevelStr: string | null
    Constant: number | null
    MaxCombo: number | null
    Charter: string | null
    Bpm: string | null
    ChartName: string | null
}

export interface ChuSongData extends Omit<ChuSongHeaderData, 'Bpm'> {
    Beatmaps: (ChuSongBeatmap | null)[]
}

const standardNames = ['BASIC', 'ADVANCED', 'EXPERT', 'MASTER', 'ULTIMA']

export function isWorldsEnd(chart: Pick<ChuSongBeatmap, 'LevelName'>) {
    return chart.LevelName.trim().toUpperCase() === "WORLD'S END"
}

export function chartDifficulty(chart: Pick<ChuSongBeatmap, 'LevelName'>) {
    return isWorldsEnd(chart) ? 5 : standardNames.indexOf(chart.LevelName.trim().toUpperCase())
}

const bpmRange = /^(\d+(?:\.\d+)?)\s*\(\s*(\d+(?:\.\d+)?)\s*-\s*(\d+(?:\.\d+)?)\s*\)$/
const bpmNumber = (value: string) => String(Number(value))

export function chartBpm(chart: ChuSongBeatmap) {
    const raw = chart.Bpm?.trim() ?? ''
    const match = raw.match(bpmRange)
    if (match) return {main: bpmNumber(match[1]), range: `${bpmNumber(match[2])}–${bpmNumber(match[3])}`}
    return {main: /^\d+(?:\.\d+)?$/.test(raw) ? bpmNumber(raw) : raw || '—', range: ''}
}
export function chartConstant(chart: ChuSongBeatmap) {
    return isWorldsEnd(chart) || chart.Constant == null || !Number.isFinite(chart.Constant) || chart.Constant <= 0
        ? '—' : chart.Constant.toFixed(1)
}
export function chartCombo(chart: ChuSongBeatmap) {
    return chart.MaxCombo == null || !Number.isFinite(chart.MaxCombo) || chart.MaxCombo <= 0 ? '—' : chart.MaxCombo.toLocaleString('en-US')
}

export function summaryBpm(charts: readonly ChuSongBeatmap[]) {
    const values = [...new Set(charts.map(chart => chart.Bpm?.trim()).filter((bpm): bpm is string => Boolean(bpm)))]
    if (values.length === 0) return ''
    if (values.length === 1 && !charts.some(chart => !chart.Bpm?.trim())) {
        const {main, range} = chartBpm(charts[0])
        return range ? `${main} (${range})` : main
    }
    if (charts.some(chart => !chart.Bpm?.trim())) return '分谱'

    const groups = values.map(value => (value.match(/\d+(?:\.\d+)?/g) ?? []).map(Number).filter(n => Number.isFinite(n) && n > 0))
    if (groups.some(group => group.length === 0)) return '分谱'
    const numbers = groups.flat()
    const minimum = Math.min(...numbers), maximum = Math.max(...numbers)
    if (minimum === maximum) return String(minimum)
    const range = `${minimum}–${maximum}`
    const primary = groups[0][0]
    return groups.every(group => group[0] === primary) ? `${primary} (${range})` : range
}
