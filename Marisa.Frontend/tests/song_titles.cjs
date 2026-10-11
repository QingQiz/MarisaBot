const assert = require('node:assert/strict')
const fs = require('node:fs')
const path = require('node:path')
const test = require('node:test')
const ts = require('typescript')

// Exercise the actual TypeScript helper without writing build output or adding a test dependency.
function loadTypeScript(file) {
    const output = ts.transpileModule(fs.readFileSync(file, 'utf8'), {
        fileName: file,
        compilerOptions: {target: ts.ScriptTarget.ES2019, module: ts.ModuleKind.CommonJS}
    }).outputText
    const module = {exports: {}}
    const localRequire = name => name.startsWith('.')
        ? loadTypeScript(path.resolve(path.dirname(file), `${name}.ts`))
        : require(name)
    new Function('exports', 'require', 'module', output)(module.exports, localRequire, module)
    return module.exports
}

const {titlesForSong, isTitleAchieved} = loadTypeScript(
    path.resolve(__dirname, '../src/components/maimai/utils/song_titles.ts'))

function title(check, overrides = {}) {
    return {id: 1, name: '称号', rare: 'Gold', text: '条件', norm: '', diffIdx: -1, check, ...overrides}
}
function chart(index, overrides = {}) {
    return {
        LevelIndex: index, Played: true, Achievement: 100, Fc: 'ap', Fs: '', MaxDx: 3000, DxScore: 2950,
        ...overrides
    }
}

test('ALL uses BASIC–MASTER and does not require an existing Re:MASTER', () => {
    const requirement = title({dim: 'fc', lv: 3, scope: 'all', difficultyIndices: [0, 1, 2, 3]})
    const scores = [0, 1, 2, 3].map(index => chart(index))
    assert.equal(isTitleAchieved(requirement, scores), true)
    assert.equal(isTitleAchieved(requirement, [...scores, chart(4, {Played: false})]), true)
    assert.equal(isTitleAchieved(requirement, scores.slice(1)), false)
    assert.equal(isTitleAchieved(requirement, scores.map(c => c.LevelIndex === 2 ? {...c, Fc: 'fc'} : c)), false)
})

test('ALL+Re:MASTER requires every named difficulty, even when the song has only four charts', () => {
    const requirement = title({dim: 'fc', lv: 3, scope: 'all', difficultyIndices: [0, 1, 2, 3, 4]})
    const scores = [0, 1, 2, 3].map(index => chart(index))
    assert.equal(isTitleAchieved(requirement, scores), false)
    assert.equal(isTitleAchieved(requirement, [...scores, chart(4)]), true)
})

test('fixed difficulty and ANY do not substitute a different chart or treat an empty pool as success', () => {
    const fixed = title({dim: 'fc', lv: 3, scope: 'one', difficultyIndices: [3]}, {diffIdx: 3})
    const any = title({dim: 'fc', lv: 3, scope: 'one'})
    assert.equal(isTitleAchieved(fixed, [chart(2)]), false)
    assert.equal(isTitleAchieved(fixed, [chart(2), chart(3, {Played: false})]), false)
    assert.equal(isTitleAchieved(any, [chart(2)]), true)
    assert.equal(isTitleAchieved(any, []), false)
})

test('special, group, Credit, ambiguous, and malformed conditions remain unknown', () => {
    const scores = [0, 1, 2, 3, 4].map(index => chart(index))
    for (const requirement of [
        title(null), title({dim: 'sonic', lv: 1, scope: 'one'}),
        title({dim: 'play', lv: 1, scope: 'credit'}), title({dim: 'play', lv: 1, scope: 'all'}),
        title({dim: 'play', lv: 1, scope: 'all', difficultyIndices: []}),
        title({dim: 'play', lv: 1, scope: 'one', difficultyIndices: [5]}),
        title({dim: 'play', lv: Number.NaN, scope: 'one'})
    ]) assert.equal(isTitleAchieved(requirement, scores), null)
})

test('DX stars with missing note totals remain unknown; known failures and successes still resolve', () => {
    const any = title({dim: 'dx', lv: 5, scope: 'one'})
    const all = title({dim: 'dx', lv: 5, scope: 'all', difficultyIndices: [0, 1]})
    const unknown = chart(0, {MaxDx: 0})
    assert.equal(isTitleAchieved(any, [unknown]), null)
    assert.equal(isTitleAchieved(any, [unknown, chart(1)]), true)
    assert.equal(isTitleAchieved(all, [unknown, chart(1)]), null)
    assert.equal(isTitleAchieved(all, [unknown, chart(1, {DxScore: 0})]), false)
    assert.equal(isTitleAchieved(any, [chart(0, {Played: false, MaxDx: 0})]), false)
})

test('missing provider values cannot establish a played chart condition', () => {
    for (const [dim, field] of [['rank', 'Achievement'], ['fc', 'Fc'], ['fs', 'Fs'], ['dx', 'DxScore']]) {
        assert.equal(isTitleAchieved(title({dim, lv: 1, scope: 'one'}), [chart(3, {[field]: null})]), null)
        assert.equal(isTitleAchieved(title({dim, lv: 1, scope: 'one'}), [chart(3, {[field]: undefined})]), null)
    }
    assert.equal(isTitleAchieved(title({dim: 'fc', lv: 1, scope: 'one'}), [chart(3, {Fc: ''})]), false)
})

test('reserve entries become queryable by the catalog-selected full ID without an asset promotion', () => {
    const pendingTitle = title(null, {id: 558118})
    const pending = {'11818': [pendingTitle]}
    assert.deepEqual(titlesForSong(11818, {}, pending), [pendingTitle])
    assert.deepEqual(titlesForSong(1818, {}, pending), [])
    assert.deepEqual(titlesForSong(0, {}, pending), [])
    assert.deepEqual(titlesForSong(11818, null, pending), [pendingTitle])
})

test('active entries win duplicates while reserve titles and rarity order remain available', () => {
    const active = title(null, {id: 2, name: 'current'})
    const duplicate = title(null, {id: 2, name: 'reserve copy'})
    const extra = title(null, {id: 3, rare: 'Rainbow'})
    const sd = title(null, {id: 4})
    assert.deepEqual(titlesForSong(11818, {'11818': [active], '1818': [sd]}, {'11818': [duplicate, extra]}),
        [extra, active])
    assert.deepEqual(titlesForSong(1818, {'11818': [active], '1818': [sd]}, {'11818': [duplicate]}), [sd])
})

const activeTitles = JSON.parse(fs.readFileSync(path.resolve(__dirname, '../public/assets/maimai/song_titles.json'), 'utf8'))
const reserveTitles = JSON.parse(fs.readFileSync(path.resolve(__dirname, '../public/assets/maimai/song_titles_pending.json'), 'utf8'))

test('shipped title assets preserve the known positive and absent-ID negative examples', () => {
    assert.deepEqual(titlesForSong(10030, activeTitles, reserveTitles).map(t => t.id), [558101, 558529])
    assert.deepEqual(titlesForSong(0, activeTitles, reserveTitles), [])
})

test('BLACKBOX includes both verified titles, while its two-song unlock cannot be inferred from one song', () => {
    const blackbox = titlesForSong(11818, activeTitles, reserveTitles)
    assert.deepEqual(blackbox.map(t => t.id), [558118, 558566])
    const ap = [0, 1, 2, 3].map(index => chart(index))
    assert.equal(isTitleAchieved(blackbox[0], ap), true)
    assert.equal(isTitleAchieved(blackbox[0], ap.slice(1)), false)
    assert.equal(isTitleAchieved(blackbox[1], ap), null)
    const lonelinessGroup = titlesForSong(11394, activeTitles, reserveTitles).find(t => t.id === 558566)
    assert.ok(lonelinessGroup)
    assert.equal(isTitleAchieved(lonelinessGroup, ap), null)
})

test('actual ALL+Re:MASTER unlocks avoid false positives and ordinary ALL ignores an unplayed Re:MASTER', () => {
    const fourApp = [0, 1, 2, 3].map(index => chart(index, {Fc: 'app'}))
    for (const [songId, titleId] of [[834, 458583], [11663, 458582], [11820, 558567]]) {
        const requirement = titlesForSong(songId, activeTitles, reserveTitles).find(t => t.id === titleId)
        assert.ok(requirement)
        assert.equal(requirement.text, 'BASIC～Re:MASTER · ALL PERFECT+')
        assert.deepEqual(requirement.check.difficultyIndices, [0, 1, 2, 3, 4])
        assert.equal(isTitleAchieved(requirement, fourApp), false)
        assert.equal(isTitleAchieved(requirement, [...fourApp, chart(4, {Fc: 'app'})]), true)
        const onlyReMaster = [0, 1, 2, 3].map(index => chart(index, {Fc: ''}))
        assert.equal(isTitleAchieved(requirement, [...onlyReMaster, chart(4, {Fc: 'app'})]), false)
        const fiveAp = [0, 1, 2, 3, 4].map(index => chart(index, {Fc: 'ap'}))
        assert.equal(isTitleAchieved(requirement, fiveAp), false)
        const missingBasic = [1, 2, 3, 4].map(index => chart(index, {Fc: 'app'}))
        assert.equal(isTitleAchieved(requirement, missingBasic), false)
    }
    const secretSleuth = titlesForSong(11002, activeTitles, reserveTitles).find(t => t.id === 5124)
    assert.ok(secretSleuth)
    assert.equal(isTitleAchieved(secretSleuth, [...fourApp, chart(4, {Played: false})]), true)
})

test('prepared CiRCLE titles work as soon as a catalog can select ID 11907 and never fall back to 1907', () => {
    const prepared = reserveTitles['11907'] ?? activeTitles['11907']
    assert.ok(Array.isArray(prepared) && prepared.length)
    const isolatedReserve = {'11907': prepared}
    assert.deepEqual(titlesForSong(11907, {}, isolatedReserve).map(t => t.id),
        titlesForSong(11907, activeTitles, reserveTitles).map(t => t.id))
    assert.deepEqual(titlesForSong(1907, {}, isolatedReserve), [])
    const scores = [0, 1, 2, 3].map(index => chart(index))
    for (const titleId of [608509, 608512, 608513, 608516]) {
        const requirement = prepared.find(t => t.id === titleId)
        assert.ok(requirement)
        assert.equal(isTitleAchieved(requirement, scores), null)
    }
})
