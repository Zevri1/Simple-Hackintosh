import assert from "node:assert/strict";
import { createServer } from "node:http";
import { readFile, mkdtemp, mkdir, writeFile, rm } from "node:fs/promises";
import { extname, resolve, sep, join } from "node:path";
import { tmpdir } from "node:os";
import { chromium } from "playwright";

const root = resolve(process.env.SITE_ROOT || "_site");
const mime = {
  ".html": "text/html; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".md": "text/markdown; charset=utf-8",
  ".svg": "image/svg+xml",
  ".jpg": "image/jpeg",
};
const server = createServer(async (req, res) => {
  try {
    const url = new URL(req.url || "/", "http://127.0.0.1");
    const relative = decodeURIComponent(url.pathname === "/" ? "/index.html" : url.pathname);
    const path = resolve(root, "." + relative);
    if (path !== root && !path.startsWith(root + sep)) {
      res.writeHead(403).end("Forbidden");
      return;
    }
    const data = await readFile(path);
    res.writeHead(200, {"content-type": mime[extname(path)] || "application/octet-stream"});
    res.end(data);
  } catch {
    res.writeHead(404).end("Not found");
  }
});

const browserErrors = [];
const failedLocalRequests = [];
let browser;
let fixtureRoot;
await new Promise(resolveListen => server.listen(0, "127.0.0.1", resolveListen));
const address = server.address();
const base = `http://127.0.0.1:${address.port}`;

try {
  browser = await chromium.launch({headless: true});
  const context = await browser.newContext({viewport: {width: 1365, height: 900}});
  const page = await context.newPage();
  page.on("pageerror", error => browserErrors.push(error.message));
  page.on("requestfailed", request => {
    if (request.url().startsWith(base)) failedLocalRequests.push(request.url());
  });

  await page.goto(base + "/", {waitUntil: "networkidle"});
  await page.locator("#chapter-grid .chapter-card").first().waitFor();
  assert.ok(await page.locator("#chapter-grid .chapter-card").count() >= 10, "Home page should render chapter cards");

  await page.locator("#search-open").click();
  await page.locator("#global-search").fill("ACPI");
  await page.locator(".global-result").first().waitFor();
  await page.locator("#global-search").press("Enter");
  await page.locator("#doc-article h1").waitFor();
  assert.equal(await page.locator("#docs-view").isVisible(), true, "Search should open a chapter");

  await page.goto(base + "/tools.html", {waitUntil: "networkidle"});
  const initialTheme = await page.locator("html").getAttribute("data-theme");
  await page.locator("#tools-theme-toggle").click();
  const changedTheme = await page.locator("html").getAttribute("data-theme");
  assert.notEqual(changedTheme, initialTheme, "Tools theme toggle should change the theme");
  assert.equal(await page.evaluate(() => localStorage.getItem("simple-hackintosh-theme")), changedTheme, "Theme preference should persist");

  const efiTab = page.locator("#tool-tab-efi");
  await efiTab.focus();
  await efiTab.press("ArrowRight");
  assert.equal(await page.evaluate(() => document.activeElement.id), "tool-tab-config", "ArrowRight should move to the next tab");
  assert.equal(await page.locator("#tool-config").isVisible(), true, "Keyboard tab navigation should reveal the matching panel");
  await page.locator("#tool-tab-config").press("End");
  assert.equal(await page.evaluate(() => document.activeElement.id), "tool-tab-hardware", "End should focus the last tab");

  const plist = `<?xml version="1.0" encoding="UTF-8"?>
<plist version="1.0"><dict>
  <key>ACPI</key><dict><key>Add</key><array/></dict>
  <key>Booter</key><dict><key>Quirks</key><dict/></dict>
  <key>DeviceProperties</key><dict><key>Add</key><dict/></dict>
  <key>Kernel</key><dict><key>Add</key><array/><key>Quirks</key><dict/></dict>
  <key>Misc</key><dict><key>Boot</key><dict/><key>Security</key><dict/></dict>
  <key>NVRAM</key><dict><key>Add</key><dict/></dict>
  <key>PlatformInfo</key><dict><key>Generic</key><dict/></dict>
  <key>UEFI</key><dict><key>Drivers</key><array/><key>Output</key><dict/><key>Quirks</key><dict/></dict>
</dict></plist>`;
  await page.locator("#tool-tab-config").click();
  await page.locator("#config-text").fill(plist);
  await page.locator("#config-run").click();
  assert.equal(await page.locator("#config-results").isVisible(), true, "Config validation should render results");
  assert.match(await page.locator("#config-results").innerText(), /Configuration review/);

  await page.locator("#tool-tab-hardware").click();
  await page.locator("#hw-cpu").fill("Test CPU");
  await page.locator("#hw-raw").fill("PCI VEN_8086&DEV_9B41");
  await page.locator("#hardware-run").click();
  assert.match(await page.locator("#hardware-results").innerText(), /VEN_8086&DEV_9B41/);
  const [download] = await Promise.all([
    page.waitForEvent("download"),
    page.getByRole("button", {name: "Download Markdown report"}).click(),
  ]);
  assert.equal(download.suggestedFilename(), "simple-hackintosh-hardware-report.md");

  fixtureRoot = await mkdtemp(join(tmpdir(), "simple-hackintosh-efi-"));
  await mkdir(join(fixtureRoot, "EFI", "OC", "Drivers"), {recursive: true});
  await mkdir(join(fixtureRoot, "EFI", "BOOT"), {recursive: true});
  await writeFile(join(fixtureRoot, "EFI", "OC", "config.plist"), plist, "utf8");
  await writeFile(join(fixtureRoot, "EFI", "OC", "OpenCore.efi"), "test");
  await writeFile(join(fixtureRoot, "EFI", "OC", "Drivers", "OpenRuntime.efi"), "test");
  await writeFile(join(fixtureRoot, "EFI", "BOOT", "BOOTx64.efi"), "test");
  await page.locator("#tool-tab-efi").click();
  await page.locator("#efi-folder").setInputFiles(join(fixtureRoot, "EFI"));
  await page.locator("#efi-run").click();
  assert.equal(await page.locator("#efi-results").isVisible(), true, "Folder-based EFI inspection should render results");
  assert.match(await page.locator("#efi-results").innerText(), /OpenCore\.efi/);

  const smallPage = await context.newPage({viewport: {width: 390, height: 844}});
  await smallPage.goto(base + "/tools.html", {waitUntil: "networkidle"});
  const bodyWidth = await smallPage.locator("body").evaluate(node => node.scrollWidth);
  const viewportWidth = await smallPage.evaluate(() => window.innerWidth);
  assert.ok(bodyWidth <= viewportWidth + 1, "Mobile Tools page should not overflow horizontally");

  assert.deepEqual(browserErrors, [], "Browser pages should not raise uncaught JavaScript errors");
  assert.deepEqual(failedLocalRequests, [], "All local site assets and chapter requests should load");
  console.log("Browser smoke passed: home, chapter search, shared theme, keyboard tabs, config validator, hardware report download, EFI folder inspection, and 390px mobile layout.");
} finally {
  if (browser) await browser.close();
  if (fixtureRoot) await rm(fixtureRoot, {recursive: true, force: true});
  await new Promise(resolveClose => server.close(resolveClose));
}
