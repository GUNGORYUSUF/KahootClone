import { useState } from 'react';
import { useAuth } from '../context/AuthContext';
import { Link } from 'react-router-dom';

export default function Profile() {
    const { user, token, login } = useAuth();
    const [nickname, setNickname] = useState(user?.nickname || '');
    const [avatarUrl, setAvatarUrl] = useState(user?.avatarUrl || '');
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [success, setSuccess] = useState(false);

    if (!user) {
        return (
            <div className="container mt-5 text-center">
                <h2>Giriş yapmadınız.</h2>
                <Link to="/" className="btn btn-primary mt-3 fw-bold">Ana Sayfaya Dön</Link>
            </div>
        );
    }

    const handleSave = async (e: React.FormEvent) => {
        e.preventDefault();
        setError(null);
        setSuccess(false);
        setIsLoading(true);

        try {
            const res = await fetch("http://localhost:5252/api/Auth/profile", {
                method: "PUT",
                headers: { 
                    "Content-Type": "application/json",
                    "Authorization": `Bearer ${token}`
                },
                body: JSON.stringify({ nickname, avatarUrl })
            });

            if (!res.ok) throw new Error("Profil güncellenemedi.");

            const data = await res.json();
            // Yeni bilgileri içeren güncel token ve kullanıcı nesnesini kaydet
            login(data.token || data.Token, data.user || data.User);
            setSuccess(true);
        } catch (err: any) {
            setError(err.message);
        } finally {
            setIsLoading(false);
        }
    };

    return (
        <div className="container mt-5">
            <div className="row justify-content-center">
                <div className="col-md-6 col-lg-5">
                    <div className="mb-3 text-start">
                        <Link to="/" className="btn btn-sm btn-outline-secondary fw-bold shadow-sm">
                            ⬅️ Ana Sayfa
                        </Link>
                    </div>
                    <div className="card shadow-lg border-0 bg-white">
                        <div className="card-body p-5">
                            <h2 className="fw-bold mb-4 text-center text-dark">👤 Profilim</h2>
                            
                            <div className="text-center mb-4">
                                {avatarUrl ? (
                                    <img src={avatarUrl} alt="Avatar Preview" className="rounded-circle shadow border border-3 border-primary" style={{ width: '100px', height: '100px', objectFit: 'cover' }} referrerPolicy="no-referrer" />
                                ) : (
                                    <div className="rounded-circle bg-secondary text-white d-flex align-items-center justify-content-center mx-auto shadow" style={{ width: '100px', height: '100px', fontSize: '3rem' }}>
                                        🧑‍💻
                                    </div>
                                )}
                            </div>

                            {error && <div className="alert alert-danger fw-bold">{error}</div>}
                            {success && <div className="alert alert-success fw-bold">Profil başarıyla güncellendi!</div>}

                            <form onSubmit={handleSave}>
                                <div className="mb-3">
                                    <label className="form-label fw-bold text-dark">Takma Ad (Nickname)</label>
                                    <input 
                                        type="text" 
                                        className="form-control form-control-lg fw-bold" 
                                        value={nickname}
                                        onChange={(e) => setNickname(e.target.value)}
                                        maxLength={15}
                                        minLength={3}
                                        required
                                    />
                                </div>
                                <div className="mb-4">
                                    <label className="form-label fw-bold text-dark">Profil Resmi URL</label>
                                    <input 
                                        type="url" 
                                        className="form-control form-control-lg" 
                                        value={avatarUrl}
                                        onChange={(e) => setAvatarUrl(e.target.value)}
                                        placeholder="https://..."
                                    />
                                </div>
                                <button 
                                    type="submit" 
                                    className="btn btn-primary btn-lg w-100 fw-bold shadow"
                                    disabled={isLoading || nickname.trim().length < 3}
                                >
                                    {isLoading ? "Kaydediliyor..." : "💾 Değişiklikleri Kaydet"}
                                </button>
                            </form>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
}