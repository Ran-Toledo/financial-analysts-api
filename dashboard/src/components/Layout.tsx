import { NavLink, Outlet } from 'react-router'

export function Layout() {
  return (
    <>
      <header className="site-header">
        <div className="container site-header-inner">
          <NavLink to="/" className="brand">
            Analyst Scorecard
          </NavLink>
          <nav aria-label="Main">
            <NavLink to="/" end>
              Leaderboard
            </NavLink>
          </nav>
        </div>
      </header>
      <main className="container">
        <Outlet />
      </main>
    </>
  )
}
