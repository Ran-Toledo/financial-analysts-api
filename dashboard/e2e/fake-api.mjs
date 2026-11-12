// Runs the API for the smoke tests: fake provider, its own SQLite file, port 5077 (so it can sit beside a dev API).
// The first run creates and fills the database (about a minute); later runs reuse it. Delete e2e/.data to rebuild.
import { spawn, spawnSync } from 'node:child_process'
import { existsSync, mkdirSync, renameSync, rmSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const project = resolve(here, '../../src/FinancialAnalystsAPI')
const dataDir = resolve(here, '.data')
const db = resolve(dataDir, 'fake.db')
const port = process.env.E2E_API_PORT ?? '5077'

const settings = (file) => [
  `--ConnectionStrings:AnalystsDb=Data Source=${file}`,
  '--DataProvider:Name=Fake',
  '--Ingestion:Enabled=false',
  '--ApiLimits:AnonymousPerMinute=100000',
]

function dotnet(args) {
  const result = spawnSync('dotnet', args, { stdio: 'inherit' })
  if (result.status !== 0) process.exit(result.status ?? 1)
}

if (!existsSync(db)) {
  // Build into a temporary file so an interrupted run never leaves a half-filled database behind.
  mkdirSync(dataDir, { recursive: true })
  const tmp = `${db}.tmp`
  rmSync(tmp, { force: true })
  dotnet(['ef', 'database', 'update', '--project', project, '--connection', `Data Source=${tmp}`])
  dotnet(['run', '--project', project, '--no-launch-profile', '--', 'ingest', ...settings(tmp)])
  renameSync(tmp, db)
}

const api = spawn(
  'dotnet',
  ['run', '--project', project, '--no-launch-profile', '--', `--urls=http://localhost:${port}`, ...settings(db)],
  { stdio: 'inherit' },
)
for (const signal of ['SIGINT', 'SIGTERM']) process.on(signal, () => api.kill(signal))
api.on('exit', (code) => process.exit(code ?? 0))
