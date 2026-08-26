import type { Plot } from '../../components/Sidebar/Sidebar'
import './PlotSelectionPage.css'

type PlotSelectionPageProps = { userName: string; plots: Plot[]; onSelect: (plot: Plot) => void; onLogout: () => void }

export default function PlotSelectionPage({ userName, plots, onSelect, onLogout }: PlotSelectionPageProps) {
  return <main className="plot-selection"><header><div className="brand dark"><span>⌁</span> GreenPlot</div><button type="button" onClick={onLogout}>ออกจากระบบ</button></header><section className="plot-selection-content"><span className="eyebrow">FARM OVERVIEW</span><h1>สวัสดี, {userName}</h1><p>เลือกแปลงผักที่คุณต้องการดูแล</p><div className="plot-cards">{plots.map((plot) => <button key={plot.id} type="button" disabled={!plot.permitted} className={plot.permitted ? '' : 'locked'} onClick={() => onSelect(plot)}><span className="plot-card-icon">{plot.permitted ? '⌁' : '▧'}</span><strong>แปลง {plot.id}</strong><small>{plot.crop}</small><em>{plot.permitted ? 'ดูข้อมูลแปลง' : 'ไม่มีสิทธิ์เข้าถึง'}</em></button>)}</div></section></main>
}
