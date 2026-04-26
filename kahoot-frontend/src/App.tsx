import { useState, useEffect } from 'react';
import 'bootstrap/dist/css/bootstrap.min.css';
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { useSignalR } from './hooks/useSignalR';
import './App.css';
import './premium-theme.css';

// Sayfalarımızı içe aktarıyoruz
import Home from './pages/Home';
import HostView from './pages/HostView';
import PlayerView from './pages/PlayerView';

function App() {
  const { isConnected, connection } = useSignalR();
  const [theme, setTheme] = useState<'light' | 'dark'>('light');

  // YENİ: Tema değişikliğini ve Modern Fontu (Poppins) uygula
  useEffect(() => {
    document.documentElement.setAttribute('data-bs-theme', theme);
    
    const link = document.createElement('link');
    link.href = 'https://fonts.googleapis.com/css2?family=Poppins:wght@400;600;800&display=swap';
    link.rel = 'stylesheet';
    document.head.appendChild(link);
    document.body.style.fontFamily = "'Poppins', sans-serif";
  }, [theme]);

  const toggleTheme = () => setTheme(prev => prev === 'light' ? 'dark' : 'light');

  return (
    <BrowserRouter>
      {/* Üst Kısım: Tüm sayfalarda her zaman görünecek olan bağlantı durumu (Navbar gibi) */}
      <div className="bg-dark text-white py-2 shadow-sm d-flex justify-content-between align-items-center px-4">
        <div>
            {isConnected ? (
            <span className="text-success fw-bold">🟢 Bağlantı Aktif</span>
            ) : (
            <span className="text-warning fw-bold">🔴 Bağlanılıyor...</span>
            )}
        </div>
        <button className="btn btn-sm btn-outline-light rounded-pill px-3 fw-bold" onClick={toggleTheme}>
            {theme === 'light' ? '🌙 Dark Mode' : '☀️ Light Mode'}
        </button>
      </div>

      {/* Orta Kısım: Adrese (URL) göre değişen dinamik sayfalar */}
      <Routes>
        <Route path="/" element={<Home />} />
        <Route path="/host" element={<HostView connection={connection} />} />
        <Route path="/player" element={<PlayerView connection={connection} />} />
      </Routes>
    </BrowserRouter>
  )
}

export default App
