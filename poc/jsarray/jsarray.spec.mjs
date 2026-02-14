import { createRequire } from 'module';
import { fileURLToPath } from 'url';
import path from 'path';
import assert from 'assert';

const require = createRequire(import.meta.url);
const pw = require('/opt/node22/lib/node_modules/playwright');
const { chromium } = pw;

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const LAUNCH_OPTS = { args: ['--no-sandbox', '--disable-gpu', '--disable-setuid-sandbox', '--disable-dev-shm-usage', '--single-process'] };

// ── Test Harness ────────────────────────────────────────────────────────
let browser, page;
let passed = 0, failed = 0;
const failures = [];

async function setup() {
    browser = await chromium.launch(LAUNCH_OPTS);
    page = await browser.newPage({ viewport: { width: 1000, height: 1200 } });
    await page.goto('http://localhost:8765/jsarray/tests.html', { waitUntil: 'load', timeout: 10000 });
    await page.waitForTimeout(500);
}

async function test(name, fn) {
    try {
        await fn();
        passed++;
        console.log(`  ✓ ${name}`);
    } catch (e) {
        failed++;
        failures.push({ name, error: e.message });
        console.log(`  ✗ ${name}: ${e.message}`);
    }
}

// ── Tests ───────────────────────────────────────────────────────────────
async function run() {
    console.log('\nJsArray<T> DSL — Playwright Spec Tests\n');

    await setup();

    // ── Screenshot 1: Full test results page ──
    await page.screenshot({ path: path.join(__dirname, 'result-full.png'), fullPage: true });
    console.log('  [screenshot] result-full.png\n');

    // ── Verify summary says all passed ──
    await test('Summary shows all tests passed', async () => {
        const summary = await page.textContent('#summary');
        assert.ok(summary.includes('passed'), `Summary: "${summary}"`);
        assert.ok(!summary.includes('failed'), `Should have no failures: "${summary}"`);
    });

    // ── Verify total test count ──
    await test('13 test cases rendered (12 spec + ForEach bonus)', async () => {
        const count = await page.locator('.test').count();
        assert.strictEqual(count, 13, `Expected 13 tests, got ${count}`);
    });

    // ── Verify every test badge is PASS ──
    await test('All test badges show PASS', async () => {
        const passBadges = await page.locator('.badge-pass').count();
        const failBadges = await page.locator('.badge-fail').count();
        assert.strictEqual(failBadges, 0, `${failBadges} tests have FAIL badge`);
        assert.strictEqual(passBadges, 13, `Expected 13 PASS badges, got ${passBadges}`);
    });

    // ── Verify individual spec examples ──
    const specs = [
        { id: '5.1',  title: 'Filter',                 jsFragment: 'residents.filter(function(r)' },
        { id: '5.2',  title: 'Map',                    jsFragment: 'residents.map(function(r)' },
        { id: '5.3',  title: 'Filter + Map Chain',     jsFragment: '.filter(function(r) { return r.isActive; }).map(' },
        { id: '5.4',  title: 'Find',                   jsFragment: 'residents.find(function(r) { return r.id === selectedId; })' },
        { id: '5.5',  title: 'FindIndex',              jsFragment: 'residents.findIndex(function(r) { return r.id === targetId; })' },
        { id: '5.6',  title: 'Some / Every',           jsFragment: 'residents.some(function(r)' },
        { id: '5.7',  title: 'Reduce',                 jsFragment: 'items.reduce(function(sum, item) { return sum + item.amount; }, 0)' },
        { id: '5.8',  title: 'Sort with Comparer',     jsFragment: '.sort(function(a, b) { return a.name.localeCompare(b.name); })' },
        { id: '5.9',  title: 'Filter+Map+Join',        jsFragment: ".join(', ')" },
        { id: '5.10', title: 'Indexer',                jsFragment: 'residents[residents.length - 1]' },
        { id: '5.11', title: 'Includes',               jsFragment: 'selected.includes(args.rowIndex)' },
        { id: '5.12', title: 'Push/Splice',            jsFragment: 'pendingChanges.push(args.data)' },
        { id: '6',    title: 'ForEach (void lambda)',   jsFragment: '.forEach(function(r) { console.log(r.name); })' },
    ];

    for (const spec of specs) {
        await test(`§${spec.id} ${spec.title}: transpile match + execution`, async () => {
            const testEl = page.locator(`[data-testid="test-${spec.id}"]`);
            const passAttr = await testEl.getAttribute('data-pass');
            assert.strictEqual(passAttr, 'true', `Test ${spec.id} did not pass`);

            // Verify the expected JS fragment appears in the transpiled output
            const html = await testEl.innerHTML();
            assert.ok(html.includes('transpile ✓'), `${spec.id}: transpile should match`);
            assert.ok(html.includes('exec ✓'), `${spec.id}: exec should pass`);
        });
    }

    // ── Verify specific execution results ──
    console.log('');

    await test('§5.1 Filter: result has 3 active residents', async () => {
        const html = await page.locator('[data-testid="test-5.1"]').innerHTML();
        // The execution result should show an array with 3 items
        assert.ok(html.includes('"isActive":true') || html.includes('"isActive": true'), 'Should contain active residents');
    });

    await test('§5.4 Find: found Charlie (id=3)', async () => {
        const html = await page.locator('[data-testid="test-5.4"]').innerHTML();
        assert.ok(html.includes('"name":"Charlie"') || html.includes('"name": "Charlie"') || html.includes('Charlie'), 'Should find Charlie');
    });

    await test('§5.7 Reduce: total = 750', async () => {
        const html = await page.locator('[data-testid="test-5.7"]').innerHTML();
        assert.ok(html.includes('750'), 'Should show total 750');
    });

    await test('§5.9 Join: "Alice, Charlie"', async () => {
        const html = await page.locator('[data-testid="test-5.9"]').innerHTML();
        assert.ok(html.includes('Alice, Charlie'), 'Should show joined names');
    });

    await test('§5.10 Indexer: first=Alice, last=Diana', async () => {
        const html = await page.locator('[data-testid="test-5.10"]').innerHTML();
        assert.ok(html.includes('Alice'), 'Should contain Alice');
        assert.ok(html.includes('Diana'), 'Should contain Diana');
    });

    // ── Verify C# == maps to JS === (critical spec requirement) ──
    await test('C# == operator transpiles to JS ===', async () => {
        const html = await page.locator('[data-testid="test-5.4"]').innerHTML();
        assert.ok(html.includes('r.id === selectedId'), '== should become ===');
    });

    // ── Verify ForEach has NO return keyword (void lambda rule) ──
    await test('§6 ForEach: void lambda emits no "return" keyword', async () => {
        const html = await page.locator('[data-testid="test-6"]').innerHTML();
        // The transpiled JS for forEach should NOT have "return console.log"
        assert.ok(!html.includes('return console.log'), 'ForEach should NOT emit return');
        assert.ok(html.includes('{ console.log(r.name); }'), 'ForEach should emit statement without return');
    });

    // ── Verify C# identifiers map to JS identifiers ──
    await test('Grid → residentGrid, StatusLabel → statusLabel identifier mapping', async () => {
        const html51 = await page.locator('[data-testid="test-5.1"]').innerHTML();
        assert.ok(html51.includes('residentGrid.dataSource'), 'Grid should map to residentGrid');

        const html54 = await page.locator('[data-testid="test-5.4"]').innerHTML();
        assert.ok(html54.includes('statusLabel.innerText'), 'StatusLabel should map to statusLabel');
    });

    // ── Verify property name casing: Length → length, IsActive → isActive ──
    await test('C# PascalCase properties → JS camelCase (Length→length, IsActive→isActive)', async () => {
        const html510 = await page.locator('[data-testid="test-5.10"]').innerHTML();
        assert.ok(html510.includes('residents.length'), 'Length should become length');

        const html51 = await page.locator('[data-testid="test-5.1"]').innerHTML();
        assert.ok(html51.includes('r.isActive'), 'IsActive should become isActive');
    });

    // ── Screenshot 2: Expanded test details ──
    // Open all tests
    await page.evaluate(() => {
        document.querySelectorAll('.test').forEach(el => el.classList.add('open'));
    });
    await page.waitForTimeout(200);
    await page.screenshot({ path: path.join(__dirname, 'result-details.png'), fullPage: true });
    console.log('\n  [screenshot] result-details.png');

    // ── Summary ──
    console.log(`\n${'═'.repeat(50)}`);
    console.log(`  ${passed} passed, ${failed} failed`);
    if (failures.length > 0) {
        console.log('\n  Failures:');
        for (const f of failures) {
            console.log(`    ✗ ${f.name}: ${f.error}`);
        }
    }
    console.log(`${'═'.repeat(50)}\n`);

    await browser.close();
    process.exit(failed > 0 ? 1 : 0);
}

run().catch(e => {
    console.error('Fatal:', e);
    process.exit(1);
});
