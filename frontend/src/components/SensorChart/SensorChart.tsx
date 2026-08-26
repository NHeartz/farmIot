import './SensorChart.css'

type Point = { value: number; createdAt: string }
type SensorChartProps = { title: string; unit: string; color: string; points: Point[]; latest?: Point }

function makePath(values: number[]) {
  if (!values.length) return ''
  const min = Math.min(...values) - 1
  const max = Math.max(...values) + 1
  return values.map((value, index) => { const x = index / Math.max(values.length - 1, 1) * 520; const y = 148 - (value - min) / (max - min) * 148; return `${index ? 'L' : 'M'} ${x.toFixed(1)} ${y.toFixed(1)}` }).join(' ')
}

export default function SensorChart({ title, unit, color, points, latest }: SensorChartProps) {
  const values = points.map((point) => point.value)
  return <section className="panel chart-panel"><div className="panel-heading"><div><span className="eyebrow">LIVE SENSOR</span><h2>{title}</h2></div><strong className="chart-value" style={{ color }}>{latest?.value.toFixed(1) ?? '--'}<small>{unit}</small></strong></div><div className="chart-wrap"><svg viewBox="0 0 520 170" preserveAspectRatio="none" aria-label={`${title} chart`} role="img">{[20,55,90,125,160].map((y) => <line key={y} x1="0" y1={y} x2="520" y2={y} className="grid-line" />)}<path d={makePath(values)} fill="none" stroke={color} strokeWidth="3" strokeLinecap="round" vectorEffect="non-scaling-stroke" /></svg><div className="chart-labels"><span>00:00</span><span>06:00</span><span>12:00</span><span>18:00</span><span>24:00</span></div></div><div className="chart-footer"><span><i className="line-dot" style={{ background: color }} /> {title} ({unit.trim()})</span><span>{latest ? new Date(latest.createdAt).toLocaleTimeString('th-TH', { hour: '2-digit', minute: '2-digit' }) : 'ยังไม่มีข้อมูล'}</span></div></section>
}
