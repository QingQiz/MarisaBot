export type Chart = { [key: string]: number[][] };
// [measure, offset, cell, width, duration]
type LnData = [string, number, number, number, number, number];
// [measure, offset, cell, width, type, duration]
type AirHoldData = [string, number, number, number, number, string, number];
// [measure, offset, cell, width, duration, target_cell, target_width]
type SldData = [string, number, number, number, number, number, number, number];
// [measure, offset, cell, width, duration, target_cell, target_width, color]
type AirSldData = [string, number, number, number, number, string, number, number, number, number, number, string];
// [measure, offset, cell, width, interval, height, duration, target_cell, target_width, target_height, color]
type AldData = [string, number, number, number, number, number, number, number, number, number, number, string];


const available_prefix = [
    // 控制的
    "BPM", "MET", "SFL",
    // 固有的
    "TAP", "CHR", "HLD", "SLD", "SLC", "FLK", "AIR", "AUR", "AUL", "AHD", "ADW", "ADR", "ADL", "MNE",
    // NEW
    "ASC", "ASD", "ALD", "HXD", "SXD", "AHX", "SXC"
];

const alias_prefix = [
    "BPM_DEF", "MET_DEF",
];

const self_defined = [
    // 自己添加的
    "HLD_H", "HLD_T", "HLD_B", "AHD_H", "AHD_T", "AHD_B", "SLD_H", "SLD_T", "ASD_H", "ASC_H", "ASD_T", "ALD_T"
];

export const cat_1 = [
    "TAP", "CHR", "MNE", "FLK",
    "HLD_H", "HLD_T", "AHD_H", "ASC_H", "ASD_H", "AHD_T", "SLD_H", "SLD_T", "ASD_T", "ALD_T",
    "AIR", "AUR", "AUL", "ADW", "ADR", "ADL",
];
export const cat_2 = ["HLD_B", "AHD_B"];
export const cat_3 = ["SLC", "SLD", "ASC", "ASD", "ALD"];
export const cat_4 = ["BPM", "BEAT_1", "BEAT_2"];

export const color_map = [
    "AQA", "BLK", "BLU", "CYN", "DEF", "DGR", "GRN", "GRY", "LIM", "NON", "ORN", "PNK", "PPL", "RED", "VLT", "YEL"
]

export function Parse(chart_string: string): Chart {
    // read chart string and parse it to chart data
    let data = chart_string
        .split("\n")
        .map(Line2Event)
        .filter(x => {
            let key = x[0].toString()
            return available_prefix.indexOf(key) != -1 || alias_prefix.indexOf(key) != -1
        });

    // parse chart data to chart
    let chart: Chart = {};

    for (let key of available_prefix.concat(self_defined)) {
        chart[key] = [];
    }

    for (let line of data) {
        ParseLine(chart, line);
    }

    MakeBeatLine(chart);

    SplitLnToFitBpmScale(chart);
    ScaleChartTickByBpm(chart);
    UpdateSldHead(chart);

    return chart;
}

function MakeBeatLine(chart: Chart) {
    let max_tick = GetMaxTick(chart);

    chart["BEAT_1"] = [];
    chart["BEAT_2"] = [];

    for (let i = 0; i < max_tick; i += 384) {
        chart["BEAT_1"].push([i, i / 384]);
        for (let j = i + 96; j < i + 384 && j < max_tick; j += 96) {
            chart["BEAT_2"].push([j]);
        }
    }
}


function UpdateSldHead(chart: Chart) {
    let sld_end: { [key: string]: boolean } = {};

    let slides = chart['SLD'].concat(chart['SLC']);

    for (const slide of slides) {
        let tick_end     = slide[3];
        let target_cell  = slide[4];
        let target_width = slide[5];

        sld_end[[tick_end, target_cell, target_width].toString()] = true;
    }

    for (const slide of slides) {
        let tick  = slide[0];
        let cell  = slide[1];
        let width = slide[2];

        if (sld_end[[tick, cell, width].toString()]) {
            continue;
        }

        chart[slide[6] == 1 ? 'CHR' : 'SLD_H'].push([tick, cell, width]);
    }
}

export function GetMaxTick(chart: Chart) {
    let tick_max = 0;

    for (let key of Object.keys(chart)) {
        for (let i = 0; i < chart[key].length; i++) {
            tick_max = Math.max(tick_max, chart[key][i][0]);
            if (cat_3.indexOf(key) != -1 || cat_2.indexOf(key) != -1) {
                tick_max = Math.max(tick_max, chart[key][i][3]);
            }
        }
    }
    return tick_max;
}

/**
 * 将所有的tick按照BPM进行缩放，以保证其间隔和 BPM 成比例
 */
function ScaleChartTickByBpm(chart: Chart) {
    // scale tick by BPM
    let bpm_before = chart['BPM'][0][1];

    for (let i = 0; i < chart['BPM'].length; i++) {
        let [tick_i, bpm] = chart['BPM'][i];
        if (bpm == bpm_before) continue;
        if (bpm == 0) continue;

        for (let key of Object.keys(chart)) {
            for (let j = 0; j < chart[key].length; j++) {
                let tick_j = chart[key][j][0];

                if (tick_j < tick_i) continue;

                chart[key][j][0] = tick_i + (tick_j - tick_i) * bpm_before / bpm;

                if (cat_3.indexOf(key) != -1 || cat_2.indexOf(key) != -1) {
                    chart[key][j][3] = (chart[key][j][3] - tick_i) * bpm_before / bpm + tick_i;
                }
            }
        }

        bpm_before = bpm;
    }
}

export function ScaleTick(chart: Chart, scale: number) {
    for (let key of Object.keys(chart)) {
        for (let j = 0; j < chart[key].length; j++) {
            chart[key][j][0] = chart[key][j][0] * scale;

            if (cat_3.indexOf(key) != -1 || cat_2.indexOf(key) != -1) {
                chart[key][j][3] = chart[key][j][3] * scale;
            }
        }
    }
}

function ParseLine(chart: Chart, data: any) {
    let key = data[0];
    if (key == 'HLD' || key == "HXD") {
        ParseLn(chart, data as LnData);
    } else if (key == 'AHD' || key == "AHX") {
        ParseAirHold(chart, data as AirHoldData);
    } else if (key == "SLD" || key == "SLC" || key == "SXD" || key == "SXC") {
        ParseSld(chart, data as SldData);
    } else if (key == "ASC" || key == "ASD") {
        ParseAirSld(chart, data as AirSldData);
    } else if (key == "ALD") {
        ParseAld(chart, data as AldData);
    } else if (key == "BPM_DEF") {
        chart["BPM"].push([0, data[1] as number]);
    } else if (key == "MET_DEF") {
        chart["MET"].push([0, data[1] as number, data[2] as number]);
    } else {
        chart[key].push(
            [ToTick(data[1] as number, data[2] as number)].concat((data as number[]).slice(3))
        );
    }
}

function ParseLn(chart: Chart, data: LnData) {
    let d = {
        measure: data[1], offset: data[2], cell: data[3], width: data[4], duration: data[5]
    };

    let h = [ToTick(d.measure, d.offset), d.cell, d.width];
    let t = [
        ToTick(d.measure + Math.floor((d.offset + d.duration) / 384),
            (d.offset + d.duration) % 384
        ),
        d.cell, d.width
    ]

    if (data[0] == "HLD") {
        chart['HLD_H'].push(h);
    } else {
        chart["CHR"].push(h);
    }

    chart["HLD_T"].push(t);
    chart['HLD_B'].push([ToTick(d.measure, d.offset), d.cell, d.width, ToTick(d.measure, d.offset) + d.duration]);
}

function ParseAirHold(chart: Chart, data: AirHoldData) {
    let d = {
        measure: data[1], offset: data[2], cell: data[3], width: data[4], type: data[5], duration: data[6]
    };

    if (d.type != 'AHD' && d.type != 'AHX') {
        chart['AHD_H'].push([ToTick(d.measure, d.offset), d.cell, d.width]);
    }

    chart['AHD_T'].push([
        ToTick(
            d.measure + Math.floor((d.offset + d.duration) / 384), (d.offset + d.duration) % 384
        ), d.cell, d.width
    ]);

    chart['AHD_B'].push([ToTick(d.measure, d.offset), d.cell, d.width, ToTick(d.measure, d.offset) + d.duration]);
}

function ParseSld(chart: Chart, data: SldData) {
    let d = {
        measure: data[1], offset: data[2], cell: data[3], width: data[4], duration: data[5],
        target_cell: data[6], target_width: data[7]
    };

    if (data[0] == "SLD" || data[0] == "SXD") {
        chart['SLD_T'].push([
            ToTick(
                d.measure + Math.floor((d.offset + d.duration) / 384), (d.offset + d.duration) % 384
            ), d.target_cell, d.target_width
        ]);
    }

    chart[data[0].replace('X', 'L')].push([
        ToTick(d.measure, d.offset), d.cell, d.width,
        ToTick(d.measure, d.offset) + d.duration, d.target_cell, d.target_width, data[0][1] == 'X' ? 1 : 0
    ]);
}

function ParseAirSld(chart: Chart, data: AirSldData) {
    let d = {
        measure: data[1], offset: data[2], cell: data[3], width: data[4], type: data[5],
        height: data[6], duration: data[7], target_cell: data[8], target_width: data[9], target_height: data[10],
        color: data[11]
    };

    if (data[0] == "ASD") {
        chart['ASD_T'].push([
            ToTick(
                d.measure + Math.floor((d.offset + d.duration) / 384), (d.offset + d.duration) % 384
            ), d.target_cell, d.target_width
        ]);
    }

    if (d.type != 'ASC' && d.type != 'ASD' && d.type != 'AHD') {
        chart[data[0] + '_H'].push([ToTick(d.measure, d.offset), d.cell, d.width, d.height]);
    }

    chart[data[0]].push([
        ToTick(d.measure, d.offset), d.cell, d.width,
        ToTick(d.measure, d.offset) + d.duration, d.target_cell, d.target_width, color_map.indexOf(d.color)
    ]);
}

function ParseAld(chart: Chart, data: AldData) {
    let d = {
        measure: data[1], offset: data[2], cell: data[3], width: data[4], interval: data[5],
        height: data[6], duration: data[7], target_cell: data[8], target_width: data[9], target_height: data[10],
        color: data[11]
    };

    chart[data[0]].push([
        ToTick(d.measure, d.offset), d.cell, d.width,
        ToTick(d.measure, d.offset) + d.duration, d.target_cell, d.target_width, color_map.indexOf(d.color)
    ]);

    if (d.interval == 0) return;

    for (let duration = 0; duration < d.duration; duration += d.interval) {
        let ratio = duration / d.duration;
        let cell  = (d.target_cell - d.cell) * ratio + d.cell;
        let width = (d.target_width - d.width) * ratio + d.width;

        chart['ALD_T'].push([
            ToTick(
                d.measure + Math.floor((d.offset + duration) / 384), (d.offset + duration) % 384
            ), cell, width
        ]);
    }
}

function Line2Event(line: string) {
    let params = line.trim().split('\t').map(x => x.toUpperCase());
    if (params[0] == "BPM") {
        return [params[0], parseInt(params[1]), parseInt(params[2]), parseFloat(params[3])];
    } else if (params[0] == "BPM_DEF") {
        return [params[0], parseFloat(params[1]), parseFloat(params[2]), parseFloat(params[3]), parseFloat(params[4])];
    } else if (params[0] == "SFL") {
        return [params[0], parseInt(params[1]), parseInt(params[2]), parseInt(params[3]), parseFloat(params[4])];
    }
    // parse all to int if possible
    return params.map(x => isNaN(parseInt(x)) ? x : parseInt(x));
}


function ToTick(measure: number, offset: number) {
    return measure * 384 + offset;
}

/**
 * 在 tick_split 处将 SLD/SLC 分割为两部分
 * @param tick_start
 * @param tick_split
 * @param duration
 * @param cell
 * @param width
 * @param target_cell
 * @param target_width
 * @param color
 */
function SplitSldBody(
    tick_start: number, tick_split: number, duration: number, cell: number, width: number,
    target_cell: number, target_width: number, color: number
) {
    let result = [];

    let ratio     = (tick_split - tick_start) / duration;
    let mid_cell  = (target_cell - cell) * ratio + cell;
    let mid_width = (target_width - width) * ratio + width;

    result.push([tick_start, cell, width, tick_split, mid_cell, mid_width, color]);
    result.push([tick_split, mid_cell, mid_width, tick_start + duration, target_cell, target_width, color]);

    return result;
}

/**
 * 在 tick_split 处将 HLD/AHD 分割为两部分
 * @param tick_start
 * @param tick_split
 * @param duration
 * @param cell
 * @param width
 * @param extra 跨区补偿，和 NoteHeight 相等
 */
function SplitLnBody(
    tick_start: number, tick_split: number, duration: number, cell: number, width: number, extra: number = 0
) {
    let result = [];

    result.push([tick_start, cell, width, tick_split + extra]);
    result.push([tick_split, cell, width, tick_start + duration]);

    return result;
}

function SplitChart(chart: Chart, key: string, tick: number, fun: (x: number[]) => number[][]) {
    let to_preserve = [];
    let to_add      = [];

    for (let i = 0; i < chart[key].length; i++) {
        let tick_s = chart[key][i][0];
        let tick_e = chart[key][i][3];

        if (tick_s < tick && tick_e > tick) {
            for (let x of fun(chart[key][i])) {
                to_add.push(x);
            }
        } else {
            to_preserve.push(i);
        }
    }

    chart[key] = to_preserve.map(x => chart[key][x]);

    for (let i of to_add) {
        chart[key].push(i);
    }
}

/**
 * 在BPM变化的地方分割所有的长条和滑条，以保证长条的长度和BPM成比例
 * 不需要补偿长条
 * @param chart
 */
function SplitLnToFitBpmScale(chart: Chart) {
    for (let [tick, _] of chart['BPM']) {
        for (let key of cat_2) {
            SplitChart(chart, key, tick,
                data => SplitLnBody(data[0], tick, data[3] - data[0], data[1], data[2])
            );
        }

        for (let key of cat_3) {
            SplitChart(chart, key, tick,
                data => SplitSldBody(
                    data[0], tick, data[3] - data[0], data[1], data[2], data[4], data[5], data[6]
                )
            );
        }
    }
}

export function SplitChartAt(chart: Chart, tick: number): void {
    for (let key of cat_2) {
        SplitChart(chart, key, tick,
            data => SplitLnBody(data[0], tick, data[3] - data[0], data[1], data[2]),
        );
    }

    for (let key of cat_3) {
        SplitChart(chart, key, tick,
            data => SplitSldBody(
                data[0], tick, data[3] - data[0], data[1], data[2], data[4], data[5], data[6]
            )
        );
    }
}