import { useEffect, useState } from 'react'
import type { Plot } from '../../components/Sidebar/Sidebar'
import SensorChart from '../../components/SensorChart/SensorChart'
import '../../App.css'

type Point = { value: number; createdAt: string }
type ApiData = { latest: Record<string, Point>; history: Record<string, Point[]> }
type CameraImage = {
  cameraId: string
  plotId: string | null
  imageUrl: string | null
  capturedAt: string | null
}

const cameraIds = ['cam_01', 'cam_02', 'cam_03', 'cam_04']

const isFreshReading = (value: Point | undefined) => {
  if (!value?.createdAt) return false
  const ageMs = Date.now() - new Date(value.createdAt).getTime()
  return ageMs >= 0 && ageMs < 60 * 1000
}

const hasRecentSensorData = (payload: ApiData | null) => {
  const latest = payload?.latest ?? {}
  const values = Object.values(latest)
  return values.length > 0 && values.some((value) => isFreshReading(value))
}

const normalizeCameraKey = (value: string | null | undefined) => {
  if (!value) return ''

  const cleaned = value.trim().toLowerCase()
  const match = cleaned.match(/(?:cam|camera)[^\d]*(\d+)/)
  if (match?.[1]) return `cam_${Number(match[1]).toString().padStart(2, '0')}`
  return cleaned
}

type Props = { plot: Plot; onBack: () => void; onLogout: () => void }

export default function PlotDashboard({ plot, onBack, onLogout }: Props) {
  const [data, setData] = useState<ApiData | null>(null)
  const [live, setLive] = useState(false)
  const [cameraImages, setCameraImages] = useState<Record<string, CameraImage | null>>({})

  const resolvedPlotId = plot.id === 'A1' || plot.id === 'A2' ? 'plot_1' : plot.id

  useEffect(() => {
    let stopped = false

    const loadSensor = async () => {
      try {
        const response = await fetch('/api/Sensor?limit=30')
        if (!response.ok) throw new Error('sensor fetch failed')
        const next = (await response.json()) as ApiData
        const connected = hasRecentSensorData(next)

        if (!stopped) {
          setData(next)
          setLive(connected)
        }
      } catch {
        if (!stopped) {
          setData(null)
          setLive(false)
        }
      }
    }

    const loadCameraImages = async () => {
      const params = new URLSearchParams()
      params.set('plotId', resolvedPlotId)
      cameraIds.forEach((cameraId) => params.append('cameraIds', cameraId))

      try {
        const response = await fetch(`/api/Image/latest?${params.toString()}`)
        if (!response.ok) throw new Error('camera fetch failed')
        const payload = (await response.json()) as { cameras?: CameraImage[] }
        const next: Record<string, CameraImage | null> = {}

        cameraIds.forEach((cameraId) => {
          next[cameraId] = null
        })

        payload.cameras?.forEach((item) => {
          if (item?.cameraId) {
            const key = normalizeCameraKey(item.cameraId)
            if (key) next[key] = item
          }
        })

        if (!stopped) setCameraImages(next)
      } catch {
        if (!stopped) {
          const fallback: Record<string, CameraImage | null> = {}
          cameraIds.forEach((cameraId) => {
            fallback[cameraId] = null
          })
          setCameraImages(fallback)
        }
      }
    }

    loadSensor()
    loadCameraImages()

    const sensorTimer = window.setInterval(loadSensor, 5000)
    const imageTimer = window.setInterval(loadCameraImages, 30000)

    return () => {
      stopped = true
      window.clearInterval(sensorTimer)
      window.clearInterval(imageTimer)
    }
  }, [resolvedPlotId])

  const temperaturePoints = data?.history.temperature ?? []
  const humidityPoints = data?.history.humidity ?? []
  const soilPhPoints = data?.history.soil_ph ?? []
  const temperature = data?.latest.temperature ?? { value: 0, createdAt: new Date().toISOString() }
  const humidity = data?.latest.humidity ?? { value: 0, createdAt: new Date().toISOString() }
  const soilPh = data?.latest.soil_ph
  const waterFlow = data?.latest.water_flow
  const waterIsFlowing = waterFlow !== undefined && waterFlow.value > 0 && isFreshReading(waterFlow)

  return (
    <div className="app-shell">
      <main className="main-content">
        <header className="topbar">
          <div>
            <span className="breadcrumb">สมชาย มีสุข / แปลง {plot.id}</span>
            <h1>
              แปลง {plot.id} <em>—</em> {plot.crop}
            </h1>
          </div>
          <div className="topbar-actions">
            <span className="location">⌖ โซน A · ทิศเหนือ · 200 ตร.ม.</span>
            <button className="back-button" type="button" onClick={onBack}>← เลือกแปลง</button>
            <button className="logout-button" type="button" onClick={onLogout}>ออกจากระบบ</button>
          </div>
        </header>

        <div className="content">
          <div className="welcome-row">
            <div>
              <p className="section-kicker">ภาพรวมแปลงปลูก</p>
              <h2 className="page-title">สถานะวันนี้</h2>
            </div>
            <span className={`status-pill ${live ? '' : 'waiting'}`}>
              <i />
              {live ? 'เซนเซอร์เชื่อมต่ออยู่' : 'เซนเซอร์ไม่ได้เชื่อมต่อ'}
            </span>
          </div>

          <div className="summary-grid">
            <section className="panel moisture-card">
              <div className="panel-heading">
                <div>
                  <span className="eyebrow">SOIL MOISTURE</span>
                  <h2>ความชื้นดิน</h2>
                </div>
                <span className="icon green">◌</span>
              </div>
              <div className="gauge">
                <div>
                  <strong>{humidity.value.toFixed(0)}</strong>
                  <small>% RH</small>
                </div>
              </div>
              <div className="gauge-copy">
                <b>เหมาะสม</b>
                <span>ระดับดีสำหรับผักเคล</span>
              </div>
            </section>

            <section className="panel temperature-card">
              <div className="panel-heading">
                <div>
                  <span className="eyebrow">AIR TEMPERATURE</span>
                  <h2>อุณหภูมิ</h2>
                </div>
                <span className="icon orange">♨</span>
              </div>
              <div className="big-number">
                {temperature.value.toFixed(1)}
                <small>°C</small>
              </div>
              <div className="temperature-bar">
                <i />
              </div>
              <div className="range-labels">
                <span>เย็น</span>
                <span>ร้อน</span>
              </div>
            </section>

            <section className="panel water-card">
              <div className="panel-heading">
                <div>
                  <span className="eyebrow">WATER FLOW</span>
                  <h2>อัตราการไหลของน้ำ</h2>
                </div>
                <span className="icon blue">≋</span>
              </div>
              <div className="big-number">
                {waterFlow ? waterFlow.value.toFixed(1) : '--'}
                <small>ลิตร/นาที</small>
              </div>
              <span className="muted-copy">{waterIsFlowing ? 'กำลังมีการไหลของน้ำ' : 'ไม่มีการไหลของน้ำ'}</span>
            </section>
          </div>

          <div className="chart-stack">
            <SensorChart title="อุณหภูมิ" unit="°C" color="#d96a3d" points={temperaturePoints} latest={temperature} />
            <SensorChart title="ความชื้นดิน" unit="%" color="#2e8b57" points={humidityPoints} latest={humidity} />
            <SensorChart title="ค่า pH ดิน" unit="pH" color="#7b61a8" points={soilPhPoints} latest={soilPh} />
          </div>

          <section className="panel camera-panel">
            <div className="panel-heading camera-panel-header">
              <div>
                <span className="eyebrow">LIVE CAMERA</span>
                <h2>ภาพล่าสุดจากกล้อง</h2>
              </div>
              <span className="camera-tag">{resolvedPlotId}</span>
            </div>

            <div className="camera-grid">
              {cameraIds.map((cameraId) => {
                const item = cameraImages[cameraId]

                return (
                  <div key={cameraId} className="camera-card">
                    <div className="camera-topbar">
                      <span className="camera-label">{cameraId}</span>
                      <span className="camera-time">
                        {item?.capturedAt ? new Date(item.capturedAt).toLocaleString('th-TH', { dateStyle: 'short', timeStyle: 'short' }) : 'รอภาพ'}
                      </span>
                    </div>

                    {item?.imageUrl ? (
                      <img src={item.imageUrl} alt={cameraId} className="camera-image" />
                    ) : (
                      <div className="camera-empty">
                        <span>ยังไม่มีภาพ</span>
                      </div>
                    )}
                  </div>
                )
              })}
            </div>
          </section>
        </div>
      </main>
    </div>
  )
}
