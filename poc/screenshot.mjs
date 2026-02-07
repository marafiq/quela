import { fileURLToPath } from 'url';
import path from 'path';
import { createRequire } from 'module';

const require = createRequire(import.meta.url);
const pw = require('/opt/node22/lib/node_modules/playwright');
const { chromium } = pw;

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const browser = await chromium.launch({
    args: ['--no-sandbox', '--disable-gpu', '--disable-setuid-sandbox', '--disable-dev-shm-usage', '--single-process']
});
const page = await browser.newPage({ viewport: { width: 700, height: 900 } });
await page.goto('http://localhost:8765/island-conductor-standalone.html', { waitUntil: 'load', timeout: 10000 });
await page.waitForTimeout(500);

// ── Shot 1: Initial state ──
await page.screenshot({ path: path.join(__dirname, 'shot-1-initial.png'), fullPage: true });
console.log('Shot 1: Initial state');

// ── Interact: Select Facility "Oak Park" (F2) → triggers cascade ──
await page.selectOption('#ctl_FacilityId', 'F2');
await page.waitForTimeout(600); // wait for simulated async fetch

// ── Interact: Select PayerType "Medicaid" → shows MedicaidSection region ──
await page.selectOption('#ctl_PayerType', 'Medicaid');
await page.waitForTimeout(300);

// ── Select Unit 202 ──
await page.selectOption('#ctl_UnitId', 'U202');
await page.waitForTimeout(200);

// ── Shot 2: Populated state ──
await page.screenshot({ path: path.join(__dirname, 'shot-2-populated.png'), fullPage: true });
console.log('Shot 2: Populated (Facility=Oak Park, Unit=202, PayerType=Medicaid, MedicaidSection visible)');

// ── Click Validate (Medicaid # empty → should error) ──
await page.click('#btnValidate');
await page.waitForTimeout(200);

// ── Click Collect Payload ──
await page.click('#btnCollect');
await page.waitForTimeout(200);

// ── Shot 3: Validation errors + payload ──
await page.screenshot({ path: path.join(__dirname, 'shot-3-validate-collect.png'), fullPage: true });
console.log('Shot 3: After Validate + Collect');

// ── Fill Medicaid # and re-validate ──
await page.fill('#ctl_MedicaidNumber', 'MC-98765');
await page.waitForTimeout(100);
await page.click('#btnValidate');
await page.waitForTimeout(100);
await page.click('#btnCollect');
await page.waitForTimeout(200);

// ── Shot 4: All valid ──
await page.screenshot({ path: path.join(__dirname, 'shot-4-all-valid.png'), fullPage: true });
console.log('Shot 4: All fields valid + final payload');

await browser.close();
console.log('Done!');
