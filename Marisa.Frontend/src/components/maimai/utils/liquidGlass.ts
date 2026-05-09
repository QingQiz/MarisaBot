// Liquid-glass displacement filter — based on https://github.com/nikdelvin/liquid-glass (MIT)
// Renders an SVG <feDisplacementMap> filter that warps the backdrop like a curved glass lens.
// Use via: backdrop-filter: url(getDisplacementFilter({...}))

type Options = {
    width: number
    height: number
    radius: number
    depth: number
    strength?: number
    chromaticAberration?: number
}

function makeDisplacementMap({width, height, radius, depth}: Omit<Options, 'strength' | 'chromaticAberration'>): string {
    const yEdge = Math.ceil((radius / height) * 15)
    const xEdge = Math.ceil((radius / width) * 15)
    const svg = `<svg height="${height}" width="${width}" viewBox="0 0 ${width} ${height}" xmlns="http://www.w3.org/2000/svg"><style>.mix{mix-blend-mode:screen}</style><defs><linearGradient id="Y" x1="0" x2="0" y1="${yEdge}%" y2="${100 - yEdge}%"><stop offset="0%" stop-color="#0F0"/><stop offset="100%" stop-color="#000"/></linearGradient><linearGradient id="X" x1="${xEdge}%" x2="${100 - xEdge}%" y1="0" y2="0"><stop offset="0%" stop-color="#F00"/><stop offset="100%" stop-color="#000"/></linearGradient></defs><rect x="0" y="0" height="${height}" width="${width}" fill="#808080"/><g filter="blur(2px)"><rect x="0" y="0" height="${height}" width="${width}" fill="#000080"/><rect x="0" y="0" height="${height}" width="${width}" fill="url(#Y)" class="mix"/><rect x="0" y="0" height="${height}" width="${width}" fill="url(#X)" class="mix"/><rect x="${depth}" y="${depth}" height="${height - 2 * depth}" width="${width - 2 * depth}" fill="#808080" rx="${radius}" ry="${radius}" filter="blur(${depth}px)"/></g></svg>`
    return 'data:image/svg+xml;utf8,' + encodeURIComponent(svg)
}

export function getDisplacementFilter(opts: Options): string {
    const {width, height, strength = 100, chromaticAberration = 0} = opts
    const map = makeDisplacementMap(opts)

    const svg = `<svg height="${height}" width="${width}" viewBox="0 0 ${width} ${height}" xmlns="http://www.w3.org/2000/svg"><defs><filter id="displace" color-interpolation-filters="sRGB"><feImage x="0" y="0" height="${height}" width="${width}" href="${map}" result="displacementMap"/><feDisplacementMap transform-origin="center" in="SourceGraphic" in2="displacementMap" scale="${strength + chromaticAberration * 2}" xChannelSelector="R" yChannelSelector="G"/><feColorMatrix type="matrix" values="1 0 0 0 0  0 0 0 0 0  0 0 0 0 0  0 0 0 1 0" result="displacedR"/><feDisplacementMap in="SourceGraphic" in2="displacementMap" scale="${strength + chromaticAberration}" xChannelSelector="R" yChannelSelector="G"/><feColorMatrix type="matrix" values="0 0 0 0 0  0 1 0 0 0  0 0 0 0 0  0 0 0 1 0" result="displacedG"/><feDisplacementMap in="SourceGraphic" in2="displacementMap" scale="${strength}" xChannelSelector="R" yChannelSelector="G"/><feColorMatrix type="matrix" values="0 0 0 0 0  0 0 0 0 0  0 0 1 0 0  0 0 0 1 0" result="displacedB"/><feBlend in="displacedR" in2="displacedG" mode="screen"/><feBlend in2="displacedB" mode="screen"/></filter></defs></svg>`
    return 'data:image/svg+xml;utf8,' + encodeURIComponent(svg) + '#displace'
}
