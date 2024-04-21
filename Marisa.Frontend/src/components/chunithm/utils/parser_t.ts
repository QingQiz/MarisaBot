export  class NotePublic {
    tick: number;

    constructor(tick: number) {
        this.tick = tick;
    }
}

export class Bpm extends NotePublic {
    bpm: number;

    constructor(tick: number, bpm: number) {
        super(tick);
        this.bpm = bpm;
    }

    toString() {
        return `${this.bpm}`;
    }
}

export class Measure extends NotePublic {
    id: number;

    constructor(tick: number, id: number) {
        super(tick);
        this.id = id;
    }

    toString() {
        return `#${this.id}`;
    }
}

export class Beat extends NotePublic {
    constructor(tick: number) {
        super(tick);
    }

    toString() {
        return '';
    }
}


export class Met extends NotePublic {
    first: number;
    second: number;

    constructor(tick: number, first: number, second: number) {
        super(tick);
        this.first  = first;
        this.second = second;
    }
}

export class Rice extends NotePublic {
    cell: number;
    width: number;

    constructor(tick: number, cell: number, width: number) {
        super(tick);
        this.cell  = cell;
        this.width = width;
    }
}

export class Noodle extends Rice {
    tick_end: number;

    constructor(tick: number, cell: number, width: number, tick_end: number) {
        super(tick, cell, width);
        this.tick_end = tick_end;
    }
}

export class SpeedVelocity extends NotePublic {
    velocity: number;
    tick_end: number;

    constructor(tick: number, tick_end: number, velocity: number) {
        super(tick);
        this.tick_end = tick_end;
        this.velocity = velocity;
    }

}

class SlidePublic extends Noodle {
    cell_target: number;
    width_target: number;

    constructor(tick: number, cell: number, width: number, tick_end: number, cell_target: number, width_target: number) {
        super(tick, cell, width, tick_end);
        this.cell_target  = cell_target;
        this.width_target = width_target;
    }
}

export enum NoteType {
    Normal,
    Ex,
}

export class Slide extends SlidePublic {
    extra: NoteType | string

    constructor(tick: number, cell: number, width: number, tick_end: number, cell_target: number, width_target: number, extra: NoteType | string) {
        super(tick, cell, width, tick_end, cell_target, width_target);
        this.extra = extra;
    }
}
