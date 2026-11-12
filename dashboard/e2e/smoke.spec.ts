import { expect, test, type Page } from '@playwright/test'

// Fails a test on any uncaught exception or console error the page logs.
let errors: string[] = []
test.beforeEach(({ page }) => {
  errors = []
  page.on('pageerror', (e) => errors.push(e.message))
  page.on('console', (m) => {
    if (m.type() === 'error') errors.push(m.text())
  })
})
test.afterEach(() => expect(errors).toEqual([]))

const rows = (page: Page) => page.locator('tbody tr')

test('leaderboard ranks firms and opens a firm profile', async ({ page }) => {
  await page.goto('/')
  await expect(page.getByRole('heading', { name: 'Leaderboard' })).toBeVisible()
  await expect(rows(page).first()).toBeVisible()
  await expect(page.locator('.sparkline svg').first()).toBeVisible()

  await rows(page).first().getByRole('link').first().click()
  await expect(page).toHaveURL(/\/firms\/\d+$/)
  await expect(page.getByRole('button', { name: /12-month horizon/ })).toHaveAttribute('aria-pressed', 'true')
  await expect(page.getByRole('heading', { name: 'Scored calls' })).toBeVisible()
  await expect(page.locator('.recharts-scatter-symbol').first()).toBeVisible()
})

test('analyst leaderboard opens an analyst profile', async ({ page }) => {
  await page.goto('/?view=analysts')
  await expect(rows(page).first()).toBeVisible()

  await rows(page).first().locator('.name').click()
  await expect(page).toHaveURL(/\/analysts\/\d+$/)
  await expect(page.getByText(/^Analyst at/)).toBeVisible()

  await page.getByRole('button', { name: /3-month horizon/ }).click()
  await expect(page).toHaveURL(/horizon=3/)
})

test('stock page draws the price chart, consensus and calls', async ({ page }) => {
  await page.goto('/stocks/AAPL')
  await expect(page.getByRole('heading', { name: /^AAPL/ })).toBeVisible()
  await expect(page.locator('.price-chart canvas').first()).toBeVisible()
  await expect(page.getByRole('heading', { name: 'Consensus today' })).toBeVisible()
  await expect(page.locator('.split')).toBeVisible()
  await expect(page.locator('.recharts-bar-rectangle').first()).toBeVisible()
  await expect(rows(page).first()).toBeVisible()

  await page.getByRole('button', { name: '3Y' }).click()
  await expect(page).toHaveURL(/range=36/)
})

test('stocks list searches and links to a stock', async ({ page }) => {
  await page.goto('/stocks')
  await expect(rows(page).first()).toBeVisible()

  await page.getByRole('searchbox').fill('MSFT')
  await expect(page).toHaveURL(/q=MSFT/)
  await expect(rows(page)).toHaveCount(1)
  await rows(page).getByRole('link', { name: 'MSFT' }).click()
  await expect(page).toHaveURL(/\/stocks\/MSFT$/)
})

test('feed lists rating changes and filters by action', async ({ page }) => {
  await page.goto('/feed')
  await expect(page.getByRole('heading', { name: 'Live feed' })).toBeVisible()
  await expect(rows(page).first()).toBeVisible()

  await page.getByRole('button', { name: 'Downgrades' }).click()
  await expect(page).toHaveURL(/show=Downgrade/)
  await expect(rows(page).first().locator('.action')).toHaveText(/Downgrade/)
})

test('unknown addresses show a not-found page', async ({ page }) => {
  await page.goto('/no/such/page')
  await expect(page.getByText('Page not found')).toBeVisible()
})
