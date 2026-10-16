import {ACHIEVEMENT_MAX, RANK_STEPS} from './score'

export const toleranceRanks = ['sss', 'sssp'] as const
export type ToleranceRank = typeof toleranceRanks[number]

const deductions = {justice: 10_000, attack: 510_000, miss: 1_010_000} as const
export type ToleranceJudgement = keyof typeof deductions

/** 与 chu tol 的扣分规则一致；每种判定独立计数，其余音符均视为 JC。 */
export function toleranceCounts(maxCombo: number | null, rank: ToleranceRank) {
    const threshold = RANK_STEPS.find(([, name]) => name === rank)![0]
    const budget = ACHIEVEMENT_MAX - threshold
    if (maxCombo == null || maxCombo <= 0) return null
    return {
        justice: Math.floor(budget * maxCombo / deductions.justice),
        attack: Math.floor(budget * maxCombo / deductions.attack),
        miss: Math.floor(budget * maxCombo / deductions.miss),
    }
}
