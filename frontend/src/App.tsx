import './App.css'
import { apiBaseUrl } from './config'

function App() {
  return (
    <main>
      <p className="eyebrow">University MVP foundation</p>
      <h1>Meeting Minutes AI</h1>
      <p className="persian" lang="fa" dir="rtl">
        سامانه هوشمند تولید صورت‌جلسه
      </p>
      <p className="boundary">
        The Phase 1 frontend foundation is ready. Upload, processing, editing,
        and export features intentionally begin in later phases.
      </p>
      <dl>
        <div>
          <dt>Backend configuration</dt>
          <dd>{apiBaseUrl}</dd>
        </div>
        <div>
          <dt>Privacy default</dt>
          <dd>Local processing</dd>
        </div>
      </dl>
    </main>
  )
}

export default App
