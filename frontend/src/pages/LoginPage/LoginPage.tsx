import { useState } from 'react'
import type { FormEvent } from 'react'
import './LoginPage.css'

type LoginPageProps = { onLogin: (userName: string) => void }

export default function LoginPage({ onLogin }: LoginPageProps) {
  const [userName, setUserName] = useState('สมชาย มีสุข')
  const submit = (event: FormEvent) => { event.preventDefault(); onLogin(userName.trim() || 'ผู้ใช้งาน') }
  return <main className="login-page"><section className="login-brand"><span>⌁</span><p>GreenPlot</p><small>ระบบจัดการแปลงปลูกอัจฉริยะ</small></section><form className="login-form" onSubmit={submit}><span className="eyebrow">WELCOME BACK</span><h1>เข้าสู่ระบบ</h1><p>จัดการข้อมูลเซนเซอร์และแปลงผักของคุณ</p><label>ชื่อผู้ใช้งาน<input value={userName} onChange={(event) => setUserName(event.target.value)} placeholder="กรอกชื่อผู้ใช้งาน" /></label><label>รหัสผ่าน<input type="password" defaultValue="greenplot" placeholder="กรอกรหัสผ่าน" /></label><button type="submit">เข้าสู่ระบบ</button><small className="login-hint">Demo: ใช้ชื่อใดก็ได้เพื่อเข้าสู่ระบบ</small></form></main>
}
