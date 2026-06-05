import { Score } from "./summary_t";

export const SCORE_SEGMENTS: {
    threshold: number;
    opBase: (c: number) => number;
    cellLength: (c: number) => number;
    cellValue: number;
}[] = [
    { threshold: 800000,   opBase: () => 0,            cellLength: (c: number) => 6000 / (c - 5),  cellValue: 0.05  },
    { threshold: 900000,   opBase: (c: number) => (c - 5) / 2, cellLength: (c: number) => 2000 / (c - 5), cellValue: 0.05 },
    { threshold: 975000,   opBase: (c: number) => c - 5,        cellLength: () => 150,                cellValue: 0.05 },
    { threshold: 1000000,  opBase: (c: number) => c,             cellLength: () => 250,                cellValue: 0.05 },
    { threshold: 1005000,  opBase: (c: number) => c + 1,         cellLength: () => 10,                 cellValue: 0.005 },
    { threshold: 1007500,  opBase: (c: number) => c + 1.5,       cellLength: () => 5,                  cellValue: 0.005 },
    { threshold: Infinity, opBase: (c: number) => c + 2,         cellLength: () => 10 / 3,             cellValue: 0.005 },
];

export function getOpS(constT: number, score: number): number {
    if (score < 500000) return 0;

    let prevThreshold = 500000;
    for (const seg of SCORE_SEGMENTS) {
        if (score < seg.threshold) {
            const scoreDiff = score - prevThreshold;
            const cellNum = Math.floor(scoreDiff / seg.cellLength(constT));
            return (seg.opBase(constT) * 5 + cellNum * seg.cellValue) * 200;
        }
        prevThreshold = seg.threshold;
    }
    return 0;
}

export function calcOverPower(score: Score): number {
    if (!score || score.score == 0) return 0;

    const op_s = getOpS(score.ds, score.score);

    let op_r = 0;
    if (score.fc == 'fullcombo' || score.fc == 'fullchain' || score.fc == 'fullchain2') op_r = 100;
    if (score.fc == 'alljustice') op_r = 200;
    if (score.score == 101_0000) op_r = 250;

    return (op_s + op_r) / 200;
}
