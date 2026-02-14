import { fileURLToPath } from 'url';
import path from 'path';
import { createRequire } from 'module';
import assert from 'assert';

const require = createRequire(import.meta.url);
const pw = require('/opt/node22/lib/node_modules/playwright');
const { chromium } = pw;

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const LAUNCH = { args: ['--no-sandbox', '--disable-gpu', '--disable-setuid-sandbox', '--disable-dev-shm-usage', '--single-process'] };

let browser, page;
let passed = 0, failed = 0;
const failures = [];

async function test(name, fn) {
    try {
        await fn();
        passed++;
        console.log(`  \u2713 ${name}`);
    } catch (e) {
        failed++;
        failures.push({ name, error: e.message });
        console.log(`  \u2717 ${name}: ${e.message}`);
    }
}

async function run() {
    browser = await chromium.launch(LAUNCH);

    // ═══════════════════════════════════════════════════════════════════
    // PART 1: Browser Execution Tests (transpiled JS executed in browser)
    // ═══════════════════════════════════════════════════════════════════
    console.log('\n\u2550\u2550 JsArray<T> DSL \u2014 Browser Execution Tests \u2550\u2550\n');

    page = await browser.newPage({ viewport: { width: 1000, height: 1200 } });
    await page.goto('http://localhost:8765/jsarray/browser-tests.html', { waitUntil: 'load', timeout: 10000 });
    await page.waitForTimeout(500);

    await page.screenshot({ path: path.join(__dirname, 'browser-exec-full.png'), fullPage: true });
    console.log('  [screenshot] browser-exec-full.png\n');

    await test('Summary shows all tests passed', async () => {
        const summary = await page.textContent('#summary');
        assert.ok(summary.includes('passed'), `Summary: "${summary}"`);
        assert.ok(!summary.includes('failed'), `Should have no failures: "${summary}"`);
    });

    await test('15 test cases from C# manifest', async () => {
        const count = await page.locator('.test').count();
        assert.strictEqual(count, 15, `Expected 15 tests, got ${count}`);
    });

    await test('All browser test badges show PASS', async () => {
        const failBadges = await page.locator('.badge-fail').count();
        assert.strictEqual(failBadges, 0, `${failBadges} tests have FAIL badge`);
    });

    // Verify specific execution results in the browser
    const specChecks = [
        { id: '5.1', check: 'isActive' },
        { id: '5.2', check: 'Alice' },
        { id: '5.3', check: 'Charlie' },
        { id: '5.4', check: 'Charlie' },
        { id: '5.5', check: '1' },
        { id: '5.6a', check: 'true' },
        { id: '5.6b', check: 'false' },
        { id: '5.7', check: '750' },
        { id: '5.8', check: 'Alice' },
        { id: '5.9', check: 'Alice, Charlie' },
        { id: '5.10a', check: 'Alice' },
        { id: '5.10b', check: 'Diana' },
        { id: '5.11', check: 'true' },
        { id: '5.12', check: '1' },
        { id: '6', check: 'void' },
    ];

    for (const s of specChecks) {
        await test(`\u00A7${s.id}: browser execution verified`, async () => {
            const el = page.locator(`[data-testid="test-${s.id}"]`);
            const passAttr = await el.getAttribute('data-pass');
            assert.strictEqual(passAttr, 'true', `Test ${s.id} did not pass in browser`);
            const html = await el.innerHTML();
            assert.ok(html.includes(s.check), `Expected "${s.check}" in result`);
        });
    }

    // ═══════════════════════════════════════════════════════════════════
    // PART 2: Verify key spec rules in transpiled output
    // ═══════════════════════════════════════════════════════════════════
    console.log('\n\u2550\u2550 Spec Rule Verification \u2550\u2550\n');

    await test('C# == becomes JS === (not ==)', async () => {
        const html = await page.locator('[data-testid="test-5.4"]').innerHTML();
        assert.ok(html.includes('==='), 'Should contain ===');
    });

    await test('PascalCase properties become camelCase (IsActive \u2192 isActive)', async () => {
        // Check only the JS column (second code-col), not the C# input column
        const jsCols = page.locator('[data-testid="test-5.1"] .code-col:nth-child(2) pre');
        const jsText = await jsCols.textContent();
        assert.ok(jsText.includes('r.isActive'), 'Should contain r.isActive');
        assert.ok(!jsText.includes('r.IsActive'), 'JS should NOT contain r.IsActive');
    });

    await test('Length property maps to .length', async () => {
        const html = await page.locator('[data-testid="test-5.10b"]').innerHTML();
        assert.ok(html.includes('.length'), 'Should contain .length');
    });

    await test('ForEach uses void lambda (no return keyword)', async () => {
        const html = await page.locator('[data-testid="test-6"]').innerHTML();
        const jsCol = html.split('Transpiled JS')[1] || '';
        assert.ok(!jsCol.includes('return console'), 'ForEach should NOT have return');
    });

    await test('JsConsole.Log maps to console.log', async () => {
        const html = await page.locator('[data-testid="test-6"]').innerHTML();
        assert.ok(html.includes('console.log'), 'Should contain console.log');
    });

    await test('Indexer uses bracket notation, not method call', async () => {
        const html = await page.locator('[data-testid="test-5.10a"]').innerHTML();
        assert.ok(html.includes('residents[0]'), 'Should use bracket notation');
    });

    await page.screenshot({ path: path.join(__dirname, 'browser-exec-full.png'), fullPage: true });

    await browser.close();

    // ═══════════════════════════════════════════════════════════════════
    // Summary
    // ═══════════════════════════════════════════════════════════════════
    console.log(`\n${'='.repeat(55)}`);
    console.log(`  TOTAL: ${passed} passed, ${failed} failed`);
    if (failures.length > 0) {
        console.log('\n  Failures:');
        for (const f of failures) console.log(`    \u2717 ${f.name}: ${f.error}`);
    }
    console.log(`${'='.repeat(55)}\n`);

    process.exit(failed > 0 ? 1 : 0);
}

run().catch(e => { console.error('Fatal:', e); process.exit(1); });
