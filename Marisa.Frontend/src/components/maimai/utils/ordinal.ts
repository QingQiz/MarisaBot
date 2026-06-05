// Achievement / Fc / Fs 字段值 → 有序整数（用于阈值比较 / 段位排序）。
// 跟 PlateData.cs 的 AchievementLevel / FcLevel / FsLevel 三个 C# helper 同语义；
// diving-fish fs 字段返回 fsd/fsdp (FDX/FDX+) 命名，fdx/fdxp 是旧别名也接受。

export function achievementOrdinal(a: number): number {
    if (a >= 100.5) return 13
    if (a >= 100)   return 12
    if (a >= 99.5)  return 11
    if (a >= 99)    return 10
    if (a >= 98)    return 9
    if (a >= 97)    return 8
    if (a >= 94)    return 7
    if (a >= 90)    return 6
    if (a >= 80)    return 5
    if (a >= 75)    return 4
    if (a >= 70)    return 3
    if (a >= 60)    return 2
    if (a >= 50)    return 1
    return 0
}

export function fcOrdinal(fc: string): number {
    return ({fc: 1, fcp: 2, ap: 3, app: 4} as Record<string, number>)[fc] ?? 0
}

export function fsOrdinal(fs: string): number {
    return ({sync: 1, fs: 2, fsp: 3, fsd: 4, fdx: 4, fsdp: 5, fdxp: 5} as Record<string, number>)[fs] ?? 0
}

/**
 * DX Score 星档 (1-5★，对应 max DX 的 85/90/93/95/97%)。
 * 跟 C# PlateData.DxScoreStar 同步用整数运算避免浮点抖。maxDx=0 兜底返 0。
 * 官方 maimai でらっくす 只到 5★，所以 plate 完成表 marker icon 也只 1-5★。
 */
export function dxScoreStar(dxScore: number, maxDx: number): number {
    if (!maxDx || maxDx <= 0) return 0
    const hundred = dxScore * 100
    if (hundred >= maxDx * 97) return 5
    if (hundred >= maxDx * 95) return 4
    if (hundred >= maxDx * 93) return 3
    if (hundred >= maxDx * 90) return 2
    if (hundred >= maxDx * 85) return 1
    return 0
}
