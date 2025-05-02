export class Beatmap {
    Tick: number;

    constructor(Tick: number) {
        this.Tick = Tick;
    }
}

class BeatmapNoteCommon extends Beatmap {
    X: number;
    Width: number;

    constructor(X: number, Tick: number, Width: number) {
        super(Tick);
        this.X = X;
        this.Width = Width;
    }
}

export class BeatmapRice extends BeatmapNoteCommon {
    constructor(Tick: number, X: number, Width: number) {
        super(X, Tick, Width);
    }
}

export class BeatmapLn extends BeatmapNoteCommon {
    TickEnd: number;

    /**
     * @param Tick 起始时间
     * @param TickEnd 结束时间
     * @param X 起始位置（百分比）
     * @param Width 宽度（百分比）
     */
    constructor(Tick: number, TickEnd: number, X: number, Width: number) {
        super(X, Tick, Width);
        this.TickEnd = TickEnd;
    }
}

// SlideUnit， 一个 Slide 由多个首尾相连的 SlideUnit 组成
export class BeatmapSlideUnit extends BeatmapLn {
    XEnd: number;
    WidthEnd: number;
    Color?: string;
    // 在整个slide中的位置百分比
    UnitStart: number;
    // 在整个slide中的结束位置百分比
    UnitEnd: number;

    private readonly _border: number[];

    constructor(Tick: number, TickEnd: number, X: number, XEnd: number, Width: number, WidthEnd: number) {
        super(Tick, TickEnd, X, Width);
        this.XEnd = XEnd;
        this.WidthEnd = WidthEnd;
        this._border = [Math.min(this.X, this.XEnd), Math.max(this.X + this.Width, this.XEnd + this.WidthEnd)]
    }

    // 最小的能包裹slide的矩形
    Border = () => this._border;
}

export class BeatmapSpeedVelocity extends Beatmap {
    Velocity: number;
    TickEnd: number;

    constructor(Tick: number, TickEnd:number, Velocity: number) {
        super(Tick);
        this.Velocity = Velocity;
        this.TickEnd = TickEnd;
    }
}

export class BeatmapSpeedVelocity2 extends BeatmapLn {
    SVs: BeatmapSpeedVelocity[];

    /**
     * @param Tick 起始时间
     * @param TickEnd 结束时间
     * @param X 起始位置（百分比）
     * @param Width 宽度（百分比）
     * @param SVs
     */
    constructor(Tick: number, TickEnd: number, X: number, Width: number, SVs: BeatmapSpeedVelocity[]) {
        super(Tick, TickEnd, X, Width);
        this.SVs = SVs.filter(x => x.Tick <= TickEnd);
    }
}

export class BeatmapBeat extends Beatmap {
    MeasureId: number;
    TickEnd: number;

    constructor(Tick: number, MeasureId: number, TickEnd: number) {
        super(Tick);
        this.MeasureId = MeasureId;
        this.TickEnd = TickEnd;
    }
}

export class BeatmapMeasure extends Beatmap {
    Id: number;
    Met: BeatmapMet;
    TickEnd: number;

    constructor(Tick: number, Id: number, Met: BeatmapMet, TickEnd: number) {
        super(Tick);
        this.Id = Id;
        this.Met = Met;
        this.TickEnd = TickEnd;
    }
}

export class BeatmapBpm extends Beatmap {
    Bpm: number

    constructor(Tick: number, bpm: number) {
        super(Tick);
        this.Bpm = bpm;
    }
}

export class BeatmapDiv extends Beatmap {
    First: number;
    Second: number;

    constructor(Tick: number, First: number, Second: number) {
        super(Tick);
        this.First = First;
        this.Second = Second;
    }
}

export class BeatmapMet extends Beatmap {
    First: number;
    Second: number;

    constructor(Tick: number, First: number, Second: number) {
        super(Tick);
        this.First  = First;
        this.Second = Second;
    }
}
