import { Suspense } from 'react'
import { NavLink, Outlet, useLocation } from 'react-router'

export function Layout() {
  // Profiles are reached from the leaderboard, so it stays highlighted there.
  const onProfile = /^\/(firms|analysts)\//.test(useLocation().pathname)

  return (
    <>
      <header className="site-header">
        <div className="container site-header-inner">
          <NavLink to="/" className="brand">
            Analyst Scorecard
          </NavLink>
          <nav aria-label="Main">
            <NavLink to="/" end className={({ isActive }) => (isActive || onProfile ? 'active' : '')}>
              Leaderboard
            </NavLink>
            <NavLink to="/stocks">Stocks</NavLink>
            <NavLink to="/feed">Live feed</NavLink>
          </nav>
        </div>
      </header>
      <main className="container">
        <Suspense fallback={<div className="chart-skeleton chart-skeleton-tall" aria-busy="true" />}>
          <Outlet />
        </Suspense>
      </main>
      <footer className="container site-footer">
        Price charts by{' '}
        <a href="https://www.tradingview.com/" target="_blank" rel="noreferrer">
          TradingView
        </a>{' '}
        Lightweight Charts.
      </footer>
    </>
  )
}
