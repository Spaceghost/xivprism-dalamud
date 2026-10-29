// Generated from native Imba; edit tools/browser-check.imba.

/*body*/
import {chromium} from 'playwright';
import {spawn} from 'node:child_process';
import {mkdtempSync,readFileSync,writeFileSync,existsSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import assert from 'node:assert/strict';
const root = process.cwd();
const control = mkdtempSync(join(tmpdir(),'xivprism-browser-'));
const child = spawn(process.env.DOTNET || 'dotnet',['run','--project','tools/browser-fixture/Fixture.csproj','-c','Release','--','viewer',control],{stdio: ['ignore','pipe','pipe']});
let fixtureLog = '';
child.stdout.on('data',function(chunk) { return fixtureLog += chunk.toString(); });
child.stderr.on('data',function(chunk) { return fixtureLog += chunk.toString(); });
let browser = null;
try {
	for (let index = 0; index < 100; index++) {
		if (existsSync(join(control,'pair.json'))) {
			break;
		};
		if (child.exitCode != null) {
			throw new Error('Fixture failed: ' + fixtureLog);
		};
		await new Promise(function(resolve) { return setTimeout(resolve,200); });
	};
	assert.ok(existsSync(join(control,'pair.json')),'Fixture did not start');
	browser = await chromium.launch({headless: true,...(process.env.CHROMIUM_EXECUTABLE ? {executablePath: process.env.CHROMIUM_EXECUTABLE} : {}),args: ['--disable-gpu','--no-proxy-server']});
	const page = await browser.newPage({viewport: {width: 1440,height: 1000},acceptDownloads: true});
	const errors = [];
	page.on('pageerror',function(error) { return errors.push(error.message); });
	await page.goto('http://127.0.0.1:18787/',{waitUntil: 'domcontentloaded',timeout: 15000});
	await page.locator('#code').fill(JSON.parse(readFileSync(join(control,'pair.json'))).code);
	await page.locator('#pair button').click();
	await page.locator('#canvas').waitFor({state: 'visible',timeout: 10000});
	assert.match(await page.locator('[data-panel="player"]').innerText(),new RegExp('TEST PLAYER'));
	await page.locator('#recommendation').click();
	assert.equal(await page.locator('#profile').inputValue(),'Desk');
	page.once('dialog',function(dialog) { return dialog.accept('Test layout'); });
	await page.locator('#save').click();
	assert.equal(await page.locator('#profile').inputValue(),'Test layout');
	const download = page.waitForEvent('download');
	await page.locator('#export').click();
	const file = await download;
	await file.saveAs(join(control,'layout.json'));
	const layout = JSON.parse(readFileSync(join(control,'layout.json')));
	assert.equal(layout.version,1);
	assert.ok(layout.profiles['Test layout']);
	assert.ok(!(JSON.stringify(layout).includes('TEST PLAYER')));
	await page.setViewportSize({width: 390,height: 844});
	assert.equal(await page.evaluate(function() { return globalThis.document.documentElement.scrollWidth > globalThis.innerWidth; }),false);
	writeFileSync(join(control,'freeze'),'1');
	await page.locator('#canvas').waitFor({state: 'hidden',timeout: 8000});
	assert.ok(!((await page.locator('[data-panel="player"]').innerText()).includes('TEST PLAYER')));
	writeFileSync(join(control,'revoke'),'1');
	await page.locator('#pairing').waitFor({state: 'visible',timeout: 8000});
	assert.deepEqual(errors,[]);
	console.log('Viewer pairing, rendering, profile export, mobile layout, stale hiding and revocation passed with fixture data.');
} finally {
	if (browser) {
		await browser.close();
	};
	writeFileSync(join(control,'stop'),'1');
	setTimeout(function() { return child.kill(); },3000).unref();
};
