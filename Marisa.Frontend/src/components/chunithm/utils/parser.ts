import {
    Beatmap,
    BeatmapBeat, BeatmapBpm,
    BeatmapDiv, BeatmapLn,
    BeatmapMeasure,
    BeatmapMet, BeatmapRice, BeatmapSlideUnit, BeatmapSpeedVelocity
} from "@/components/utils/BeatmapVisualizer/BeatmapTypes";

export type Chart = { [key: string]: Beatmap[] };
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
    "HLD_H", "HLD_T", "HLD_B", "AHD_H", "AHD_T", "AHD_B", "SLD_H", "SLD_T", "ASD_H", "ASC_H", "ASD_T", "ALD_T",
    //
    "BEAT_1", "BEAT_2", "DIV",
];

export const cat_rice   = [
    "TAP", "CHR", "MNE", "FLK",
    "HLD_H", "HLD_T", "AHD_H", "ASC_H", "ASD_H", "AHD_T", "SLD_H", "SLD_T", "ASD_T", "ALD_T",
    "AIR", "AUR", "AUL", "ADW", "ADR", "ADL",
];
export const cat_noodle = ["HLD_B", "AHD_B"];
export const cat_slide  = ["SLC", "SLD", "ASC", "ASD", "ALD"];

export const resolution = 384;
const cell_count        = 16;


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

    UpdateSlide(chart);
    MakeBeatLine(chart);
    MakeNoteGapLine(chart);

    ScaleChartTickByBpm(chart);

    return chart;
}


function MakeNoteGapLine(chart: Chart) {
    function gcd(a: number, b: number): number {
        if (b == 0) return a;
        return gcd(b, a % b);
    }

    let ticks = [] as number[];
    let beats = chart["BEAT_1"].map(x => x.Tick);

    for (let key of cat_rice) {
        if (key == "SLD_T") continue;
        for (let note of chart[key]) {
            ticks.push(note.Tick);
        }
    }

    beats.sort((a, b) => a - b);
    ticks.sort((a, b) => a - b);
    ticks = ticks.filter((x, i) => i == 0 || x != ticks[i - 1]);

    let sep = ticks.map(x => {
        let l = 0, r = beats.length;

        while (l <= r) {
            let mid = Math.floor((l + r) / 2);
            if (beats[mid] <= x) {
                l = mid + 1;
            } else {
                r = mid - 1;
            }
        }

        return [l - 1, x - beats[l - 1]]
    });

    let no_skip = [0];

    for (let i = 0, j = 1; j < sep.length; i++, j++) {
        if (sep[i][0] == sep[j][0] && sep[i][1] + 1 == sep[j][1]) {
            continue;
        }
        no_skip.push(j);
    }

    sep = no_skip.map(x => sep[x]);

    for (let i = 0, j = 1; i < sep.length; i++, j++) {
        let tick_i = sep[i][1];
        let tick_j = j == sep.length ? resolution + tick_i : sep[j][1];

        if (j < sep.length && sep[i][0] != sep[j][0]) tick_j = beats[sep[i][0] + 1] - beats[sep[i][0]];

        let gap = tick_j - tick_i;
        let div = gcd(gap, resolution);

        chart["DIV"].push(new BeatmapDiv(beats[sep[i][0]] + tick_i, gap / div, resolution / div));
    }
}

function MakeBeatLine(chart: Chart) {
    let max_tick = GetMaxTick(chart);

    let met = (chart["MET"] as BeatmapMet[]).filter(x => x.First != 0 && x.Second != 0);

    met.sort((a, b) => a.Tick - b.Tick);

    let measure_id = 0;

    for (let i = 0; i < met.length; i++) {
        let next_tick = i == met.length - 1 ? max_tick : met[i + 1].Tick;

        let beat_length    = resolution / met[i].Second;
        let measure_length = beat_length * met[i].First;

        for (let j = met[i].Tick; j < next_tick; j += measure_length) {
            for (let k = j + beat_length; k < j + measure_length && k < next_tick; k += beat_length) {
                chart["BEAT_2"].push(new BeatmapBeat(k, measure_id));
            }

            chart["BEAT_1"].push(new BeatmapMeasure(j, measure_id++, met[i]))
        }
    }
}

/**
 * 更新 Slide， 包括设置 Slide 起点的 Note 和 SlideUnit 的百分比
 * @param chart
 */
function UpdateSlide(chart: Chart) {
    let sld_end: { [key: string]: []} = {};
    let sld_beg: { [key: string]: []} = {};

    let slides = [...chart["SLD"], ...chart["SLC"], ...chart["SXD"], ...chart["SXC"]] as BeatmapSlideUnit[]
    let keys   = [...chart["SLD"].map(_ => "SLD"), ...chart["SLC"].map(_ => "SLC"), ...chart["SXD"].map(_ => "SXD"), ...chart["SXC"].map(_ => "SXC")]

    for (let i = 0; i < slides.length; i++){
        const slide      = slides[i];
        let tick_end     = slide.TickEnd;
        let target_cell  = slide.XEnd;
        let target_width = slide.WidthEnd;

        let key = [tick_end, target_cell, target_width].toString();
        if (!sld_end[key]) {
            sld_end[key] = [];
        }
        sld_end[key].push(i);
    }

    for (let i = 0; i < slides.length; i++){
        const slide      = slides[i];
        let tick     = slide.Tick;
        let cell     = slide.X;
        let width    = slide.Width;
        let key = [tick, cell, width].toString();
        if (!sld_beg[key]) {
            sld_beg[key] = [];
        }
        sld_beg[key].push(i);
    }

    let slide_head = new Set<number>();
    for (let i = 0; i < slides.length; i++) {
        const slide = slides[i];
        let tick    = slide.Tick;
        let cell    = slide.X;
        let width   = slide.Width;

        if (sld_end[[tick, cell, width].toString()]) {
            continue;
        }

        // 补全 slide 头
        chart[keys[i][1] == 'X' ? 'CHR' : 'SLD_H'].push(new BeatmapRice(slide.Tick, cell, width));
        slide_head.add(i);
    }

    // SLD 结尾是新 slide 的开头
    let visit = new Set<number>();
    for (let i = 0; i < slides.length; i++) {
        if (keys[i][2] == 'D') {
            let key = [slides[i].TickEnd, slides[i].XEnd, slides[i].WidthEnd].toString();
            if (!sld_beg[key]) continue;

            for (let j of sld_beg[key]) {
                if (visit.has(j)) continue;
                slide_head.add(j);
                visit.add(j);
            }
        }
    }

    let full_slide = [];
    for (let h of slide_head) {
        let current = [];

        let current_slide_idx = h;
        while (current_slide_idx != undefined) {
            let current_slide = slides[current_slide_idx];
            let tick_end      = current_slide.TickEnd;
            let target_cell   = current_slide.XEnd;
            let target_width  = current_slide.WidthEnd;
            let key = [tick_end, target_cell, target_width].toString();
            current.push(current_slide_idx);
            if (!sld_beg[key]) break;
            current_slide_idx = sld_beg[key].shift();
            if (slide_head.has(current_slide_idx)) break;
        }
        full_slide.push(current);
    }

    for (let slide of full_slide) {
        let tick_min = Math.min(...slide.map(x => slides[x].Tick));
        let tick_max = Math.max(...slide.map(x => slides[x].TickEnd));
        for (let i of slide) {
            let slide_unit = slides[i];
            slide_unit.UnitStart = (slide_unit.Tick - tick_min) / (tick_max - tick_min);
            slide_unit.UnitEnd = (slide_unit.TickEnd - tick_min) / (tick_max - tick_min);
        }
    }
}

export function GetMaxTick(chart: Chart) {
    let tick_max = 0;

    for (let key of Object.keys(chart)) {
        for (let i = 0; i < chart[key].length; i++) {

            let note = chart[key][i];

            tick_max = Math.max(tick_max, note.Tick);

            if (cat_slide.indexOf(key) != -1 || cat_noodle.indexOf(key) != -1) {
                tick_max = Math.max(tick_max, (note as BeatmapLn).TickEnd);
            }

            if (key == "SFL") {
                tick_max = Math.max(tick_max, (note as BeatmapSpeedVelocity).TickEnd);
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
    let bpm_before = (chart['BPM'][0] as BeatmapBpm).Bpm;

    for (let i = 0; i < chart['BPM'].length; i++) {
        let bpm = (chart['BPM'][i] as BeatmapBpm);

        if (bpm.Bpm == bpm_before) continue;
        if (bpm.Bpm == 0) continue;

        ScaleTickFrom(chart, bpm_before / bpm.Bpm, bpm.Tick);

        bpm_before = bpm.Bpm;
    }
}

export function ScaleTickFrom(chart: Chart, scale: number, tick: number = 0) {
    for (let key of Object.keys(chart)) {
        for (let j = 0; j < chart[key].length; j++) {
            let note = chart[key][j] as any;

            if (note.Tick >= tick) {
                note.Tick = (note.Tick - tick) * scale + tick;
            }

            if ('TickEnd' in note && note.TickEnd >= tick) {
                note.TickEnd = (note.TickEnd - tick) * scale + tick;
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
            chart["BPM"].push(new BeatmapBpm(ToTick(data[1] as number, data[2] as number), data[3] as number));
            break;
        case "MET":
            chart["MET"].push(new BeatmapMet(ToTick(data[1] as number, data[2] as number), data[4] as number, data[3] as number));
            break;
        case "BPM_DEF":
            chart["BPM"].push(new BeatmapBpm(0, data[1] as number));
            break;
        case "MET_DEF":
            chart["MET"].push(new BeatmapMet(0, data[2] as number, data[1] as number));
            break;
        case "SFL":
            let measure  = data[1] as number;
            let offset   = data[2] as number;
            let duration = data[3] as number;

            while (duration > 0) {
                if (offset + duration > resolution) {
                    chart["SFL"].push(new BeatmapSpeedVelocity(ToTick(measure, offset), ToTick(measure, resolution), data[4] as number));
                    duration -= resolution - offset;
                    measure++;
                    offset = 0;
                } else {
                    chart["SFL"].push(new BeatmapSpeedVelocity(ToTick(measure, offset), ToTick(measure, offset + duration), data[4] as number));
                    duration = 0;
                }
            }

            break;
        default:
            chart[key].push(
                new BeatmapRice(ToTick(data[1] as number, data[2] as number), data[3] as number / cell_count, data[4] as number / cell_count)
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

    let lnH = new BeatmapRice(tick, d.cell / cell_count, d.width / cell_count);
    let lnT = new BeatmapRice(tick_end, d.cell / cell_count, d.width / cell_count)

    if (data[0][1] == 'X') {
        chart["CHR"].push(lnH);
    } else {
        chart['HLD_H'].push(lnH);
    }

    chart["HLD_T"].push(lnT);
    chart['HLD_B'].push(new BeatmapLn(tick, tick_end, d.cell / cell_count, d.width / cell_count));
}

function ParseAirHold(chart: Chart, data: AirHoldData) {
    let d = {
        measure: data[1], offset: data[2], cell: data[3], width: data[4], type: data[5], duration: data[6]
    };

    let tick     = ToTick(d.measure, d.offset);
    let tick_end = tick + d.duration;

    if (d.type != 'AHD' && d.type != 'AHX') {
        chart['AHD_H'].push(new BeatmapRice(tick, d.cell / cell_count, d.width / cell_count));
    }

    chart['AHD_T'].push(new BeatmapRice(tick_end, d.cell / cell_count, d.width / cell_count));
    chart['AHD_B'].push(new BeatmapLn(tick, tick_end, d.cell / cell_count, d.width / cell_count));
}

function ParseSld(chart: Chart, data: SldData) {
    let d = {
        measure     : data[1],
        offset      : data[2],
        cell        : data[3],
        width       : data[4],
        duration    : data[5],
        target_cell : data[6],
        target_width: data[7]
    };

    let tick     = ToTick(d.measure, d.offset);
    let tick_end = tick + d.duration;

    if (data[0] == "SLD" || data[0] == "SXD") {
        chart['SLD_T'].push(new BeatmapRice(tick_end, d.target_cell / cell_count, d.target_width / cell_count));
    }

    chart[data[0]].push(new BeatmapSlideUnit(
        tick, tick_end, d.cell / cell_count, d.target_cell / cell_count, d.width / cell_count, d.target_width / cell_count,
    ));
}

function ParseAirSld(chart: Chart, data: AirSldData) {
    let d = {
        measure      : data[1],
        offset       : data[2],
        cell         : data[3],
        width        : data[4],
        type         : data[5],
        height       : data[6],
        duration     : data[7],
        target_cell  : data[8],
        target_width : data[9],
        target_height: data[10],
        color        : data[11]
    };

    let tick     = ToTick(d.measure, d.offset);
    let tick_end = tick + d.duration;

    if (data[0] == "ASD") {
        chart['ASD_T'].push(new BeatmapRice(tick_end, d.target_cell / cell_count, d.target_width / cell_count));
    }

    if (d.type != 'ASC' && d.type != 'ASD' && d.type != 'AHD') {
        chart[data[0] + '_H'].push(new BeatmapRice(tick, d.cell / cell_count, d.width / cell_count));
    }

    chart[data[0]].push({
        ...new BeatmapSlideUnit(tick, tick_end, d.cell / cell_count, d.target_cell / cell_count, d.width / cell_count, d.target_width / cell_count),
        ...{Color: d.color}
    });
}

function ParseAld(chart: Chart, data: AldData) {
    let d = {
        measure      : data[1],
        offset       : data[2],
        cell         : data[3],
        width        : data[4],
        interval     : data[5],
        height       : data[6],
        duration     : data[7],
        target_cell  : data[8],
        target_width : data[9],
        target_height: data[10],
        color        : data[11]
    };

    let tick     = ToTick(d.measure, d.offset);
    let tick_end = tick + d.duration;

    if (d.color != 'NON') {
        chart[data[0]].push({
            ...new BeatmapSlideUnit(tick, tick_end, d.cell / cell_count, d.target_cell / cell_count, d.width / cell_count, d.target_width / cell_count),
            ...{Color: d.color}
        });
    }

    if (d.interval == 0) return;

    for (let duration = 0; duration < d.duration; duration += d.interval) {
        let ratio = duration / d.duration;
        let cell  = (d.target_cell - d.cell) * ratio + d.cell;
        let width = (d.target_width - d.width) * ratio + d.width;

        chart['ALD_T'].push(new BeatmapRice(tick + duration, cell / cell_count, width / cell_count));
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
