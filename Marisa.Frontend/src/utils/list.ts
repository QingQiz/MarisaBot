export function range(stop: number): number[];
export function range(start: number, stop: number): number[];
export function range(start: number, stop: number, step: number): number[];

export function range(start: number, stop?: number, step?: number): number[] {
    if (stop === undefined) {
        stop = start;
        start = 0;
    }

    if (step === undefined) {
        step = start < stop ? 1 : -1;
    }

    const length = Math.max(Math.ceil((stop - start) / step), 0);
    const range = Array(length);

    for (let i = 0; i < length; i++) {
        range[i] = start + i * step;
    }

    return range;
}

type ZipReturn<T extends any[]> = T[0] extends infer A
    ? {
        [K in keyof A]: [...{
            [K2 in keyof T]: T[K2][K & keyof T[K2]]
        }]
    }
    : never


export function zip<
    T extends [...{ [K in keyof S]: S[K] }][], S extends any[]
>(arr: [...T]): ZipReturn<T> {
    const maxLength = Math.max(...arr.map((x) => x.length));

    return arr.reduce(
        (acc: any, val) => {
            val.forEach((v, i) => acc[i].push(v));

            return acc;
        },
        range(maxLength).map(() => [])
    );
}
