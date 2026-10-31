// Renders one 1920x1080 PNG per narration segment from slides.html.
// usage: node render.mjs <frames_dir> <path to a package.json whose node_modules has @playwright/test>
import { createRequire } from 'node:module';
import { readFileSync, mkdirSync } from 'node:fs';
import { pathToFileURL } from 'node:url';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const out = resolve(process.argv[2]);
const require = createRequire(resolve(process.argv[3]));
const { chromium } = require('@playwright/test');

mkdirSync(out, { recursive: true });
const segs = JSON.parse(readFileSync(join(here, 'narration.json'), 'utf8'));
const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1920, height: 1080 }, deviceScaleFactor: 1 });
await page.goto(pathToFileURL(join(here, 'slides.html')).href);
for (const s of segs) {
  await page.evaluate(([a, b]) => window.show(a, b), [s.slide, s.step]);
  await page.waitForTimeout(150);
  await page.screenshot({ path: join(out, `${s.id}.png`) });
  console.log('frame', s.id);
}
await browser.close();
