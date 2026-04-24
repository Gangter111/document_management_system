import * as esbuild from 'esbuild';
import path from 'path';
import fs from 'fs';

async function build() {
  const outdir = 'dist-electron';
  if (!fs.existsSync(outdir)) fs.mkdirSync(outdir);

  // Build Main Process
  await esbuild.build({
    entryPoints: ['electron-main.ts'],
    bundle: true,
    platform: 'node',
    outfile: path.join(outdir, 'electron-main.js'),
    external: ['electron', 'better-sqlite3', 'tesseract.js', 'pdf-parse', 'mammoth'],
    format: 'esm',
    target: 'node20',
  });

  // Build Preload Script
  await esbuild.build({
    entryPoints: ['src/main/preload.ts'],
    bundle: true,
    platform: 'node',
    outfile: path.join(outdir, 'preload.js'),
    external: ['electron'],
    format: 'cjs', // Preload usually works best as CJS or ESM depending on Electron version, but CJS is safer for contextBridge
    target: 'node20',
  });

  console.log('Electron build complete.');
}

build().catch(err => {
  console.error(err);
  process.exit(1);
});
