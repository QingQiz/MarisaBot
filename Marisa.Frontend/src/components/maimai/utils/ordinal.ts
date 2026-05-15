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
