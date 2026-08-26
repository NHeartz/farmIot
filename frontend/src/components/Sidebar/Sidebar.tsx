import './Sidebar.css'

export type Plot = { id: string; name: string; crop: string; permitted: boolean }

type SidebarProps = {
  plots: Plot[]
  selectedPlotId?: string
  onSelectPlot: (plot: Plot) => void
  onLogout: () => void
}

export default function Sidebar({ plots, selectedPlotId, onSelectPlot, onLogout }: SidebarProps) {
  return <aside className="sidebar"><div className="brand"><span>⌁</span> GreenPlot</div><div className="farm-title"><b>▦</b><div><strong>สมชาย มีสุข</strong><small>{plots.filter((plot) => plot.permitted).length} แปลงที่ดูแล</small></div></div><div className="map-label">ผังแปลง</div><small className="zone-label">โซน A</small><div className="plots">{plots.map((plot) => <button key={plot.id} type="button" className={`plot ${plot.permitted ? '' : 'locked'} ${selectedPlotId === plot.id ? 'active' : ''}`} disabled={!plot.permitted} onClick={() => onSelectPlot(plot)}><b>{plot.id}</b><small>{plot.crop}</small><i /></button>)}</div><small className="zone-label">โซน B</small><div className="locked-zone">▧　▧　▧</div><small className="zone-label">โซน C</small><div className="plots mini"><div>▧</div><div>▧</div><div>▧</div></div><div className="map-legend">● ระบบ　<span>● ออนไลน์</span></div><button className="logout" type="button" onClick={onLogout}>↪ ออกจากระบบ</button></aside>
}
