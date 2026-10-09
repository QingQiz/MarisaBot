import {Score} from "./summary_t";

/**
 * 成就分上限，达到即 ALL JUSTICE。
 * 与后端 Marisa.Plugin.Shared.Chunithm.ChunithmScore 一致。
 */
export const ACHIEVEMENT_MAX = 1_010_000;

export type ChunithmRank =
    'sssp' | 'sss' | 'ssp' | 'ss' | 'sp' | 's' |
    'aaa' | 'aa' | 'a' | 'bbb' | 'bb' | 'b' | 'c' | 'd';

/** 评级阈值，从高到低。与后端 ChunithmScore.GetRank 同源，不要在页面里各写一份。 */
export const RANK_STEPS: [threshold: number, rank: ChunithmRank][] = [
    [1_009_000, 'sssp'],
    [1_007_500, 'sss'],
    [1_005_000, 'ssp'],
    [1_000_000, 'ss'],
    [990_000,   'sp'],
    [975_000,   's'],
    [950_000,   'aaa'],
    [925_000,   'aa'],
    [900_000,   'a'],
    [800_000,   'bbb'],
    [700_000,   'bb'],
    [600_000,   'b'],
    [500_000,   'c'],
    [0,         'd'],
];

/**
 * 是否满分（对应 OverPower 里的 ajc 档）。
 * 1010000 是上限，这里用 >= 而不是 ==：脏数据超过上限时也不会被漏判成普通 AJ。
 */
export function isMaxScore(score?: Score | null): boolean {
    return !!score && score.score >= ACHIEVEMENT_MAX;
}

export function isAllJustice(score?: Score | null): boolean {
    return !!score && score.fc === 'alljustice';
}

/** fullcombo / fullchain / fullchain2 统称 FC 系。 */
export function isFullCombo(score?: Score | null): boolean {
    return !!score && (score.fc ?? '').startsWith('full');
}

export function getRank(achievement: number): ChunithmRank {
    for (const [threshold, rank] of RANK_STEPS) {
        if (achievement >= threshold) return rank;
    }
    return 'd';
}
