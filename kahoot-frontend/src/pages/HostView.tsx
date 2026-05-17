import { HubConnection } from '@microsoft/signalr';
import { Link } from 'react-router-dom';
import { useKahootHost } from '../hooks/useKahootHost';

interface Props {
    readonly connection: HubConnection | null;
}

export default function HostView({ connection }: Props) {
    const { 
        user, quizTitle, setQuizTitle, markdown, setMarkdown, pin, players, isLoading, error, setError, 
        inputMode, setInputMode, visualQuestions, setVisualQuestions, currentQText, setCurrentQText, 
        currentQTime, setCurrentQTime, currentQOptions, setCurrentQOptions, editingIndex, setEditingIndex, 
        requireGoogleAuth, setRequireGoogleAuth, setHasUnsavedChanges, currentQuestion, waitPhase, 
        gameEndedLeaderboard, timeLeft, isLastQuestion, isGettingReady, readyCountdown, answerStats, 
        handleAddVisualQuestion, handleEditVisualQuestion, handleDownloadMarkdown, handleSyncMarkdownToVisual, handleCreateGame, handleSaveToSystem 
    } = useKahootHost(connection);

    // JSX içerisindeki S3776 ve S2004 (Bilişsel Karmaşıklık ve Derin İç İçe Geçme) sorunlarını çözmek için
    const handlePlayAgain = async () => {
        try {
            await connection?.invoke("PlayAgain", pin);
        } catch(err) {
            console.error("PlayAgain Tetikleme Hatası:", err);
        }
    };

    const handleResetLobby = () => {
        if (globalThis.confirm("Lobiyi dağıtmak istediğinize emin misiniz? Tüm oyuncular ana ekrana gönderilecek.")) {
            connection?.invoke("ResetLobby", pin);
        }
    };

    const handleEndGame = () => {
        if (globalThis.confirm("Oyunu erken bitirmek istediğinize emin misiniz? (Şu anki puanlarla final tablosu gösterilecek)")) {
            connection?.invoke("EndGame", pin);
        }
    };

    const handleKickPlayer = (nickname: string) => {
        if (globalThis.confirm(`'${nickname}' adlı oyuncuyu atmak istediğinize emin misiniz?`)) {
            connection?.invoke("KickPlayer", pin, nickname);
        }
    };

    const handleDeleteVisualQuestion = (idx: number) => {
        setVisualQuestions(visualQuestions.filter((_, i) => i !== idx));
        if (editingIndex === idx) {
            setEditingIndex(null);
            setCurrentQText('');
            setCurrentQTime(20);
            setCurrentQOptions([{ text: '', isCorrect: true }, { text: '', isCorrect: false }, { text: '', isCorrect: false }, { text: '', isCorrect: false }]);
        }
    };

    const handleDeleteOption = (indexToRemove: number, wasCorrect: boolean) => {
        const newOpts = currentQOptions.filter((_, idx) => idx !== indexToRemove);
        if (wasCorrect && newOpts.length > 0) newOpts[0].isCorrect = true;
        setCurrentQOptions(newOpts);
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
                                        <span className="d-flex align-items-center gap-2">
                                            <span>{["👑 1. ", "🥈 2. ", "🥉 3. "][index] || `${index + 1}. `}</span>
                                            {p.avatarUrl && <img src={p.avatarUrl} alt="avatar" className="rounded-circle shadow-sm" style={{ width: '32px', height: '32px', objectFit: 'cover' }} referrerPolicy="no-referrer" />}
                                            <span>{p.nickname}</span>
                                        </span>
                                        <span className="badge bg-danger rounded-pill">{p.score} Puan</span>
                                    </li>
                                ))}
                            </ul>
                        </div>
                        <div className="d-flex justify-content-center gap-3 mt-4">
                            <button 
                                className="btn btn-dark btn-lg px-5 py-3 fs-4 shadow" 
                                onClick={handlePlayAgain}
                            >
                                🔄 Yeniden Oyna
                            </button>
                            <button 
                                className="btn btn-danger btn-lg px-5 py-3 fs-4 shadow" 
                                onClick={handleResetLobby}
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
                                            <span className="d-flex align-items-center gap-2">
                                                <span>{["🥇 ", "🥈 ", "🥉 "][index] || `${index + 1}. `}</span>
                                                {p.avatarUrl && <img src={p.avatarUrl} alt="avatar" className="rounded-circle shadow-sm" style={{ width: '32px', height: '32px', objectFit: 'cover' }} referrerPolicy="no-referrer" />}
                                                <span>{p.nickname}</span>
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
                                onClick={handleEndGame}
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
            <div className="container mt-4 text-center">
                <div className="card shadow-lg border-0">
                    <div className="card-body py-4">
                        <div className="d-flex justify-content-between align-items-center mb-3">
                            <h4 className="text-primary fw-bold mb-0">Soru {currentQuestion.currentIndex} / {currentQuestion.totalQuestions}</h4>
                            <div className="badge bg-secondary fs-5 shadow-sm px-4 py-2">
                                📝 Cevaplar: {answerStats.answered} / {answerStats.total}
                            </div>
                            <button 
                                className="btn btn-danger fw-bold shadow-sm"
                                onClick={handleEndGame}
                            >
                                🛑 Erken Bitir
                            </button>
                        </div>
                        <h1 className="display-4 fw-bold mb-3 text-dark">{currentQuestion.text}</h1>
                        
                        <div className="mb-3">
                            <div className="d-inline-block rounded-circle bg-danger text-white d-flex align-items-center justify-content-center mx-auto shadow" style={{ width: '100px', height: '100px', fontSize: '2.5rem' }}>
                                {timeLeft}
                            </div>
                        </div>
                        
                        <div className="row g-3 mt-1 px-3">
                            {currentQuestion.options.map((opt, i) => {
                                const colors = ['bg-danger text-white', 'bg-primary text-white', 'bg-warning text-dark', 'bg-success text-white'];
                                return (
                                    <div key={opt.id} className="col-md-6">
                                        <div className={`card ${colors[i % 4]} border-0 option-card`} style={{ minHeight: '90px' }}>
                                            <div className="card-body d-flex align-items-center justify-content-center fs-4 option-text text-center">
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
        const joinUrl = `${globalThis.location.origin}/#/player?pin=${pin}`;
        const qrCodeUrl = `https://api.qrserver.com/v1/create-qr-code/?size=200x200&data=${encodeURIComponent(joinUrl)}`;

        return (
            <div className="container mt-5 text-center">
                <div className="card shadow-lg border-0">
                    <div className="card-body py-5">
                        <div className="row align-items-center mb-4">
                            <div className="col-md-4 d-flex flex-column align-items-center justify-content-center">
                                <p className="text-muted fw-bold mb-2">Telefonla QR Okut</p>
                                <img src={qrCodeUrl} alt="QR Code" className="img-fluid rounded-4 shadow-sm border border-2 border-primary p-2 bg-white" style={{ width: '180px', height: '180px' }} />
                            </div>
                            <div className="col-md-8 text-md-start text-center mt-4 mt-md-0">
                                <h2 className="text-muted fw-bold">Oyun PIN Kodu</h2>
                                <h1 className="display-1 fw-bold text-primary" style={{ fontSize: '7rem', letterSpacing: '5px' }}>{pin}</h1>
                            </div>
                        </div>
                        <p className="lead text-muted fw-bold border-top pt-4">Oyuncuların katılması bekleniyor... ({players.length} Kişi)</p>

                        <div className="d-flex flex-wrap justify-content-center gap-2 mt-4 mb-5">
                            {players.map((p) => (
                                <span key={p.nickname} className="badge bg-primary text-white fs-5 py-2 px-3 shadow-sm d-flex align-items-center gap-2">
                                    {p.avatarUrl && <img src={p.avatarUrl} alt="avatar" className="rounded-circle bg-white" style={{ width: '28px', height: '28px', objectFit: 'cover' }} referrerPolicy="no-referrer" />}
                                    {p.nickname}
                                    <button 
                                        type="button" 
                                        className="btn-close btn-close-white ms-2" 
                                        style={{ fontSize: '0.65rem' }} 
                                        aria-label="Kick"
                                        onClick={() => handleKickPlayer(p.nickname)}
                                    ></button>
                                </span>
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
                                onClick={handleResetLobby}
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
            <div className="row justify-content-center">
                <div className="col-md-8">
                    <div className="d-flex justify-content-between align-items-center mb-4">
                        <Link to="/" className="btn btn-sm btn-outline-secondary fw-bold shadow-sm">
                            ⬅️ Ana Sayfa
                        </Link>
                        <h2 className="fw-bold mb-0 text-dark">👨‍🏫 Yönetici Ekranı</h2>
                        <div style={{ width: '100px' }}></div> {/* Başlığı ortalamak için boşluk */}
                    </div>
                
                {/* YENİ EKLENEN: Oyun Başlığı Alanı */}
                <div className="card shadow-sm border-0 mb-3 bg-white">
                    <div className="card-body p-3">
                        <label htmlFor="quizTitleInput" className="fw-bold text-dark mb-2">Oyun Başlığı (İsteğe Bağlı)</label>
                        <input id="quizTitleInput" type="text" className="form-control form-control-lg fw-bold" placeholder="Örn: Vize Hazırlık Testi" value={quizTitle} onChange={(e) => {
                            setQuizTitle(e.target.value);
                            setHasUnsavedChanges(true);
                            localStorage.setItem("kahoot_draft_title", e.target.value);
                        }} />
                    </div>
                </div>
                    
                    {error && <div className="alert alert-danger fw-bold">{error}</div>}
                    
                    {/* YENİ EKLENEN: Üst Kısma Taşınan Oyun Kurma Butonları */}
                    <div className="card shadow-sm border-0 mb-4 bg-primary text-white">
                        <div className="card-body p-4 d-flex flex-column flex-md-row justify-content-between align-items-center gap-3">
                            <div className="text-md-start text-center">
                                <h4 className="fw-bold mb-1">Yeni Bir Oyun Başlat</h4>
                                <p className="mb-0 opacity-75 small">Sorularınızı hazırladıktan sonra oyunu kurun.</p>
                            <div className="mt-3 text-start">
                                <div className="d-inline-block bg-white text-dark py-2 px-3 rounded-pill shadow-sm border">
                                    <div className="form-check form-switch mb-0">
                                        <input 
                                            className="form-check-input" 
                                            type="checkbox" 
                                            id="googleAuthSwitch" 
                                            checked={requireGoogleAuth} 
                                            onChange={(e) => setRequireGoogleAuth(e.target.checked)} 
                                            style={{ cursor: 'pointer', transform: 'scale(1.2)', marginTop: '0.2rem' }}
                                        />
                                        <label className="form-check-label fw-bold ms-2" htmlFor="googleAuthSwitch" style={{ cursor: 'pointer', fontSize: '0.9rem' }}>
                                            🔒 Sadece Google hesabı olanlar katılabilsin
                                        </label>
                                    </div>
                                </div>
                            </div>
                            </div>
                            <div className="d-flex flex-wrap justify-content-center gap-2">
                                {sessionStorage.getItem("kahoot_host_pin") && (
                                    <button 
                                        className="btn btn-success fw-bold shadow-sm"
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
                                        🔄 Mevcut Oyuna Dön
                                    </button>
                                )}
                                <button 
                                    className="btn btn-warning text-dark fw-bold px-4 shadow-sm fs-5"
                                    onClick={handleCreateGame}
                                    disabled={isLoading || !connection}
                                >
                                    {isLoading ? "Başlatılıyor..." : "🚀 Oyunu Başlat"}
                                </button>
                            </div>
                        </div>
                    </div>

                    <div className="card shadow-sm border-0">
                        <div className="card-body p-4">
                            
                            {/* SEKMELER (TABS) */}
                            <ul className="nav nav-tabs mb-4">
                                <li className="nav-item">
                                    <button className={`nav-link fw-bold ${inputMode === 'visual' ? 'active' : 'text-muted'}`} onClick={() => { setInputMode('visual'); setError(null); }}>🎨 Soru Oluşturucu</button>
                                </li>
                                <li className="nav-item">
                                    <button className={`nav-link fw-bold ${inputMode === 'markdown' ? 'active' : 'text-muted'}`} onClick={() => { setInputMode('markdown'); setError(null); }}>📝 Markdown (İleri Düzey)</button>
                                </li>
                            </ul>

                            {inputMode === 'visual' ? (
                                <div className="text-start">
                                    <p className="text-muted mb-3 small">Sorularınızı aşağıdaki formu kullanarak kolayca oluşturun. Hiç soru eklemezseniz varsayılan örnek sorularla başlar.</p>
                                    
                                    {visualQuestions.length > 0 && (
                                        <div className="mb-4">
                                            <div className="d-flex justify-content-between align-items-center mb-2">
                                                <h6 className="fw-bold text-primary mb-0">Hazır Sorular ({visualQuestions.length})</h6>
                                                <button className="btn btn-sm btn-outline-secondary fw-bold" onClick={handleDownloadMarkdown}>⬇️ .md İndir</button>
                                            </div>
                                            <ul className="list-group shadow-sm">
                                                {visualQuestions.map((vq, idx) => (
                                                    <li key={`vq-${idx}`} className="list-group-item d-flex justify-content-between align-items-center bg-light">
                                                        <span className="text-truncate fw-bold text-dark me-3" style={{maxWidth: '65%'}}>{idx + 1}. {vq.text}</span>
                                                        <div className="d-flex gap-2">
                                                            <button className="btn btn-sm btn-outline-primary fw-bold" onClick={() => handleEditVisualQuestion(idx)}>Düzenle</button>
                                                            <button className="btn btn-sm btn-outline-danger fw-bold" onClick={() => handleDeleteVisualQuestion(idx)}>Sil</button>
                                                        </div>
                                                    </li>
                                                ))}
                                            </ul>
                                        </div>
                                    )}

                                    <div className={`bg-light p-3 rounded-4 border shadow-sm mb-4 ${editingIndex !== null ? 'border-primary shadow' : ''}`}>
                                        <h5 className="fw-bold mb-3 text-dark">{editingIndex !== null ? 'Soruyu Düzenle' : 'Yeni Soru Ekle'}</h5>
                                        <input type="text" className="form-control mb-3 fw-bold border-0 shadow-sm" placeholder="Soru metnini yazın..." value={currentQText} onChange={e => setCurrentQText(e.target.value)} />
                                        
                                        <div className="row g-2 mb-3">
                                            {currentQOptions.map((opt, i) => (
                                                <div key={`opt-${i}`} className="col-md-6">
                                                    <div className="input-group shadow-sm rounded-3 overflow-hidden">
                                                        <div className={`input-group-text border-0 ${opt.isCorrect ? 'bg-success text-white' : 'bg-white'}`}>
                                                            <input className="form-check-input mt-0" type="radio" name="correctOption" checked={opt.isCorrect} onChange={() => {
                                                                const newOpts = [...currentQOptions];
                                                                newOpts.forEach(o => o.isCorrect = false);
                                                                newOpts[i].isCorrect = true;
                                                                setCurrentQOptions(newOpts);
                                                            }} />
                                                        </div>
                                                        <input type="text" className={`form-control border-0 fw-bold ${opt.isCorrect ? 'text-success' : 'text-dark'}`} placeholder={`${i+1}. Şık (Cevap)`} value={opt.text} onChange={e => {
                                                            const newOpts = [...currentQOptions];
                                                            newOpts[i].text = e.target.value;
                                                            setCurrentQOptions(newOpts);
                                                        }} />
                                                        
                                                        {currentQOptions.length > 2 && (
                                                            <button 
                                                                type="button" 
                                                                className="btn btn-light text-danger border-0 px-3 fw-bold" 
                                                                title="Şıkkı Sil"
                                                                onClick={() => handleDeleteOption(i, opt.isCorrect)}
                                                            >
                                                                ✖
                                                            </button>
                                                        )}
                                                    </div>
                                                </div>
                                            ))}
                                            
                                            {currentQOptions.length < 8 && (
                                                <div className="col-12 mt-2">
                                                    <button 
                                                        type="button" 
                                                        className="btn btn-sm btn-outline-secondary w-100 fw-bold py-2" 
                                                        style={{ borderStyle: 'dashed', borderWidth: '2px' }}
                                                        onClick={() => setCurrentQOptions([...currentQOptions, { text: '', isCorrect: false }])}
                                                    >
                                                        ➕ Yeni Şık Ekle
                                                    </button>
                                                </div>
                                            )}
                                        </div>

                                        <div className="d-flex justify-content-between align-items-center mt-3 pt-3 border-top">
                                            <div className="d-flex align-items-center gap-2">
                                                <span className="fw-bold text-muted small">Süre (Saniye):</span>
                                                <select className="form-select form-select-sm fw-bold border-0 shadow-sm" style={{width: '90px'}} value={currentQTime} onChange={e => setCurrentQTime(Number(e.target.value))}>
                                                    <option value={10}>10</option>
                                                    <option value={20}>20</option>
                                                    <option value={30}>30</option>
                                                    <option value={60}>60</option>
                                                </select>
                                            </div>
                                            <div className="d-flex align-items-center gap-2">
                                                {editingIndex !== null && (
                                                    <button className="btn btn-sm btn-outline-secondary fw-bold px-3 shadow-sm" onClick={() => {
                                                        setEditingIndex(null);
                                                        setCurrentQText('');
                                                        setCurrentQTime(20);
                                                        setCurrentQOptions([{ text: '', isCorrect: true }, { text: '', isCorrect: false }, { text: '', isCorrect: false }, { text: '', isCorrect: false }]);
                                                    }}>İptal</button>
                                                )}
                                                <button className={`btn btn-sm ${editingIndex !== null ? 'btn-success' : 'btn-primary'} fw-bold px-4 shadow-sm`} onClick={handleAddVisualQuestion}>
                                                    {editingIndex !== null ? '💾 Soruyu Güncelle' : '➕ Soruyu Ekle'}
                                                </button>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            ) : (
                                <div>
                                    <div className="d-flex justify-content-between align-items-center mb-3">
                                        <p className="text-muted mb-0 text-start" style={{ fontSize: '0.9rem' }}>
                                            Sorularınızı <strong>Markdown</strong> formatında yazın veya dosya yükleyin. 
                                        </p>
                                        <div className="d-flex gap-2">
                                            <button type="button" className="btn btn-sm btn-outline-success fw-bold shadow-sm text-nowrap" onClick={handleSyncMarkdownToVisual} disabled={isLoading || !markdown.trim()}>
                                                🔄 Forma Aktar
                                            </button>
                                            <input type="file" id="md-upload" className="d-none" accept=".md,.txt" onChange={(e) => {
                                                const file = e.target.files?.[0];
                                                if (!file) return;
                                                const reader = new FileReader();
                                                reader.onload = (event) => { if (event.target?.result) setMarkdown(event.target.result as string); };
                                                reader.readAsText(file);
                                                e.target.value = '';
                                            }} />
                                            <button type="button" className="btn btn-sm btn-outline-primary fw-bold shadow-sm text-nowrap" onClick={() => document.getElementById('md-upload')?.click()}>📁 Dosya Yükle</button>
                                        </div>
                                    </div>
                                    <textarea className="form-control mb-4 text-start font-monospace shadow-sm" rows={8} placeholder="# Soru: Türkiye'nin başkenti neresidir?&#10;Süre: 20&#10;- İstanbul&#10;- Ankara (*)&#10;- İzmir" value={markdown} onChange={(e) => setMarkdown(e.target.value)}></textarea>
                                </div>
                            )}

                            {/* YENİ: Sadece giriş yapmış kullanıcılar soru bankasına kaydedebilir */}
                            {user ? (
                                <div className="mt-4 pt-4 border-top d-flex flex-column flex-md-row justify-content-between align-items-center gap-3">
                                    <span className="text-muted small fw-bold">💡 Sorularınızı sisteme kaydederek Soru Bankası oluşturabilirsiniz.</span>
                                    <button 
                                        className="btn btn-outline-primary btn-lg fw-bold px-5 shadow-sm"
                                        onClick={handleSaveToSystem}
                                        disabled={isLoading}
                                    >
                                        💾 Sisteme Kaydet
                                    </button>
                                </div>
                            ) : (
                                <div className="mt-4 pt-4 border-top text-center">
                                    <span className="text-muted small fw-bold">💡 Soru bankası oluşturmak ve sorularınızı sisteme kaydetmek için lütfen ana sayfadan giriş yapın.</span>
                                </div>
                            )}
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
}