import {sort} from "d3";
import {NullOrWhitespace} from "@/utils/str";
import {
    BeatmapBeat,
    BeatmapBpm,
    BeatmapLn,
    BeatmapMeasure, BeatmapMet, BeatmapRice,
    BeatmapSpeedVelocity
} from "@/components/utils/BeatmapVisualizer/BeatmapTypes";

type TimingPoint = {
    offset: number;
    value: number;
    meter: number;
    inherited: boolean;
    kiai: boolean;
};

type HitObject = {
    x: number,
    y: number,
    time: number,
    type: number,
    extras: string
}

const scale = 2;

export function parse(beatmap_string: string) {
    let beatmap = extractTiming_HitObj(beatmap_string);

    // console.log('parsing hit objects')
    let [rice, ln] = generateHitObj(beatmap.hitObjects, beatmap.keyCount)

    let length = findBeatmapLength(rice, ln);

    // console.log('parsing control')
    let [sv, bpm, measure, beat] = generateControl(beatmap.timings, length)
    // console.log('done')

    return {
        rice, ln, sv, bpm, measure, beat, length,
        key_count: beatmap.keyCount,
    };
}

function extractTiming_HitObj(beatmap_string: string) {
    enum ParseState {TimingPoints, HitObjects, Metadata, Skip}

    let timings    = [];
    let hitObjects = [];
    let keyCount   = 0;

    let lines = beatmap_string.split("\n");

    let stat = ParseState.Skip;

    for (let line of lines) {
        if (line.startsWith("[TimingPoints]")) {
            stat = ParseState.TimingPoints;
            continue;
        } else if (line.startsWith("[HitObjects]")) {
            stat = ParseState.HitObjects;
            continue;
        } else if (line.startsWith("[Metadata]")) {
            stat = ParseState.Metadata;
            continue;
        }

        if (line.startsWith("//")) continue;
        if (NullOrWhitespace(line)) continue;

        if (stat == ParseState.TimingPoints) {
            let timing = line.split(",");
            timings.push({
                offset   : Math.floor(parseFloat(timing[0]) / scale),
                value    : parseFloat(timing[1]),
                meter    : parseInt(timing[2]),
                inherited: !parseInt(timing[6]),
                kiai     : !!parseInt(timing[7]),
            } as TimingPoint);
        } else if (stat == ParseState.HitObjects) {
            let hit = line.split(",");
            hitObjects.push({
                x     : parseInt(hit[0]),
                y     : parseInt(hit[1]),
                time  : Math.floor(parseInt(hit[2]) / scale),
                type  : parseInt(hit[3]),
                extras: hit.slice(5).join(','),
            } as HitObject);
        }

        if (line.startsWith("CircleSize")) {
            keyCount = parseInt(line.split(':')[1]);
        }
    }

    return {timings, hitObjects, keyCount};
}

function findBeatmapLength(rice: BeatmapRice[], ln: BeatmapLn[]) {
    let max = 0;

    for (let r of rice) {
        max = Math.max(max, r.Tick);
    }

    for (let l of ln) {
        max = Math.max(max, l.TickEnd);
    }

    return max;
}

function generateHitObj(hitObjects: HitObject[], keyCount: number) {
    let rice = [];
    let ln   = [];

    let w = 1 / keyCount;

    for (let h of hitObjects) {
        let x = Math.floor(h.x * keyCount / 512);
        if (h.type == 128) {
            let end_time = Math.floor(parseInt(h.extras.split(':')[0]) / scale);
            ln.push(new BeatmapLn(h.time, end_time, x, w));
            // rice.push(new BeatmapRice(end_time, x, w));
            rice.push(new BeatmapRice(h.time, x, w));

        } else {
            rice.push(new BeatmapRice(h.time, x, w));
        }
    }
    return [rice, ln] as const;
}

function generateControl(timingPoints: TimingPoint[], length: number) {
    sort(timingPoints, (a, b) => a.offset - b.offset);

    let timings = [] as { offset: number; bpm: number; meter: number; kiai: boolean; sv: number, inherited: boolean }[];

    ////////////////////////////////////////////
    // 把原始的timingPoints转换成易于处理的对象
    ////////////////////////////////////////////
    for (let i = 0; i < timingPoints.length; i++) {
        if (timings.length == 0 && timingPoints[i].inherited) continue;

        if (timingPoints[i].inherited) {
            timings.push({
                ...timings[timings.length - 1],
                offset   : timingPoints[i].offset,
                kiai     : timingPoints[i].kiai,
                sv       : ((-100 / timingPoints[i].value) * timings[timings.length - 1].bpm) / timings[0].bpm,
                inherited: true
            });
        } else {
            let bpm = 60000 / timingPoints[i].value;

            timings.push({
                offset   : timingPoints[i].offset,
                bpm      : bpm,
                meter    : timingPoints[i].meter,
                kiai     : timingPoints[i].kiai,
                sv       : bpm / (timings.length > 0 ? timings[timings.length - 1].bpm : bpm),
                inherited: false
            });
        }
    }

    if (timings[0].offset != 0) {
        timings.unshift({
            ...timings[0],
            offset: 0,
        });
    }

    ////////////////////////////////////////////
    // 构造结构
    ////////////////////////////////////////////

    // sv
    let sv_res = [] as BeatmapSpeedVelocity[];
    for (let i = 1; i < timings.length; i++) {
        sv_res.push(new BeatmapSpeedVelocity(timings[i - 1].offset, timings[i].offset, timings[i - 1].sv));
    }

    sv_res.push(new BeatmapSpeedVelocity(timings[timings.length - 1].offset, Infinity, timings[timings.length - 1].sv));
    sv_res = sv_res.filter(x => x.TickEnd - x.Tick > 0);

    // bpm
    let bpm_res = [] as BeatmapBpm[];
    for (let i = 0; i < timings.length; i++) {
        bpm_res.push(new BeatmapBpm(timings[i].offset, timings[i].bpm));
    }
    // 去掉bpm相同且tick相同的点
    bpm_res = bpm_res.filter((x, i) => i == 0 || (x.Bpm != bpm_res[i - 1].Bpm || x.Tick != bpm_res[i - 1].Tick));

    // measure & beat
    let measure_res = [] as BeatmapMeasure[];
    let beat_res    = [] as BeatmapBeat[];
    let measure_id  = 0;

    let t = timings.filter(x => !x.inherited);

    for (let i = 0; i < t.length; i++) {
        let next = i == t.length - 1 ? length : t[i + 1].offset;

        let beat_length    = 60000 / t[i].bpm / scale;
        let measure_length = beat_length * t[i].meter;

        if (beat_length < 2 || measure_length < 2) continue;

        let offset = t[i].offset;

        for (let j = offset; j < next && j < length; j += measure_length) {
            measure_res.push(new BeatmapMeasure(j, ++measure_id, new BeatmapMet(offset, t[i].meter, 4)));
            for (let k = j + beat_length; k < j + measure_length && k < length && k < next; k += beat_length) {
                beat_res.push(new BeatmapBeat(k, measure_id - 1));
            }
        }
    }

    return [sv_res, bpm_res, measure_res, beat_res] as const;
}
