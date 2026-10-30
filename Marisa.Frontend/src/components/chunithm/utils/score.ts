import {Score} from "./summary_t";

/**
 * 成就分上限，达到即 ALL JUSTICE。
 * 与后端 Marisa.Plugin.Shared.Chunithm.ChunithmScore 一致。
 */
export const ACHIEVEMENT_MAX = 1_010_000;

export type ChunithmRank =
    'sssp' | 'sss' | 'ssp' | 'ss' | 'sp' | 's' |
    'aaa' | 'aa' | 'a' | 'bbb' | 'bb' | 'b' | 'c' | 'd';

/** 原生字片共用同一画布，避免附加 + 改变主体字的显示大小。 */
export function rankIcon(rank: string | null) {
    return `/assets/chunithm/pic/rank_${(rank ?? 'd').toLowerCase()}.svg`;
}

/**
 * 舞萌同款的 rank 牌子（PNG，宽度随等级变化、带底衬），汇总图用它替代中二原生 SVG 字片：
 * 视觉分量更足，也和 maisum 完全一致。其他页面（b30/b50、chu vs）仍用 rankIcon。
 */
export function rankSummaryIcon(rank: string | null) {
    return `/assets/maimai/pic/rank_summary_${(rank ?? 'd').toLowerCase()}.png`;
}

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
export function isMaxScore(score?: Pick<Score, 'score'> | null): boolean {
    return !!score && score.score >= ACHIEVEMENT_MAX;
}

export function isAllJustice(score?: Pick<Score, 'fc'> | null): boolean {
    return !!score && score.fc === 'alljustice';
}

/** fullcombo / fullchain / fullchain2 统称 FC 系。 */
export function isFullCombo(score?: Pick<Score, 'fc'> | null): boolean {
    return !!score && (score.fc ?? '').startsWith('full');
}

export function getRank(achievement: number): ChunithmRank {
    for (const [threshold, rank] of RANK_STEPS) {
        if (achievement >= threshold) return rank;
    }
    return 'd';
}

export interface ChuScore {
    State: 'played' | 'unplayed'
    Score: number | null
    Rank: string | null
    Rating: number | null
    FullCombo: string
    FullChain: string
    Clear: string | null
}

const scoreFormat = new Intl.NumberFormat('en-US')
export function formatScore(score: number | null) { return score == null ? '—' : scoreFormat.format(score) }
export function formatRating(rating: number | null) { return rating == null ? '—' : rating.toFixed(2) }
export function hasCombo(combo?: string | null) { return ['fullcombo', 'alljustice', 'alljusticecritical'].includes(combo ?? '') }
export function hasChain(chain?: string | null) { return chain === 'fullchain' || chain === 'fullchain2' }
export function hasClear(clear?: string | null) { return ['clear', 'hard', 'brave', 'absolute', 'catastrophy', 'failed'].includes(clear ?? '') }
export function hasMarks(score: ChuScore | null) {
    return score?.State === 'played' && (hasCombo(score.FullCombo) || hasChain(score.FullChain) || hasClear(score.Clear))
}

/**
 * 把各查分器的原始 fc 写法归一到 versus_* 的素材名，
 * 与后端 ChunithmScore.FullCombo 保持一致。b30/b50 卡片拿到的是原始 fc，
 * 里面可能是 fc/aj/ajc 这类缩写，而素材只有 fullcombo/alljustice/alljusticecritical。
 */
export function normalizeCombo(fc?: string | null): string {
    switch ((fc ?? '').toLowerCase()) {
        case 'fc':
        case 'fullcombo':
            return 'fullcombo';
        case 'aj':
        case 'alljustice':
            return 'alljustice';
        case 'ajc':
        case 'alljusticecritical':
            return 'alljusticecritical';
        default:
            return '';
    }
}

/** 部分查分器会把 fullchain / fullchain2 塞在 fc 里，单独取出来当成链标记。 */
export function chainOfFc(fc?: string | null): string {
    const value = (fc ?? '').toLowerCase();
    return value === 'fullchain' || value === 'fullchain2' ? value : '';
}
