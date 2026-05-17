import { useState, useEffect } from 'react';
import 'bootstrap/dist/css/bootstrap.min.css';
import { HashRouter, Routes, Route } from 'react-router-dom';
import { useSignalR } from './hooks/useSignalR';
import { GoogleOAuthProvider } from '@react-oauth/google';
import { HubConnectionState } from '@microsoft/signalr';
import './App.css';
import './premium-theme.css';

import { AuthProvider } from './context/AuthContext.tsx';
import Navbar from './components/Navbar.tsx';
// Sayfalarımızı içe aktarıyoruz
import Home from './pages/Home';
import HostView from './pages/HostView';
import PlayerView from './pages/PlayerView';
import Profile from './pages/Profile';

function App() {
  const { connection } = useSignalR();
  const [theme, setTheme] = useState<'light' | 'dark'>('light');
  const [isLive, setIsLive] = useState(false);
  const googleClientId = import.meta.env.VITE_GOOGLE_CLIENT_ID || "GECICI_CLIENT_ID";

  // YENİ: Tema değişikliğini ve Modern Fontu (Poppins) uygula
  useEffect(() => {
    document.documentElement.dataset.bsTheme = theme;
    
    const link = document.createElement('link');
    link.href = 'https://fonts.googleapis.com/css2?family=Poppins:wght@400;600;800&display=swap';
    link.rel = 'stylesheet';
    document.head.appendChild(link);
    document.body.style.fontFamily = "'Poppins', sans-serif";
  }, [theme]);

  // YENİ: Bağlantı durumunu doğrudan SignalR'ın gerçek State nesnesi ile tam senkronize tut
  useEffect(() => {
    if (!connection) return;

    const syncConnectionStatus = () => {
      // Güvenli Enum Kontrolü: SignalR sürümüne bakılmaksızın tam uyumlu çalışır
      setIsLive(connection.state === HubConnectionState.Connected);
    };

    // İlk açılışta ve durum değişimlerinde kontrol et
    syncConnectionStatus();
    connection.onclose(syncConnectionStatus);
    connection.onreconnecting(syncConnectionStatus);
    connection.onreconnected(syncConnectionStatus);

    // Olası arayüz takılmalarına karşı (sessiz kopmalar) her 1 saniyede bir durumu anında doğrula
    const interval = setInterval(syncConnectionStatus, 1000);

    return () => clearInterval(interval);
  }, [connection]);

  const toggleTheme = () => setTheme(prev => prev === 'light' ? 'dark' : 'light');

  return (
    <GoogleOAuthProvider clientId={googleClientId}>
      <AuthProvider>
        <HashRouter>
          {/* Üst Kısım: Tüm sayfalarda her zaman görünecek olan Navbar */}
          <Navbar isLive={isLive} theme={theme} toggleTheme={toggleTheme} />

          {/* Orta Kısım: Adrese (URL) göre değişen dinamik sayfalar */}
          <Routes>
            <Route path="/" element={<Home />} />
            <Route path="/host" element={<HostView connection={connection} />} />
            <Route path="/player" element={<PlayerView connection={connection} />} />
            <Route path="/profile" element={<Profile />} />
          </Routes>
        </HashRouter>
      </AuthProvider>
    </GoogleOAuthProvider>
  )
}

export default App
