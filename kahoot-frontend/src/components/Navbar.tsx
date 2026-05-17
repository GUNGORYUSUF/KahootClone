import { useGoogleLogin } from '@react-oauth/google';
import { useAuth } from '../context/AuthContext';
import { useState } from 'react';
import { Link } from 'react-router-dom';

interface Props {
    readonly isLive: boolean;
    readonly theme: 'light' | 'dark';
    readonly toggleTheme: () => void;
}

export default function Navbar({ isLive, theme, toggleTheme }: Props) {
    const { user, login, logout } = useAuth();
    const [isLoading, setIsLoading] = useState(false);

    // YENİ: Sisteme kalıcı olarak (Global) Google ile giriş yapma işlemi
    const globalGoogleLogin = useGoogleLogin({
        onSuccess: async (tokenResponse) => {
            setIsLoading(true);
            try {
                // Backend'deki yeni AuthController'a Google Token'ı gönderiyoruz
                const res = await fetch("http://localhost:5252/api/Auth/google-login", {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ credential: tokenResponse.access_token })
                });

                if (!res.ok) throw new Error("Giriş başarısız oldu");

                const data = await res.json();
                // Backend bize kendi ürettiği JWT'yi ve veritabanına kaydettiği User objesini döndü
                login(data.token || data.Token, data.user || data.User);
            } catch (err) {
                console.error("Global Login Hatası:", err);
                alert("Giriş yapılamadı!");
            } finally {
                setIsLoading(false);
            }
        },
        onError: () => alert("Google ile giriş iptal edildi.")
    });

    const enableGoogleLogin = import.meta.env.VITE_ENABLE_GOOGLE_LOGIN === 'true';

    return (
        <div className="bg-dark text-white py-2 shadow-sm d-flex justify-content-between align-items-center px-4">
            <div>
                {isLive ? (
                    <span className="text-success fw-bold">🟢 Bağlantı Aktif</span>
                ) : (
                    <span className="text-warning fw-bold">🔴 Bağlanılıyor...</span>
                )}
            </div>
            
            <div className="d-flex align-items-center gap-3">
                {enableGoogleLogin && (
                    user ? (
                        <div className={`d-flex align-items-center gap-2 px-3 py-1 rounded-pill shadow-sm ${theme === 'dark' ? 'bg-secondary text-white' : 'bg-light text-dark'}`}>
                            <Link to="/profile" className={`text-decoration-none d-flex align-items-center gap-2 ${theme === 'dark' ? 'text-white' : 'text-dark'}`} title="Profili Düzenle">
                                {user.avatarUrl && <img src={user.avatarUrl} alt="Avatar" className="rounded-circle" style={{ width: '24px', height: '24px', objectFit: 'cover' }} referrerPolicy="no-referrer" />}
                                <span className="fw-bold fs-6">{user.nickname}</span>
                            </Link>
                            <button className="btn btn-sm btn-danger rounded-pill ms-2 fw-bold" style={{ fontSize: '0.7rem' }} onClick={logout}>Çıkış</button>
                        </div>
                    ) : (
                        <button className={`btn btn-sm rounded-pill px-3 fw-bold shadow-sm d-flex align-items-center gap-2 ${theme === 'dark' ? 'btn-outline-light' : 'btn-light border text-dark'}`} onClick={() => globalGoogleLogin()} disabled={isLoading}>
                            {isLoading ? "Bekleniyor..." : (
                                <>
                                    <img src="https://www.svgrepo.com/show/475656/google-color.svg" alt="Google" style={{ width: '16px', height: '16px' }} />
                                    {" "}Giriş Yap
                                </>
                            )}
                        </button>
                    )
                )}

                <button className="btn btn-sm btn-outline-light rounded-pill px-3 fw-bold" onClick={toggleTheme}>
                    {theme === 'light' ? '🌙 Dark Mode' : '☀️ Light Mode'}
                </button>
            </div>
        </div>
    );
}