import { useState, useEffect } from 'react';
import { HubConnection } from '@microsoft/signalr';
import type { QuestionPacket, WaitPhasePayload, Player } from '../types/index';

interface Props {
    connection: HubConnection | null;
}

export default function HostView({ connection }: Props) {
    const [markdown, setMarkdown] = useState('');
    const [pin, setPin] = useState<string | null>(null);
    const [players, setPlayers] = useState<string[]>([]);
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    // YENİ: Soru ve süre state'leri
    const [currentQuestion, setCurrentQuestion] = useState<QuestionPacket | null>(null);
    const [waitPhase, setWaitPhase] = useState<WaitPhasePayload | null>(null);
    const [gameEndedLeaderboard, setGameEndedLeaderboard] = useState<Player[] | null>(null);
    const [timeLeft, setTimeLeft] = useState<number>(0);
    const [isLastQuestion, setIsLastQuestion] = useState<boolean>(false);
    const [isGettingReady, setIsGettingReady] = useState<boolean>(false);
    const [readyCountdown, setReadyCountdown] = useState<number>(3);

    useEffect(() => {
        if (!connection) return;

        // YENİ: Yeniden oyna dendiğinde aynı oyuncularla lobiye dön
        connection.on("RedirectToNewGame", async (payload: any) => {
            // Güvenli Veri Okuma: Backend'den büyük veya küçük harf gelse bile yakala
            const targetPin = payload.newPin || payload.NewPin;
            const targetPlayers = payload.players || payload.Players || [];

            setPin(targetPin);
            sessionStorage.setItem("kahoot_host_pin", targetPin);
            setPlayers(targetPlayers);
            setGameEndedLeaderboard(null);
            setWaitPhase(null);
            setCurrentQuestion(null);
            
            try {
                await connection.invoke("JoinAsManager", targetPin);
            } catch (err) {
                console.error("JoinAsManager (Yeniden Bağlanma) Hatası:", err);
            }
        });

        // Lobideki oyuncu hareketlerini dinle
        connection.on("PlayerJoined", (nickname: string) => {
            console.log("🟢 Oyuncu katıldı:", nickname);
            setPlayers(prev => {
                if (prev.includes(nickname)) return prev; // Aynı kişinin çift eklenmesini önle
                return [...prev, nickname];
            });
        });

        connection.on("PlayerLeft", (nickname: string) => {
            setPlayers(prev => prev.filter(p => p !== nickname));
        });

        // YENİ: 3-2-1 Geri Sayımını Başlat
        connection.on("GetReady", () => {
            setIsGettingReady(true);
            setReadyCountdown(3);
            let counter = 3;
            const interval = setInterval(() => {
                counter -= 1;
                setReadyCountdown(counter);
                if (counter <= 1) clearInterval(interval);
            }, 1000);
        });

        // YENİ: Soru geldiğinde ekranı değiştir
        connection.on("ReceiveQuestion", (question: QuestionPacket) => {
            setCurrentQuestion(question);
            setWaitPhase(null); // Soru geldiğinde bekleme ekranını kapat
            setIsGettingReady(false); // Hazırlık ekranını kapat
            setTimeLeft(question.timeLimit);
            setIsLastQuestion(question.currentIndex === question.totalQuestions);
        });

        // YENİ: Saniye güncellemelerini dinle
        connection.on("TimeUpdate", (time: number) => {
            setTimeLeft(time);
        });

        // YENİ: Soru bitip bekleme (Transition) aşamasına geçildiğinde
        connection.on("WaitPhase", (payload: WaitPhasePayload) => {
            setWaitPhase(payload);
            setCurrentQuestion(null); // Soruyu ekrandan kaldır
            setTimeLeft(payload.waitTime);
        });

        // YENİ: Bekleme aşamasındaki saniye güncellemeleri
        connection.on("WaitTimeUpdate", (time: number) => {
            setTimeLeft(time);
        });

        // YENİ: Oyun tamamen bittiğinde
        connection.on("GameEnded", (leaderboard: Player[]) => {
            setGameEndedLeaderboard(leaderboard);
            setWaitPhase(null);
            setCurrentQuestion(null);
        });

        // YENİ: Yönetici sayfayı kapatıp tekrar açtığında oyun durumunu (Kaldığı yeri) geri yükler
        connection.on("RestoreGameState", (fullState: any) => {
            const quiz = fullState.quiz || fullState.Quiz;
            const gameState = fullState.gameState || fullState.GameState;
            const currentQ = fullState.currentQuestion || fullState.CurrentQuestion;
            
            if (currentQ) {
                setIsLastQuestion((currentQ.currentIndex || currentQ.CurrentIndex) === (currentQ.totalQuestions || currentQ.TotalQuestions));
            }

            if (quiz) {
                const restoredPin = quiz.pin || quiz.Pin;
                setPin(restoredPin);
                sessionStorage.setItem("kahoot_host_pin", restoredPin);
                
                const playersList = quiz.players || quiz.Players || [];
                setPlayers(playersList.map((p: any) => p.nickname || p.Nickname));
            }

            if (gameState) {
                const phase = (gameState.phase !== undefined) ? gameState.phase.toString() : (gameState.Phase !== undefined ? gameState.Phase.toString() : "");
                const timeRem = gameState.timeRemaining || gameState.TimeRemaining;

                if (phase === "Question" || phase === "0") {
                    setCurrentQuestion(currentQ);
                    setTimeLeft(timeRem);
                    setWaitPhase(null);
                    setGameEndedLeaderboard(null);
                } else if (phase === "Transition" || phase === "1") {
                    setWaitPhase({ waitTime: timeRem, correctOptionId: null, leaderboard: [], allAnswered: false });
                    setTimeLeft(timeRem);
                    setCurrentQuestion(null);
                    setGameEndedLeaderboard(null);
                } else if (phase === "Ended" || phase === "2") {
                    const pinToUse = quiz ? (quiz.pin || quiz.Pin) : null;
                    if (pinToUse) {
                        connection.invoke("ShowLeaderboard", pinToUse).catch(console.error);
                    }
                }
            } else {
                setCurrentQuestion(null);
                setWaitPhase(null);
                setGameEndedLeaderboard(null);
            }
        });

        // YENİ: Skor tablosunu manuel tetikleme durumunda dinler
        connection.on("UpdateLeaderboard", (leaderboard: Player[]) => {
            setGameEndedLeaderboard(leaderboard);
            setWaitPhase(null);
            setCurrentQuestion(null);
            // YENİ DÜZELTME: Oyun bittiğinde "Mevcut Oyuna Dön" butonunun çıkmasını engeller
            sessionStorage.removeItem("kahoot_host_pin");
        });

        // YENİ: Yönetici lobiyi iptal ettiğinde ekranı sıfırlar
        connection.on("LobbyReset", () => {
            setPin(null);
            setPlayers([]);
            setCurrentQuestion(null);
            setWaitPhase(null);
            setGameEndedLeaderboard(null);
            sessionStorage.removeItem("kahoot_host_pin");
            setError("Lobi başarıyla iptal edildi.");
        });

        return () => {
            connection.off("PlayerJoined");
            connection.off("PlayerLeft");
            connection.off("GetReady");
            connection.off("ReceiveQuestion");
            connection.off("TimeUpdate");
            connection.off("WaitPhase");
            connection.off("WaitTimeUpdate");
            connection.off("GameEnded");
            connection.off("RedirectToNewGame");
            connection.off("RestoreGameState");
            connection.off("UpdateLeaderboard");
            connection.off("LobbyReset");
        };
    }, [connection]);

    const handleCreateGame = async () => {
        setIsLoading(true);
        setError(null);
        try {
            // YENİ: Eğer yöneticinin hafızada unuttuğu/yarım bıraktığı bir oyun varsa, yeni oyuna geçmeden önce o lobiyi dağıt.
            const oldPin = sessionStorage.getItem("kahoot_host_pin");
            if (oldPin && connection) {
                connection.invoke("ResetLobby", oldPin).catch(() => {});
            }

            let questions = [];
            
            // Eğer yönetici kendi sorularını Markdown olarak yazdıysa, önce onları ayrıştır
            if (markdown.trim()) {
                const parseRes = await fetch("http://localhost:5252/api/Quiz/parse-markdown", {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ markdownText: markdown })
                });
                if (!parseRes.ok) {
                    const errData = await parseRes.json();
                    throw new Error(errData.message || "Markdown ayrıştırma hatası");
                }
                questions = await parseRes.json();
            }

            // 1. Backend'e oyunu kurma isteği at
            const createRes = await fetch("http://localhost:5252/api/Quiz/create", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ title: "React Kahoot Quiz", questions })
            });

            if (!createRes.ok) throw new Error("Oyun kurulamadı");

            const data = await createRes.json();
            
            // Backend'den gelen verilerin büyük/küçük harf (CamelCase vs PascalCase) güvenliğini sağla
            const generatedPin = data.pin || data.Pin;
            const generatedToken = data.token || data.Token;

            if (!generatedToken) {
                throw new Error("Sunucudan yetkilendirme anahtarı (Token) alınamadı!");
            }

            // 2. Yöneticinin (Host) JWT yetkilendirme Token'ını kaydet
            sessionStorage.setItem("kahoot_host_token", generatedToken);
            sessionStorage.setItem("kahoot_host_pin", generatedPin);
            
            // 3. Token'ın SignalR'a yansıması için bağlantıyı durdur-başlat ve gruba katıl
            if (connection) {
                await connection.stop();
                await connection.start();
                await connection.invoke("JoinAsManager", generatedPin);
            }

            // HER ŞEY BAŞARILI OLURSA EKRANI LOBİYE ÇEVİR
            setPin(generatedPin);
        } catch (err: any) {
            console.error("Oyun Kurma Hatası:", err);
            setError(err.message);
        } finally {
            setIsLoading(false);
        }
    };

    // OYUN BİTTİ EKRANI (GAME ENDED)
    if (gameEndedLeaderboard) {
        return (
            <div className="container mt-5 text-center">
                <div className="card shadow-lg border-0">
                    <div className="card-body py-5">
                        <h1 className="display-1 fw-bold text-warning mb-4">🏆 Oyun Bitti! 🏆</h1>
                        <h3 className="mb-5 text-muted">İşte Şampiyonlar:</h3>
                        <div className="bg-light rounded-4 p-4 mx-auto mb-4" style={{ maxWidth: '600px' }}>
                            <ul className="list-group list-group-flush fs-4 fw-bold text-start">
                                {gameEndedLeaderboard.map((p, index) => (
                                    <li key={p.id} className="list-group-item d-flex justify-content-between align-items-center">
                                        <span>
                                            {index === 0 ? "👑 1. " : index === 1 ? "🥈 2. " : index === 2 ? "🥉 3. " : `${index + 1}. `} 
                                            {p.nickname}
                                        </span>
                                        <span className="badge bg-danger rounded-pill">{p.score} Puan</span>
                                    </li>
                                ))}
                            </ul>
                        </div>
                        <div className="d-flex justify-content-center gap-3 mt-4">
                            <button 
                                className="btn btn-dark btn-lg px-5 py-3 fs-4 shadow" 
                                onClick={async () => {
                                    try {
                                        await connection?.invoke("PlayAgain", pin);
                                    } catch(err) {
                                        console.error("PlayAgain Tetikleme Hatası:", err);
                                    }
                                }}
                            >
                                🔄 Yeniden Oyna
                            </button>
                            <button 
                                className="btn btn-danger btn-lg px-5 py-3 fs-4 shadow" 
                                onClick={() => {
                                    if (window.confirm("Lobiyi dağıtmak istediğinize emin misiniz? Tüm oyuncular ana ekrana gönderilecek.")) {
                                        connection?.invoke("ResetLobby", pin);
                                    }
                                }}
                            >
                                ❌ Lobiyi Dağıt
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
                <h1 className="display-1 fw-bold text-primary mb-4" style={{ fontSize: '5rem' }}>Hazır Ol!</h1>
                <div className="rounded-circle bg-primary text-white d-flex align-items-center justify-content-center shadow-lg" style={{ width: '200px', height: '200px', fontSize: '6rem', fontWeight: '800' }}>
                    {readyCountdown}
                </div>
            </div>
        );
    }

    // BEKLEME / ARA GEÇİŞ EKRANI (WAIT PHASE)
    if (waitPhase) {
        return (
            <div className="container mt-5 text-center">
                <div className="card shadow-lg border-0">
                    <div className="card-body py-5">
                        <h1 className="display-1 fw-bold text-info mb-4">Süre Doldu!</h1>
                        <h3 className="mb-4 text-muted">{isLastQuestion ? "Sonuçlar hesaplanıyor, şampiyonlar belirleniyor..." : "Sıradaki soruya geçiliyor..."}</h3>
                        
                        <div className="bg-light rounded-4 p-4 mx-auto mb-4" style={{ maxWidth: '600px' }}>
                            {waitPhase.leaderboard && waitPhase.leaderboard.length > 0 ? (
                                <ul className="list-group list-group-flush fs-4 fw-bold text-start">
                                    {waitPhase.leaderboard.map((p, index) => (
                                        <li key={p.id} className="list-group-item d-flex justify-content-between align-items-center">
                                            <span>
                                                {index === 0 ? "🥇 " : index === 1 ? "🥈 " : index === 2 ? "🥉 " : `${index + 1}. `} 
                                                {p.nickname}
                                            </span>
                                            <span className="badge bg-primary rounded-pill">{p.score} Puan</span>
                                        </li>
                                    ))}
                                </ul>
                            ) : (
                                <p className="text-muted fs-5 mb-0">Henüz puan alan oyuncu yok.</p>
                            )}
                        </div>

                        <div className="d-flex align-items-center justify-content-center gap-3">
                            <span className="fs-4 text-muted">{isLastQuestion ? "Büyük final için son:" : "Sıradaki soru için son:"}</span>
                            <div className="d-inline-block rounded-circle bg-info text-white d-flex align-items-center justify-content-center shadow" style={{ width: '80px', height: '80px', fontSize: '2.5rem' }}>
                                {timeLeft}
                            </div>
                        </div>

                        <div className="mt-5">
                            <button 
                                className="btn btn-outline-danger fw-bold px-4"
                                onClick={() => {
                                    if (window.confirm("Oyunu erken bitirmek istediğinize emin misiniz? (Şu anki puanlarla final tablosu gösterilecek)")) {
                                        connection?.invoke("EndGame", pin);
                                    }
                                }}
                            >
                                🛑 Oyunu Erken Bitir
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        );
    }

    // OYUN BAŞLADIYSA (SORU EKRANI)
    if (currentQuestion) {
        return (
            <div className="container mt-5 text-center">
                <div className="card shadow-lg border-0">
                    <div className="card-body py-4">
                        <div className="d-flex justify-content-between align-items-center mb-4">
                            <h4 className="text-primary fw-bold mb-0">Soru {currentQuestion.currentIndex} / {currentQuestion.totalQuestions}</h4>
                            <button 
                                className="btn btn-danger fw-bold shadow-sm"
                                onClick={() => {
                                    if (window.confirm("Oyunu erken bitirmek istediğinize emin misiniz? (Şu anki puanlarla final tablosu gösterilecek)")) {
                                        connection?.invoke("EndGame", pin);
                                    }
                                }}
                            >
                                🛑 Erken Bitir
                            </button>
                        </div>
                        <h1 className="display-3 fw-bold my-4 text-dark">{currentQuestion.text}</h1>
                        
                        <div className="mb-5">
                            <div className="d-inline-block rounded-circle bg-danger text-white d-flex align-items-center justify-content-center mx-auto shadow" style={{ width: '120px', height: '120px', fontSize: '3rem' }}>
                                {timeLeft}
                            </div>
                        </div>
                        
                        <div className="row g-4 mt-2 px-4">
                            {currentQuestion.options.map((opt, i) => {
                                const colors = ['bg-danger text-white', 'bg-primary text-white', 'bg-warning text-dark', 'bg-success text-white'];
                                return (
                                    <div key={opt.id} className="col-md-6">
                                        <div className={`card ${colors[i % 4]} border-0 option-card`} style={{ minHeight: '100px' }}>
                                            <div className="card-body d-flex align-items-center justify-content-center fs-3 option-text text-center">
                                                {opt.text}
                                            </div>
                                        </div>
                                    </div>
                                );
                            })}
                        </div>
                    </div>
                </div>
            </div>
        );
    }

    // OYUN KURULDUYSA (LOBİ EKRANI)
    if (pin) {
        return (
            <div className="container mt-5 text-center">
                <div className="card shadow-lg border-0">
                    <div className="card-body py-5">
                        <h2 className="text-muted fw-bold">Oyun PIN Kodu</h2>
                        <h1 className="display-1 fw-bold text-primary mb-4" style={{ fontSize: '6rem', letterSpacing: '5px' }}>{pin}</h1>
                        <p className="lead text-muted">Oyuncuların katılması bekleniyor... ({players.length} Kişi)</p>
                        
                        <div className="d-flex flex-wrap justify-content-center gap-2 mt-4 mb-5">
                            {players.map((p, i) => (
                                <span key={i} className="badge bg-primary text-white fs-5 py-2 px-3 shadow-sm">{p}</span>
                            ))}
                        </div>

                        <div className="d-flex justify-content-center gap-3 mt-4 mb-5">
                            <button 
                                className="btn btn-warning btn-lg fw-bold px-5 py-3 fs-4 shadow"
                                onClick={() => connection?.invoke("StartGame", pin)}
                                disabled={players.length === 0}
                            >
                                🚀 Oyunu Başlat
                            </button>
                            <button 
                                className="btn btn-danger btn-lg fw-bold px-5 py-3 fs-4 shadow"
                                onClick={() => {
                                    if (window.confirm("Lobiyi iptal etmek istediğinize emin misiniz?")) {
                                        connection?.invoke("ResetLobby", pin);
                                    }
                                }}
                            >
                                ❌ Lobiyi İptal Et
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        );
    }

    // OYUN KURULMADIYSA (FORM EKRANI)
    return (
        <div className="container mt-5">
            <h2 className="text-center fw-bold mb-4">👨‍🏫 Yönetici (Host) Ekranı</h2>
            
            <div className="row justify-content-center">
                <div className="col-md-8">
                    {error && <div className="alert alert-danger fw-bold">{error}</div>}
                    
                    <div className="card shadow-sm border-0">
                        <div className="card-body p-4">
                            <div className="d-flex justify-content-between align-items-center mb-3">
                                <p className="text-muted mb-0 text-start" style={{ fontSize: '0.9rem' }}>
                                    Sorularınızı <strong>Markdown</strong> formatında yazın veya dosya yükleyin. 
                                    Boş bırakırsanız örnek sorularla başlar.
                                </p>
                                <div>
                                    <input 
                                        type="file" 
                                        id="md-upload" 
                                        className="d-none" 
                                        accept=".md,.txt"
                                        onChange={(e) => {
                                            const file = e.target.files?.[0];
                                            if (!file) return;
                                            const reader = new FileReader();
                                            reader.onload = (event) => {
                                                if (event.target?.result) {
                                                    setMarkdown(event.target.result as string);
                                                }
                                            };
                                            reader.readAsText(file);
                                            e.target.value = ''; // Aynı dosyayı art arda seçebilmeyi sağlar
                                        }}
                                    />
                                    <button 
                                        type="button" 
                                        className="btn btn-sm btn-outline-primary fw-bold shadow-sm text-nowrap"
                                        onClick={() => document.getElementById('md-upload')?.click()}
                                    >
                                        📁 Dosya Yükle
                                    </button>
                                </div>
                            </div>
                            <textarea 
                                className="form-control mb-4 text-start font-monospace" 
                                rows={8}
                                placeholder="# Soru: Türkiye'nin başkenti neresidir?&#10;Süre: 20&#10;- İstanbul&#10;- Ankara (*)&#10;- İzmir"
                                value={markdown}
                                onChange={(e) => setMarkdown(e.target.value)}
                            ></textarea>
                            
                            <button 
                                className="btn btn-primary btn-lg w-100 py-3 fw-bold fs-5 shadow-sm"
                                onClick={handleCreateGame}
                                disabled={isLoading || !connection}
                            >
                                {isLoading ? "Oluşturuluyor..." : "✨ Yeni Oyun Kur"}
                            </button>

                            {sessionStorage.getItem("kahoot_host_pin") && (
                                <button 
                                    className="btn btn-success btn-lg w-100 py-3 fw-bold fs-5 shadow-sm mt-3"
                                    onClick={() => {
                                        setError(null);
                                        connection?.invoke("RejoinAsManager", sessionStorage.getItem("kahoot_host_pin")).catch(err => {
                                            console.error(err);
                                            setError("Oyuna dönülemedi. Oyun bitmiş veya iptal edilmiş olabilir.");
                                            sessionStorage.removeItem("kahoot_host_pin");
                                        });
                                    }}
                                    disabled={isLoading || !connection}
                                >
                                    🔄 Mevcut Oyuna Dön (PIN: {sessionStorage.getItem("kahoot_host_pin")})
                                </button>
                            )}
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
}