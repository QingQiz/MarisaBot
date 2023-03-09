const host = "http://localhost:14311"

const maimai_newRa = host + "/MaiMai/RaNew"

const maimai_levelColors = [
    '#52e72b',
    '#ffa801',
    '#ff5a66',
    '#c64fe4',
    '#dbaaff'
]

function maimai_alternativeCover(id: number) {
    return [
        `/assets/maimai/cover/${id}.png`,
        `/assets/maimai/cover/${id}.jpg`,
        `/assets/maimai/cover/${(id?? 0) + 10000}.jpg`,
        `/assets/maimai/cover/${(id ?? 0) + 10000}.png`,
        `/assets/maimai/cover/${(id ?? 0) - 10000}.jpg`,
        `/assets/maimai/cover/${(id ?? 0) - 10000}.png`,
        `/assets/maimai/cover/0.png`,
    ]
}

export {
    host,
    maimai_newRa,
    maimai_levelColors,
    maimai_alternativeCover
};