import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { createBrowserRouter, RouterProvider } from 'react-router'
import { Layout } from './components/Layout'
import './index.css'
import { ApiError } from './lib/errors'
import { LeaderboardPage } from './pages/LeaderboardPage'
import { NotFoundPage, RouteErrorPage } from './pages/NotFoundPage'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // Data changes once a day after ingestion, and the API rate-limits anonymous clients.
      staleTime: 5 * 60_000,
      refetchOnWindowFocus: false,
      // Client errors (400, 404, 429) won't fix themselves on retry.
      retry: (failures, error) => !(error instanceof ApiError && error.status < 500) && failures < 2,
    },
  },
})

const router = createBrowserRouter([
  {
    element: <Layout />,
    errorElement: <RouteErrorPage />,
    children: [
      { index: true, element: <LeaderboardPage /> },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
])

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  </StrictMode>,
)
