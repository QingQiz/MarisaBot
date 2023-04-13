export {}

declare global {
    interface Number {
        toPercentage(): string;
    }
}

Number.prototype.toPercentage = function (this: number) {
    return (this * 100).toFixed(2) + '%'
}
