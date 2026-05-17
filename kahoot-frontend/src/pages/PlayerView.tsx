import { HubConnection } from '@microsoft/signalr';
import { Link } from 'react-router-dom';
import { useKahootPlayer } from '../hooks/useKahootPlayer';

interface Props {
    readonly connection: HubConnection | null;
}

export default function PlayerView({ connection }: Props) {
    const { pin, setPin, nickname, setNickname, isJoined, setIsJoined, error, setError, isLoading, setIsLoading, currentQuestion, setCurrentQuestion, waitPhase, setWaitPhase, gameEndedLeaderboard, setGameEndedLeaderboard, timeLeft, hasAnswered, setHasAnswered, answerResult, setAnswerResult, isLastQuestion, isGettingReady, readyCountdown, answerStats, enableGoogleLogin, loginWithGoogle, handleJoin, submitAnswer, user, token } = useKahootPlayer(connection);

    // Bilişsel Karmaşıklığı (Cognitive Complexity) azaltmak için fonksiyonları JSX'ten dışarı alıyoruz
    const handleReplay = () => {
        setCurrentQuestion(null);
        setWaitPhase(null);
        setGameEndedLeaderboard(null);
        setHasAnswered(false);
        setAnswerResult(null);
    };

    const handleLeaveGame = async () => {
        await connection?.invoke("LeaveGame");
        sessionStorage.removeItem("kahoot_player_pin");
        setPin("");
        setIsJoined(false);
        setCurrentQuestion(null);
        setWaitPhase(null);
        setGameEndedLeaderboard(null);
        setHasAnswered(false);
        setAnswerResult(null);
    };

    // OYUN BİTTİ EKRANI (GAME ENDED)
    if (gameEndedLeaderboard) {
        const myRank = gameEndedLeaderboard.findIndex(p => p.nickname === nickname.trim()) + 1;
        const myData = gameEndedLeaderboard.find(p => p.nickname === nickname.trim());

        return (
            <div className="container mt-5 text-center">
                <div className="card shadow-lg border-0">
                    <div className="card-body py-5">
                        <h1 className="display-1 fw-bold text-warning mb-4">🏆 Oyun Bitti! 🏆</h1>
                        
                        <div className="bg-light rounded-4 p-4 mx-auto my-4 shadow-sm" style={{ maxWidth: '500px' }}>
                            {myRank > 0 ? (
                                <>
                                    <h3 className="fw-bold mb-3">Tebrikler! Sıralaman: <span className="text-danger">{myRank}.</span></h3>
                                    <h4 className="text-muted">Toplam Puan: {myData?.score}</h4>
                                </>
                            ) : (
                                <h3 className="fw-bold text-muted">Oyunu tamamladınız.</h3>
                            )}
                        </div>

                        <div className="bg-light rounded-4 p-3 mx-auto my-4" style={{ maxWidth: '500px' }}>
                            <h4 className="fw-bold mb-3">Final Tablosu (İlk 5)</h4>
                            <ul className="list-group list-group-flush fs-5 fw-bold text-start">
                                {gameEndedLeaderboard.slice(0, 5).map((p, index) => {
                                    const isMe = p.nickname === nickname.trim();
                                    return (
                                        <li key={p.id} className={`list-group-item d-flex justify-content-between align-items-center ${isMe ? 'bg-success text-white rounded' : ''}`}>
                                            <span className="d-flex align-items-center gap-2">
                                                <span>{["👑 ", "🥈 ", "🥉 "][index] || `${index + 1}. `}</span>
                                                {p.avatarUrl && <img src={p.avatarUrl} alt="avatar" className="rounded-circle shadow-sm" style={{ width: '32px', height: '32px', objectFit: 'cover' }} referrerPolicy="no-referrer" />}
                                                <span>{p.nickname} {isMe && "(Sen)"}</span>
                                            </span>
                                            <span className={`badge ${isMe ? 'bg-light text-success' : 'bg-danger'} rounded-pill`}>{p.score} Puan</span>
                                        </li>
                                    )
                                })}
                            </ul>
                        </div>

                        <div className="mt-4 d-flex flex-column gap-3 align-items-center">
                            <button 
                                className="btn btn-primary btn-lg w-100 py-3 fs-5 shadow fw-bold" 
                                style={{ maxWidth: '400px' }}
                                onClick={handleReplay}
                            >
                                🔄 Yeniden Oyna (Aynı Lobi)
                            </button>
                            <button 
                                className="btn btn-danger btn-lg w-100 py-3 fs-5 shadow fw-bold" 
                                style={{ maxWidth: '400px' }}
                                onClick={handleLeaveGame}
                            >
                                 🚪 Ana Ekrana Dön
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        );
    }

    // HAZIR OL (3-2-1) EKRANI
    if (isGettingReady) {
        return (
            <div className="container mt-5 text-center d-flex flex-column align-items-center justify-content-center" style={{ minHeight: '60vh' }}>
                <h1 className="display-1 fw-bold text-success mb-4" style={{ fontSize: '4rem' }}>Oyun Başlıyor!</h1>
                <div className="rounded-circle bg-success text-white d-flex align-items-center justify-content-center shadow-lg" style={{ width: '150px', height: '150px', fontSize: '5rem', fontWeight: '800' }}>
                    {readyCountdown}
                </div>
            </div>
        );
    }

    // YENİ: BEKLEME / ARA GEÇİŞ EKRANI (WAIT PHASE)
    if (waitPhase) {
        const correctOption = currentQuestion?.options.find(o => o.id === waitPhase.correctOptionId);

        return (
            <div className="container mt-5 text-center">
                {/* Üst Kısım: Doğru / Yanlış / Süre Doldu Durumu */}
                {answerResult ? (
                    <div className={`card shadow-sm border-0 ${answerResult.isCorrect ? 'bg-success' : 'bg-danger'} text-white mb-4`}>
                        <div className="card-body py-4">
                            <h1 className="display-4 fw-bold mb-2">{answerResult.isCorrect ? '✅ Doğru Cevap!' : '❌ Yanlış Cevap!'}</h1>
                            {answerResult.isCorrect ? (
                                <p className="lead fs-4 mb-0">+{answerResult.points} Puan Kazandın!</p>
                            ) : (
                                <p className="lead fs-4 mb-0">Doğru Cevap: <strong>{correctOption?.text || "Bilinmiyor"}</strong></p>
                            )}
                        </div>
                    </div>
                ) : (
                    <div className="card shadow-sm border-0 bg-secondary text-white mb-4">
                        <div className="card-body py-4">
                            <h1 className="display-4 fw-bold mb-2">⏱️ Süre Doldu!</h1>
                            <p className="lead fs-4 mb-0">Cevap veremedin. Doğru Cevap: <strong>{correctOption?.text || "Bilinmiyor"}</strong></p>
                        </div>
                    </div>
                )}

                {/* Alt Kısım: Ara Liderlik Tablosu */}
                <div className="card shadow-lg border-0">
                    <div className="card-body py-5">
                        <h2 className="display-5 fw-bold text-primary mb-4">{isLastQuestion ? "Final Tablosu Hazırlanıyor..." : "Sıradaki Soru Bekleniyor..."}</h2>
                        
                        <div className="bg-light rounded-4 p-3 mx-auto my-4" style={{ maxWidth: '500px' }}>
                            <h4 className="fw-bold mb-3">Ara Liderlik Tablosu (İlk 5)</h4>
                            <ul className="list-group list-group-flush fs-5 fw-bold text-start">
                                {waitPhase.leaderboard && waitPhase.leaderboard.length > 0 ? (
                                    waitPhase.leaderboard.map((p, index) => {
                                        const isMe = p.nickname === nickname.trim();
                                        return (
                                            <li key={p.id} className={`list-group-item d-flex justify-content-between align-items-center ${isMe ? 'bg-success text-white rounded' : ''}`}>
                                                <span className="d-flex align-items-center gap-2">
                                                    <span>{["🥇 ", "🥈 ", "🥉 "][index] || `${index + 1}. `}</span>
                                                    {p.avatarUrl && <img src={p.avatarUrl} alt="avatar" className="rounded-circle shadow-sm" style={{ width: '32px', height: '32px', objectFit: 'cover' }} referrerPolicy="no-referrer" />}
                                                    <span>{p.nickname} {isMe && "(Sen)"}</span>
                                                </span>
                                                <span className={`badge ${isMe ? 'bg-light text-success' : 'bg-primary'} rounded-pill`}>{p.score} Puan</span>
                                            </li>
                                        )
                                    })
                                ) : (
                                    <p className="text-muted text-center mb-0">Henüz puan alan oyuncu yok.</p>
                                )}
                            </ul>
                        </div>

                        <div className="display-3 fw-bold mt-2 text-dark">⏳ {timeLeft}</div>
                    </div>
                </div>
            </div>
        );
    }

    // YENİ: OYUN BAŞLADIYSA (SORU EKRANI)
    if (currentQuestion) {
        if (hasAnswered) {
            return (
                <div className="container mt-5 text-center">
                <div className="card shadow-lg border-0">
                        <div className="card-body py-5">
                        <h1 className="display-1 fw-bold text-primary mb-4">⏳</h1>
                        <h2 className="display-4 fw-bold text-dark">Cevap Gönderildi!</h2>
                        <p className="lead mt-4 text-muted">Diğer oyuncuların cevaplaması veya sürenin bitmesi bekleniyor...</p>
                        <div className="badge bg-primary fs-4 mt-3 shadow-sm px-4 py-2">
                            📝 {answerStats.answered} / {answerStats.total} Kişi Cevapladı
                        </div>
                        <div className="display-3 fw-bold mt-4 text-primary">{timeLeft}</div>
                        </div>
                    </div>
                </div>
            );
        }

        return (
            <div className="container mt-5 text-center">
                <div className="d-flex justify-content-between align-items-center mb-4 px-3">
                    <h3 className="fw-bold text-dark mb-0">Hızlı Ol, Doğru Rengi Seç! 🚀</h3>
                    <div className="rounded-circle bg-white text-dark d-flex align-items-center justify-content-center shadow-sm" style={{ width: '60px', height: '60px', fontSize: '1.5rem', border: '3px solid #333', fontWeight: 'bold' }}>
                        {timeLeft}
                    </div>
                </div>
                <div className="mb-4">
                    <h2 className="display-6 fw-bold text-dark">{currentQuestion.text}</h2>
                    <div className="text-muted fw-bold mt-2">
                        📝 Cevaplayanlar: {answerStats.answered} / {answerStats.total}
                    </div>
                </div>
                <div className="row g-3">
                    {currentQuestion.options.map((opt, i) => {
                        const colors = ['bg-danger text-white', 'bg-primary text-white', 'bg-warning text-dark', 'bg-success text-white'];
                        return (
                            <div key={opt.id} className="col-6">
                                <button 
                                    className={`btn w-100 ${colors[i % 4]} border-0 option-btn d-flex align-items-center justify-content-center p-2`} 
                                    style={{ height: '20vh', minHeight: '150px' }}
                                    onClick={() => submitAnswer(opt.id)}
                                >
                                    <span className="fs-3 option-text" style={{ wordBreak: 'break-word' }}>{opt.text}</span>
                                </button>
                            </div>
                        );
                    })}
                </div>
            </div>
        );
    }

    if (isJoined) {
        return (
            <div className="container mt-5 text-center">
                <div className="card shadow-lg border-0">
                    <div className="card-body py-5">
                        <h1 className="display-1 fw-bold text-success mb-4">Sen İçeridesin!</h1>
                        <h3 className="mb-4 text-dark">Ekranda adını görüyor musun?</h3>
                        <output className="spinner-border text-success mt-3">
                            <span className="visually-hidden">Bekleniyor...</span>
                        </output>
                        <p className="mt-3 lead fw-bold text-muted">Oyunun başlaması bekleniyor...</p>
                        
                        <button 
                            className="btn btn-outline-danger mt-4 fw-bold px-4"
                            onClick={handleLeaveGame}
                        >
                            🚪 Lobiden Ayrıl
                        </button>
                    </div>
                </div>
            </div>
        );
    }

    return (
        <div className="container mt-5">
            <div className="row justify-content-center">
                <div className="col-md-6 col-lg-4">
                    <div className="mb-3 text-start">
                        <Link to="/" className="btn btn-sm btn-outline-secondary fw-bold shadow-sm">
                            ⬅️ Ana Sayfa
                        </Link>
                    </div>
                    <div className="card shadow-sm border-0 bg-primary text-white">
                        <div className="card-body p-5 text-center">
                            <h2 className="fw-bold mb-4">🎮 Oyuna Katıl</h2>
                            
                            {error && (
                                <div className="alert alert-danger fw-bold" role="alert">
                                    {error}
                                </div>
                            )}

                            <form onSubmit={handleJoin}>
                                <div className="mb-3">
                                    <label htmlFor="pinInput" className="visually-hidden">Oyun PIN</label>
                                    <input 
                                        id="pinInput"
                                        type="text" 
                                        className="form-control form-control-lg text-center fw-bold fs-4" 
                                        placeholder="Oyun PIN" 
                                        value={pin}
                                        onChange={(e) => {
                                            // SonarQube S6353: Daha temiz Regex Sınıfı (\D) kullanıldı
                                            const numericValue = e.target.value.replace(/\D/g, '');
                                            setPin(numericValue);
                                        }}
                                        inputMode="numeric"
                                        pattern="[0-9]*"
                                        maxLength={6}
                                        required
                                    />
                                </div>
                                <div className="mb-4">
                                    {user ? (
                                        <div className="d-flex align-items-center justify-content-center gap-3 bg-white text-dark rounded-pill py-2 px-4 shadow-sm border border-2 border-primary">
                                            {user.avatarUrl ? (
                                                <img src={user.avatarUrl} alt="avatar" className="rounded-circle shadow-sm" style={{ width: '40px', height: '40px', objectFit: 'cover' }} referrerPolicy="no-referrer" />
                                            ) : (
                                                <div className="rounded-circle bg-secondary text-white d-flex align-items-center justify-content-center shadow-sm" style={{ width: '40px', height: '40px', fontSize: '1.2rem' }}>👤</div>
                                            )}
                                            <div className="text-start">
                                                <div className="text-muted fw-bold mb-0" style={{ fontSize: '0.7rem', letterSpacing: '1px' }}>GİRİŞ YAPILDI</div>
                                                <div className="fw-bold fs-5 lh-1 mt-1">{user.nickname}</div>
                                            </div>
                                        </div>
                                    ) : (
                                        <>
                                            <label htmlFor="nicknameInput" className="visually-hidden">Takma Ad (Nickname)</label>
                                            <input 
                                                id="nicknameInput"
                                                type="text" 
                                                className="form-control form-control-lg text-center fw-bold fs-5" 
                                                placeholder="Takma Ad (Nickname)" 
                                                value={nickname}
                                                onChange={(e) => setNickname(e.target.value)}
                                                maxLength={15}
                                                minLength={3}
                                                required
                                            />
                                        </>
                                    )}
                                </div>
                                <button 
                                    type="submit" 
                                    className="btn btn-light text-primary btn-lg w-100 py-3 fw-bold fs-5 shadow-sm"
                                    disabled={isLoading || !connection || pin.length < 6 || nickname.trim().length < 3}
                                >
                                    {isLoading ? "Bağlanıyor..." : "Giriş Yap"}
                                </button>

                                {!user && enableGoogleLogin && (
                                    <>
                                        <div className="d-flex align-items-center my-3">
                                            <hr className="flex-grow-1 bg-white opacity-50" />
                                            <span className="mx-2 text-white fw-bold opacity-75 small">VEYA</span>
                                            <hr className="flex-grow-1 bg-white opacity-50" />
                                        </div>
                                        <button 
                                            type="button"
                                    className="btn btn-light text-primary w-100 py-3 fw-bold fs-5 shadow-sm d-flex justify-content-center align-items-center gap-2"
                                            onClick={() => loginWithGoogle()}
                                            disabled={isLoading || !connection || pin.length < 6}
                                        >
                                            <img src="https://www.svgrepo.com/show/475656/google-color.svg" alt="Google" style={{ width: '28px', height: '28px' }} />
                                            Google ile Katıl
                                        </button>
                                    </>
                                )}

                                {sessionStorage.getItem("kahoot_player_pin") && sessionStorage.getItem("kahoot_nickname") && (
                                    <button 
                                        type="button"
                                        className="btn btn-success btn-lg w-100 py-3 fw-bold fs-5 shadow-sm mt-3"
                                        onClick={async () => {
                                            setError(null);
                                            setIsLoading(true);
                                            try {
                                                const sp = sessionStorage.getItem("kahoot_player_pin")!;
                                                const sn = sessionStorage.getItem("kahoot_nickname")!;
                                                const st = sessionStorage.getItem("kahoot_session_token");
                                                const sa = user?.avatarUrl || sessionStorage.getItem("kahoot_avatar_url");
                                                const st_google = token || null;
                                                const success = await connection?.invoke("JoinGame", sp, sn, st || null, st_google, sa || null);
                                                if (success) {
                                                    setPin(sp);
                                                    setNickname(sn);
                                                    setIsJoined(true);
                                                } else {
                                                    setError("Oyuna dönülemedi. Oyun bitmiş veya iptal edilmiş olabilir.");
                                                    sessionStorage.removeItem("kahoot_player_pin");
                                                }
                                            } catch (err) {
                                                console.error(err);
                                                setError("Bağlantı hatası oluştu.");
                                                sessionStorage.removeItem("kahoot_player_pin");
                                            } finally {
                                                setIsLoading(false);
                                            }
                                        }}
                                        disabled={isLoading || !connection}
                                    >
                                        🔄 Kaldığın Yerden Devam Et ({sessionStorage.getItem("kahoot_nickname")})
                                    </button>
                                )}
                            </form>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
}