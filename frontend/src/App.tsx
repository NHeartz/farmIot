import { useState } from 'react'
import LoginPage from './pages/LoginPage/LoginPage'
import PlotSelectionPage from './pages/PlotSelectionPage/PlotSelectionPage'
import PlotDashboard from './pages/PlotDashboard/PlotDashboard'
import type { Plot } from './components/Sidebar/Sidebar'

const userPlots: Plot[] = [
  { id: 'A1', name: 'แปลง A1', crop: 'ผักเคล', permitted: true },
  { id: 'A2', name: 'แปลง A2', crop: 'ผักเคล', permitted: false },
]

type Page = 'login' | 'plots' | 'dashboard'

function App() {
  const [page, setPage] = useState<Page>('login')
  const [userName, setUserName] = useState('')
  const [selectedPlot, setSelectedPlot] = useState<Plot | null>(null)

  const login = (name: string) => { setUserName(name); setPage('plots') }
  const selectPlot = (plot: Plot) => { if (plot.permitted) { setSelectedPlot(plot); setPage('dashboard') } }
  const logout = () => { setSelectedPlot(null); setUserName(''); setPage('login') }

  if (page === 'login') return <LoginPage onLogin={login} />
  if (page === 'plots') return <PlotSelectionPage userName={userName} plots={userPlots} onSelect={selectPlot} onLogout={logout} />
  return selectedPlot ? <PlotDashboard plot={selectedPlot} plots={userPlots} onSelectPlot={selectPlot} onLogout={logout} /> : <PlotSelectionPage userName={userName} plots={userPlots} onSelect={selectPlot} onLogout={logout} />
}

export default App
