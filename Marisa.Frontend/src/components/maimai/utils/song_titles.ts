import {achievementOrdinal, dxScoreStar, fcOrdinal, fsOrdinal} from './ordinal'

export interface SongTitle {
    id: number
    name: string
    rare: string
    text: string
    norm: string
    diffIdx: number
    check: {
        dim: string
        lv: number
        scope: string
        difficultyIndices?: number[]
    } | null
}

export type SongTitlesDatabase = Record<string, SongTitle[]>

export interface TitleChartScore {
    LevelIndex: number
    MaxDx: number
    Played: boolean
    Achievement: number | null
    Fc: string | null
    Fs: string | null
    DxScore: number | null
}

const RARE_ORDER: Record<string, number> = {Rainbow: 0, Gold: 1, Silver: 2, Bronze: 3, Normal: 4}

/** Full SD/DX IDs remain separate; earlier databases take precedence for duplicate titles. */
export function titlesForSong(songId: number, ...databases: (SongTitlesDatabase | null)[]): SongTitle[] {
    const titles = new Map<number, SongTitle>()
    for (const database of databases) {
        const list = database?.[String(songId)]
        if (!Array.isArray(list)) continue
        for (const title of list) {
            if (!titles.has(title.id)) titles.set(title.id, title)
        }
    }
    return [...titles.values()].sort((a, b) =>
        (RARE_ORDER[a.rare] ?? 9) - (RARE_ORDER[b.rare] ?? 9) || a.id - b.id)
}

/** null means that score-provider records cannot establish the complete unlock condition. */
export function isTitleAchieved(title: SongTitle, charts: TitleChartScore[]): boolean | null {
    const check = title.check
    if (!check || !['rank', 'fc', 'fs', 'dx', 'play'].includes(check.dim) ||
        !['one', 'all'].includes(check.scope) || !Number.isInteger(check.lv) || check.lv < 0) return null

    let indices = check.difficultyIndices
    if (indices !== undefined) {
        if (!Array.isArray(indices) || !indices.length ||
            indices.some(index => !Number.isInteger(index) || index < 0 || index > 4)) return null
        indices = [...new Set(indices)]
    } else if (check.scope === 'all') {
        // ALL and ALL+Re:MASTER require different sets; the available charts cannot disambiguate them.
        return null
    } else if (title.diffIdx >= 0) {
        if (!Number.isInteger(title.diffIdx) || title.diffIdx > 4) return null
        indices = [title.diffIdx]
    }

    const groups = indices
        ? indices.map(index => charts.filter(chart => chart.LevelIndex === index))
        : charts.filter(chart => Number.isInteger(chart.LevelIndex) && chart.LevelIndex >= 0 && chart.LevelIndex <= 4)
            .map(chart => [chart])
    const results = groups.map(group => someResult(group.map(chart => chartAchieved(chart, check))))
    if (check.scope === 'one') return someResult(results)
    if (results.includes(false)) return false
    return results.includes(null) ? null : true
}

function someResult(results: (boolean | null)[]): boolean | null {
    if (results.includes(true)) return true
    return results.includes(null) ? null : false
}

function chartAchieved(chart: TitleChartScore, check: NonNullable<SongTitle['check']>): boolean | null {
    if (!chart.Played) return false
    let ordinal: number
    switch (check.dim) {
        case 'play': ordinal = 1; break
        case 'rank':
            if (chart.Achievement === null || !Number.isFinite(chart.Achievement)) return null
            ordinal = achievementOrdinal(chart.Achievement)
            break
        case 'fc':
            if (typeof chart.Fc !== 'string') return null
            ordinal = fcOrdinal(chart.Fc)
            break
        case 'fs':
            if (typeof chart.Fs !== 'string') return null
            ordinal = fsOrdinal(chart.Fs)
            break
        case 'dx':
            if (chart.DxScore === null || !Number.isFinite(chart.DxScore) || chart.DxScore < 0 ||
                !Number.isFinite(chart.MaxDx) || chart.MaxDx <= 0) return null
            ordinal = dxScoreStar(chart.DxScore, chart.MaxDx)
            break
        default: return null
    }
    return ordinal >= check.lv
}
