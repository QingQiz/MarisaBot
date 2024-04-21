import {
    Beat,
    Measure,
    Met,
    NotePublic,
    Bpm,
    Noodle,
    Rice,
    Slide,
    SpeedVelocity, NoteType
} from "@/components/chunithm/utils/parser_t";

export type Chart = { [key: string]: NotePublic[] };
/**
 * [measure, offset, cell, width, duration]
 */
type LnData = [string, number, number, number, number, number];
/**
 * [measure, offset, cell, width, type, duration]
 */
type AirHoldData = [string, number, number, number, number, string, number];
/**
 * [measure, offset, cell, width, duration, target_cell, target_width]
 */
type SldData = [string, number, number, number, number, number, number, number];
/**
 * [measure, offset, cell, width, duration, target_cell, target_width, color]
 */
type AirSldData = [string, number, number, number, number, string, number, number, number, number, number, string];
/**
 * [measure, offset, cell, width, interval, height, duration, target_cell, target_width, target_height, color]
 */
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

export const cat_rice    = [
    "TAP", "CHR", "MNE", "FLK",
    "HLD_H", "HLD_T", "AHD_H", "ASC_H", "ASD_H", "AHD_T", "SLD_H", "SLD_T", "ASD_T", "ALD_T",
    "AIR", "AUR", "AUL", "ADW", "ADR", "ADL",
];
export const cat_noodle  = ["HLD_B", "AHD_B"];
export const cat_slide   = ["SLC", "SLD", "ASC", "ASD", "ALD"];
export const cat_control = ["BPM", "BEAT_1", "BEAT_2"];

export const resolution = 384;


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

    UpdateSldHead(chart);
    MakeBeatLine(chart);

    SplitLnToFitBpmScale(chart);
    ScaleChartTickByBpm(chart);

    return chart;
}

function MakeBeatLine(chart: Chart) {
    let max_tick = GetMaxTick(chart);

    chart["BEAT_1"] = [];
    chart["BEAT_2"] = [];

    let met = (chart["MET"] as Met[]).filter(x => x.first != 0 && x.second != 0);

    met.sort((a, b) => a.tick - b.tick);

    let measure_id = 0;

    for (let i = 0; i < met.length; i++) {
        let next_tick = i == met.length - 1 ? max_tick : met[i + 1].tick;

        let beat_length    = resolution / met[i].second;
        let measure_length = beat_length * met[i].first;

        for (let j = met[i].tick; j < next_tick; j += measure_length) {
            chart["BEAT_1"].push(new Measure(j, measure_id++))

            for (let k = j + beat_length; k < j + measure_length && k < next_tick; k += beat_length) {
                chart["BEAT_2"].push(new Beat(k));
            }
        }
    }
}

function UpdateSldHead(chart: Chart) {
    let sld_end: { [key: string]: boolean } = {};

    let slides = chart['SLD'].concat(chart['SLC']) as Slide[];

    for (const slide of slides) {
        let tick_end     = slide.tick_end;
        let target_cell  = slide.cell_target;
        let target_width = slide.width_target;

        sld_end[[tick_end, target_cell, target_width].toString()] = true;
    }

    for (const slide of slides) {
        let tick  = slide.tick;
        let cell  = slide.cell;
        let width = slide.width;

        if (sld_end[[tick, cell, width].toString()]) {
            continue;
        }

        chart[slide.extra == NoteType.Ex ? 'CHR' : 'SLD_H'].push(new Rice(tick, cell, width));
    }
}

export function GetMaxTick(chart: Chart) {
    let tick_max = 0;

    for (let key of Object.keys(chart)) {
        for (let i = 0; i < chart[key].length; i++) {

            let note = chart[key][i];

            tick_max = Math.max(tick_max, note.tick);

            if (cat_slide.indexOf(key) != -1 || cat_noodle.indexOf(key) != -1) {
                tick_max = Math.max(tick_max, (note as Noodle).tick_end);
            }

            if (key == "SFL") {
                tick_max = Math.max(tick_max, (note as SpeedVelocity).tick_end);
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
    let bpm_before = (chart['BPM'][0] as Bpm).bpm;

    for (let i = 0; i < chart['BPM'].length; i++) {
        let bpm = (chart['BPM'][i] as Bpm);

        if (bpm.bpm == bpm_before) continue;
        if (bpm.bpm == 0) continue;

        ScaleTickFrom(chart, bpm_before / bpm.bpm, bpm.tick);

        bpm_before = bpm.bpm;
    }
}

export function ScaleTickFrom(chart: Chart, scale: number, tick: number = 0) {
    for (let key of Object.keys(chart)) {
        for (let j = 0; j < chart[key].length; j++) {
            let note = chart[key][j];

            if (note.tick < tick) continue;

            note.tick = (note.tick - tick) * scale + tick;

            if (cat_slide.indexOf(key) != -1 || cat_noodle.indexOf(key) != -1 || key == "SFL") {
                let ln = note as Noodle;

                ln.tick_end = (ln.tick_end - tick) * scale + tick;
            }
        }
    }
}

function ParseLine(chart: Chart, data: any) {
    let key = data[0];
    switch (key) {
        case 'HLD':
        case "HXD":
            ParseLn(chart, data as LnData);
            break;
        case 'AHD':
        case "AHX":
            ParseAirHold(chart, data as AirHoldData);
            break;
        case "SLD":
        case "SLC":
        case "SXD":
        case "SXC":
            ParseSld(chart, data as SldData);
            break;
        case "ASC":
        case "ASD":
            ParseAirSld(chart, data as AirSldData);
            break;
        case "ALD":
            ParseAld(chart, data as AldData);
            break;
        case "BPM":
            chart["BPM"].push(new Bpm(ToTick(data[1] as number, data[2] as number), data[3] as number));
            break;
        case "MET":
            chart["MET"].push(new Met(ToTick(data[1] as number, data[2] as number), data[4] as number, data[3] as number));
            break;
        case "BPM_DEF":
            chart["BPM"].push(new Bpm(0, data[1] as number));
            break;
        case "MET_DEF":
            chart["MET"].push(new Met(0, data[2] as number, data[1] as number));
            break;
        case "SFL":
            let tick = ToTick(data[1] as number, data[2] as number);

            chart["SFL"].push(new SpeedVelocity(tick, data[3] as number + tick, data[4] as number));
            break;
        default:
            chart[key].push(
                new Rice(ToTick(data[1] as number, data[2] as number), data[3] as number, data[4] as number)
            );
            break;
    }
}

function ParseLn(chart: Chart, data: LnData) {
    let d = {
        measure: data[1], offset: data[2], cell: data[3], width: data[4], duration: data[5]
    };

    let tick     = ToTick(d.measure, d.offset);
    let tick_end = tick + d.duration;

    let lnH = new Rice(tick, d.cell, d.width);
    let lnT = new Rice(tick_end, d.cell, d.width)

    if (data[0] == "HLD") {
        chart['HLD_H'].push(lnH);
    } else {
        chart["CHR"].push(lnT);
    }

    chart["HLD_T"].push(lnT);
    chart['HLD_B'].push(new Noodle(tick, d.cell, d.width, tick_end));
}

function ParseAirHold(chart: Chart, data: AirHoldData) {
    let d = {
        measure: data[1], offset: data[2], cell: data[3], width: data[4], type: data[5], duration: data[6]
    };

    let tick     = ToTick(d.measure, d.offset);
    let tick_end = tick + d.duration;

    if (d.type != 'AHD' && d.type != 'AHX') {
        chart['AHD_H'].push(new Rice(tick, d.cell, d.width));
    }

    chart['AHD_T'].push(new Rice(tick_end, d.cell, d.width));
    chart['AHD_B'].push(new Noodle(tick, d.cell, d.width, tick_end));
}

function ParseSld(chart: Chart, data: SldData) {
    let d = {
        measure: data[1], offset: data[2], cell: data[3], width: data[4], duration: data[5],
        target_cell: data[6], target_width: data[7]
    };

    let tick     = ToTick(d.measure, d.offset);
    let tick_end = tick + d.duration;

    if (data[0] == "SLD" || data[0] == "SXD") {
        chart['SLD_T'].push(new Rice(tick_end, d.target_cell, d.target_width));
    }

    chart[data[0].replace('X', 'L')].push(new Slide(
        tick, d.cell, d.width, tick_end, d.target_cell, d.target_width,
        data[0][1] == 'X' ? NoteType.Ex : NoteType.Normal)
    );
}

function ParseAirSld(chart: Chart, data: AirSldData) {
    let d = {
        measure: data[1], offset: data[2], cell: data[3], width: data[4], type: data[5],
        height: data[6], duration: data[7], target_cell: data[8], target_width: data[9], target_height: data[10],
        color: data[11]
    };

    let tick     = ToTick(d.measure, d.offset);
    let tick_end = tick + d.duration;

    if (data[0] == "ASD") {
        chart['ASD_T'].push(new Rice(tick_end, d.target_cell, d.target_width));
    }

    if (d.type != 'ASC' && d.type != 'ASD' && d.type != 'AHD') {
        chart[data[0] + '_H'].push(new Rice(tick, d.cell, d.width));
    }

    chart[data[0]].push(new Slide(tick, d.cell, d.width, tick_end, d.target_cell, d.target_width, d.color));
}

function ParseAld(chart: Chart, data: AldData) {
    let d = {
        measure: data[1], offset: data[2], cell: data[3], width: data[4], interval: data[5],
        height: data[6], duration: data[7], target_cell: data[8], target_width: data[9], target_height: data[10],
        color: data[11]
    };

    let tick     = ToTick(d.measure, d.offset);
    let tick_end = tick + d.duration;

    chart[data[0]].push(new Slide(tick, d.cell, d.width, tick_end, d.target_cell, d.target_width, d.color));

    if (d.interval == 0) return;

    for (let duration = 0; duration < d.duration; duration += d.interval) {
        let ratio = duration / d.duration;
        let cell  = (d.target_cell - d.cell) * ratio + d.cell;
        let width = (d.target_width - d.width) * ratio + d.width;

        chart['ALD_T'].push(new Rice(tick + duration, cell, width));
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
    return measure * resolution + offset;
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
 * @param extra
 */
function SplitSldBody(
    tick_start: number, tick_split: number, duration: number, cell: number, width: number,
    target_cell: number, target_width: number, extra: string | NoteType
) {
    let result = [];

    let ratio     = (tick_split - tick_start) / duration;
    let mid_cell  = (target_cell - cell) * ratio + cell;
    let mid_width = (target_width - width) * ratio + width;

    result.push(new Slide(
        tick_start, cell, width, tick_split, mid_cell, mid_width, extra)
    );
    result.push(new Slide(
        tick_split, mid_cell, mid_width, tick_start + duration, target_cell, target_width, extra)
    );

    return result;
}

/**
 * 在 tick_split 处将 HLD/AHD 分割为两部分
 * @param tick_start
 * @param tick_split
 * @param duration
 * @param cell
 * @param width
 */
function SplitLnBody(tick_start: number, tick_split: number, duration: number, cell: number, width: number) {
    return [
        new Noodle(tick_start, cell, width, tick_split),
        new Noodle(tick_split, cell, width, tick_start + duration)
    ];
}

function SplitChart(chart: Chart, key: string, tick: number, get_tick_e: (x: NotePublic) => number, fun: (x: NotePublic) => NotePublic[]) {
    let to_preserve = [];
    let to_add      = [];

    for (let i = 0; i < chart[key].length; i++) {
        let tick_s = chart[key][i].tick;
        let tick_e = get_tick_e(chart[key][i]);

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
 * @param chart
 */
function SplitLnToFitBpmScale(chart: Chart) {
    for (let bpm of chart['BPM']) {
        SplitChartAt(chart, bpm.tick);
    }
}

export function SplitChartAt(chart: Chart, tick: number): void {
    for (let key of cat_noodle) {
        SplitChart(chart, key, tick,
            x => (x as Noodle).tick_end,
            data => {
                let note = data as Noodle;
                return SplitLnBody(note.tick, tick, note.tick_end - note.tick, note.cell, note.width)
            },
        );
    }

    for (let key of cat_slide) {
        SplitChart(chart, key, tick,
            x => (x as Slide).tick_end,
            data => {
                let note = data as Slide;
                return SplitSldBody(
                    note.tick, tick, note.tick_end - note.tick,
                    note.cell, note.width, note.cell_target, note.width_target, note.extra
                )
            }
        );
    }

    SplitChart(chart, "SFL", tick,
        x => (x as SpeedVelocity).tick_end,
        data => {
            let note = data as SpeedVelocity;
            return [
                new SpeedVelocity(note.tick, tick, note.velocity),
                new SpeedVelocity(tick, note.tick_end, note.velocity)
            ]
        }
    );
}