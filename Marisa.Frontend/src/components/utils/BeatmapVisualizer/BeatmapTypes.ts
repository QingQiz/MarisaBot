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

export class BeatmapBeat extends Beatmap {
    MeasureId: number;

    constructor(Tick: number, MeasureId: number) {
        super(Tick);
        this.MeasureId = MeasureId;
    }
}

export class BeatmapMeasure extends Beatmap {
    Id: number;
    Met: BeatmapMet

    constructor(Tick: number, Id: number, Met: BeatmapMet) {
        super(Tick);
        this.Id = Id;
        this.Met = Met;
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
