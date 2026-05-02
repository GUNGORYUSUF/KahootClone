import { useState, useEffect } from 'react';
import { HubConnection } from '@microsoft/signalr';
import { Link } from 'react-router-dom';
import type { QuestionPacket, WaitPhasePayload, Player } from '../types/index';
import { useAuth } from '../context/AuthContext';

interface Props {
    connection: HubConnection | null;
}

export default function HostView({ connection }: Props) {
    const { user, token } = useAuth(); // YENİ: Oturum açmış kullanıcının kimliğini ve bilgilerini al
    const [quizTitle, setQuizTitle] = useState(() => localStorage.getItem("kahoot_draft_title") || '');
    const [markdown, setMarkdown] = useState(() => localStorage.getItem("kahoot_draft_markdown") || '');
    const [pin, setPin] = useState<string | null>(null);
    const [players, setPlayers] = useState<any[]>([]);
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    // YENİ: Görsel Soru Oluşturucu State'leri
    const [inputMode, setInputMode] = useState<'visual' | 'markdown'>('visual');
    const [visualQuestions, setVisualQuestions] = useState<any[]>(() => {
        const saved = localStorage.getItem("kahoot_draft_visual");
        return saved ? JSON.parse(saved) : [];
    });
    const [currentQText, setCurrentQText] = useState('');
    const [currentQTime, setCurrentQTime] = useState<number>(20);
    const [currentQOptions, setCurrentQOptions] = useState([
        { text: '', isCorrect: true },
        { text: '', isCorrect: false },
        { text: '', isCorrect: false },
        { text: '', isCorrect: false }
    ]);
    const [editingIndex, setEditingIndex] = useState<number | null>(null);
    const [requireGoogleAuth, setRequireGoogleAuth] = useState(false); // YENİ: Google Login zorunluluğu

    // YENİ: Değişiklik yapılıp yapılmadığını takip et
    const [hasUnsavedChanges, setHasUnsavedChanges] = useState(() => localStorage.getItem("kahoot_unsaved") === "true");
    useEffect(() => { localStorage.setItem("kahoot_unsaved", hasUnsavedChanges ? "true" : "false"); }, [hasUnsavedChanges]);


    // YENİ: Soru ve süre state'leri
    const [currentQuestion, setCurrentQuestion] = useState<QuestionPacket | null>(null);
    const [waitPhase, setWaitPhase] = useState<WaitPhasePayload | null>(null);
    const [gameEndedLeaderboard, setGameEndedLeaderboard] = useState<Player[] | null>(null);
    const [timeLeft, setTimeLeft] = useState<number>(0);
    const [isLastQuestion, setIsLastQuestion] = useState<boolean>(false);
    const [isGettingReady, setIsGettingReady] = useState<boolean>(false);
    const [readyCountdown, setReadyCountdown] = useState<number>(3);
    const [answerStats, setAnswerStats] = useState({ answered: 0, total: 0 });

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
        connection.on("PlayerJoined", (playerObj: any) => {
            const nick = typeof playerObj === 'string' ? playerObj : (playerObj.nickname || playerObj.Nickname);
            const avatar = typeof playerObj === 'string' ? undefined : (playerObj.avatarUrl || playerObj.AvatarUrl);
            console.log("🟢 Oyuncu katıldı:", nick);
            setPlayers(prev => {
                if (prev.some(p => p.nickname === nick)) return prev;
                return [...prev, { nickname: nick, avatarUrl: avatar }];
            });
        });

        connection.on("PlayerLeft", (nickname: string) => {
            setPlayers(prev => prev.filter(p => p.nickname !== nickname));
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

        // YENİ: Anlık cevap sayacı güncellemelerini dinle
        connection.on("UpdateAnswerCount", (payload: any) => {
            const answered = payload.answeredCount ?? payload.AnsweredCount ?? 0;
            const total = payload.totalCount ?? payload.TotalCount ?? 0;
            setAnswerStats({ answered, total });
        });

        // YENİ: Soru geldiğinde ekranı değiştir
        connection.on("ReceiveQuestion", (question: QuestionPacket) => {
            setCurrentQuestion(question);
            setWaitPhase(null); // Soru geldiğinde bekleme ekranını kapat
            setIsGettingReady(false); // Hazırlık ekranını kapat
            setTimeLeft(question.timeLimit);
            setIsLastQuestion(question.currentIndex === question.totalQuestions);
            setAnswerStats({ answered: 0, total: question.totalPlayers });
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
            
            const ansCount = fullState.answeredCount ?? fullState.AnsweredCount ?? 0;
            const totCount = fullState.totalActiveCount ?? fullState.TotalActiveCount ?? 0;
            setAnswerStats({ answered: ansCount, total: totCount });
            
            if (currentQ) {
                setIsLastQuestion((currentQ.currentIndex || currentQ.CurrentIndex) === (currentQ.totalQuestions || currentQ.TotalQuestions));
            }

            if (quiz) {
                const restoredPin = quiz.pin || quiz.Pin;
                setPin(restoredPin);
                sessionStorage.setItem("kahoot_host_pin", restoredPin);
                
                const playersList = quiz.players || quiz.Players || [];
                setPlayers(playersList.map((p: any) => ({ nickname: p.nickname || p.Nickname, avatarUrl: p.avatarUrl || p.AvatarUrl })));
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
            connection.off("UpdateAnswerCount");
            connection.off("GameEnded");
            connection.off("RedirectToNewGame");
            connection.off("RestoreGameState");
            connection.off("UpdateLeaderboard");
            connection.off("LobbyReset");
        };
    }, [connection]);

    // YENİ: Soru oluşturucudan soru ekleme fonksiyonu
    const handleAddVisualQuestion = () => {
        if (!currentQText.trim()) {
            setError("Soru metni boş olamaz.");
            return;
        }
        if (currentQOptions.some(o => !o.text.trim())) {
            setError("Tüm şıkları eksiksiz doldurmalısınız.");
            return;
        }
        
        const newQuestion = {
            text: currentQText,
            timeLimitInSeconds: currentQTime,
            options: currentQOptions.map(o => ({ ...o }))
        };

        if (editingIndex !== null) {
            const updated = [...visualQuestions];
            updated[editingIndex] = newQuestion;
            setVisualQuestions(updated);
            setEditingIndex(null);
        } else {
            setVisualQuestions([...visualQuestions, newQuestion]);
        }
        setHasUnsavedChanges(true); // Değişiklik yapıldı
        
        // Formu temizle
        setCurrentQText('');
        setCurrentQTime(20);
        setCurrentQOptions([{ text: '', isCorrect: true }, { text: '', isCorrect: false }, { text: '', isCorrect: false }, { text: '', isCorrect: false }]);
        setError(null);
    };

    const handleEditVisualQuestion = (idx: number) => {
        const q = visualQuestions[idx];
        setCurrentQText(q.text);
        setCurrentQTime(q.timeLimitInSeconds || 20);
        setCurrentQOptions(q.options.map((o: any) => ({ ...o })));
        setEditingIndex(idx);
    };

    const handleDownloadMarkdown = () => {
        if (visualQuestions.length === 0) return;
        let md = "";
        visualQuestions.forEach(q => {
            md += `# Soru: ${q.text}\nSüre: ${q.timeLimitInSeconds || 20}\n`;
            q.options.forEach((o: any) => {
                md += `- ${o.text} ${o.isCorrect ? '(*)' : ''}\n`;
            });
            md += "\n";
        });
        const blob = new Blob([md], { type: 'text/markdown' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = 'kahoot_sorulari.md';
        a.click();
        URL.revokeObjectURL(url);
    };

    // YENİ: Markdown sekmesindeki metni ayrıştırıp Soru Oluşturucuya (Visual Builder) aktarır
    const handleSyncMarkdownToVisual = async () => {
        if (!markdown.trim()) return;
        setIsLoading(true);
        setError(null);
        try {
            // YENİ: Markdown Hata Yakalama (Pre-Validation)
            const cleanLines = markdown.split('\n').map(l => l.trim()).filter(l => l.length > 0);
            if (cleanLines.length > 0 && !cleanLines[0].startsWith('#')) {
                throw new Error("Markdown formatı hatalı: Metin '#' işareti ile (soru başlığıyla) başlamalıdır. (Örn: '# Soru 1:')");
            }
            
            // Global Kontrol: "1.", "2)", "Q:", "Pregunta:" gibi soru başlıkları '#' olmadan yazılmışsa yakala
            if (/^[ \t]*(?!Süre|Time|Timer|Duration|Zaman)([A-Za-zğüşıöçĞÜŞİÖÇ]+[ \t]*:|\d+[\.\)])/im.test(markdown)) {
                throw new Error("Bazı soruların başında '#' işareti eksik olabilir. Lütfen her sorunun başına '#' ekleyin.");
            }

            const parseRes = await fetch("http://localhost:5252/api/Quiz/parse-markdown", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ markdownText: markdown })
            });
            
            if (!parseRes.ok) {
                const errData = await parseRes.json();
                throw new Error(errData.message || "Markdown ayrıştırma hatası");
            }
            
            const parsedQuestions = await parseRes.json();

            // YENİ: Ayrıştırılan Soruları Doğrulama (Post-Validation)
            if (parsedQuestions.length === 0) {
                throw new Error("Geçerli bir soru bulunamadı. Lütfen Markdown formatını kontrol edin.");
            }

            // Backend'den gelen C# nesnelerini Frontend State (Visual) yapısına güvenle eşle
            const mappedQuestions = parsedQuestions.map((q: any, index: number) => {
                const text = q.text || q.Text;
                const opts = q.options || q.Options || [];
                
                if (opts.length < 2) {
                    throw new Error(`${index + 1}. Soru ("${text}") için en az 2 şık eklemelisiniz. (Eğer yeni bir soru eklediyseniz başına '#' koymayı unutmuş olabilirsiniz)`);
                }
                if (!opts.some((o: any) => o.isCorrect || o.IsCorrect)) {
                    throw new Error(`${index + 1}. Soru ("${text}") için doğru cevap seçilmemiş. Doğru şıkkın sonuna (*) ekleyin.`);
                }

                return {
                    text: text,
                    timeLimitInSeconds: q.timeLimitInSeconds || q.TimeLimitInSeconds || 20,
                    options: opts.map((o: any) => ({
                        text: o.text || o.Text,
                        isCorrect: o.isCorrect || o.IsCorrect || false
                    }))
                };
            });

            setVisualQuestions(mappedQuestions); // Görsel listeyi güncelle
            setInputMode('visual'); // Kullanıcıyı otomatik olarak Görsel sekmeye kaydır
            setHasUnsavedChanges(true); // Değişiklik yapıldı
        } catch (err: any) {
            console.error("Markdown Aktarma Hatası:", err);
            setError(err.message);
        } finally {
            setIsLoading(false);
        }
    };

    const handleCreateGame = async () => {
        setIsLoading(true);
        setError(null);
        try {
            if (hasUnsavedChanges) {
                alert("Lütfen oyunu başlatmadan önce sorularınızı '💾 Sisteme Kaydet' butonu ile kaydedin veya güncelleyin.");
                return;
            }

            // YENİ: Eğer yöneticinin hafızada unuttuğu/yarım bıraktığı bir oyun varsa, yeni oyuna geçmeden önce o lobiyi dağıt.
            const oldPin = sessionStorage.getItem("kahoot_host_pin");
            if (oldPin && connection) {
                connection.invoke("ResetLobby", oldPin).catch(() => {});
            }

            let questions: any[] = [];
            
            if (inputMode === 'markdown') {
                if (markdown.trim()) {
                    // YENİ: Markdown Hata Yakalama (Pre-Validation)
                    const cleanLines = markdown.split('\n').map(l => l.trim()).filter(l => l.length > 0);
                    if (cleanLines.length > 0 && !cleanLines[0].startsWith('#')) {
                        throw new Error("Markdown formatı hatalı: Metin '#' işareti ile (soru başlığıyla) başlamalıdır. (Örn: '# Soru 1:')");
                    }
                    
                    // Global Kontrol: "1.", "2)", "Q:", "Pregunta:" gibi soru başlıkları '#' olmadan yazılmışsa yakala
                    if (/^[ \t]*(?!Süre|Time|Timer|Duration|Zaman)([A-Za-zğüşıöçĞÜŞİÖÇ]+[ \t]*:|\d+[\.\)])/im.test(markdown)) {
                        throw new Error("Bazı soruların başında '#' işareti eksik olabilir. Lütfen her sorunun başına '#' ekleyin.");
                    }

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

                    // YENİ: Ayrıştırılan Soruları Doğrulama (Post-Validation)
                    if (questions.length === 0) {
                        throw new Error("Geçerli bir soru bulunamadı. Lütfen Markdown formatını kontrol edin.");
                    }
                    questions.forEach((q: any, index: number) => {
                        const text = q.text || q.Text;
                        const opts = q.options || q.Options || [];
                        if (opts.length < 2) {
                            throw new Error(`${index + 1}. Soru ("${text}") için en az 2 şık eklemelisiniz. (Eğer yeni bir soru eklediyseniz başına '#' koymayı unutmuş olabilirsiniz)`);
                        }
                        if (!opts.some((o: any) => o.isCorrect || o.IsCorrect)) {
                            throw new Error(`${index + 1}. Soru ("${text}") için doğru cevap seçilmemiş. Lütfen doğru şıkkın sonuna (*) ekleyin.`);
                        }
                    });
                }
            } else {
                questions = visualQuestions;
            }

            // YENİ: Guid çakışmalarını önlemek için eski ID'leri temizle
            const cleanQuestions = questions.map((q: any) => ({
                text: q.text || q.Text,
                timeLimitInSeconds: q.timeLimitInSeconds || q.TimeLimitInSeconds || 20,
                options: (q.options || q.Options || []).map((o: any) => ({
                    text: o.text || o.Text,
                    isCorrect: o.isCorrect || o.IsCorrect || false
                }))
            }));

            // YENİ: Sunucuya gönderilecek başlıkları ayarla, eğer kullanıcı giriş yapmışsa Token'ını da ekle
            const headers: Record<string, string> = { "Content-Type": "application/json" };
            if (token) {
                headers["Authorization"] = `Bearer ${token}`;
            }

            // 1. Backend'e oyunu kurma isteği at
            const createRes = await fetch("http://localhost:5252/api/Quiz/create", {
                method: "POST",
                headers: headers,
                body: JSON.stringify({ title: quizTitle.trim() || "Canlı Oyun", questions: cleanQuestions, requireGoogleAuth, isDraft: false })
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
            
            // YENİ: Oyun başarıyla kurulup lobi açıldıktan sonra tarayıcıdaki taslağı temizle
            localStorage.removeItem("kahoot_draft_visual");
            localStorage.removeItem("kahoot_draft_markdown");
            localStorage.removeItem("kahoot_draft_title");
            localStorage.removeItem("kahoot_editing_pin");
        } catch (err: any) {
            console.error("Oyun Kurma Hatası:", err);
            setError(err.message);
        } finally {
            setIsLoading(false);
        }
    };

    // YENİ: Soruları doğrudan Backend Veritabanına (Kendi Profiline) kaydeder
    const handleSaveToSystem = async () => {
        setIsLoading(true);
        setError(null);
        try {
            if (!quizTitle.trim()) {
                setError("Soru Bankasına kaydetmek için lütfen bir 'Oyun Başlığı' girin.");
                return;
            }

            let questions: any[] = [];
            
            if (inputMode === 'markdown') {
                if (markdown.trim()) {
                    const cleanLines = markdown.split('\n').map(l => l.trim()).filter(l => l.length > 0);
                    if (cleanLines.length > 0 && !cleanLines[0].startsWith('#')) {
                        throw new Error("Markdown formatı hatalı: Metin '#' işareti ile başlamalıdır.");
                    }
                    if (/^[ \t]*(?!Süre|Time|Timer|Duration|Zaman)([A-Za-zğüşıöçĞÜŞİÖÇ]+[ \t]*:|\d+[\.\)])/im.test(markdown)) {
                        throw new Error("Bazı soruların başında '#' işareti eksik olabilir.");
                    }
                    const parseRes = await fetch("http://localhost:5252/api/Quiz/parse-markdown", {
                        method: "POST",
                        headers: { "Content-Type": "application/json" },
                        body: JSON.stringify({ markdownText: markdown })
                    });
                    if (!parseRes.ok) throw new Error("Markdown ayrıştırma hatası");
                    questions = await parseRes.json();
                    if (questions.length === 0) throw new Error("Geçerli bir soru bulunamadı.");
                }
            } else {
                questions = visualQuestions;
            }

            if (questions.length === 0) {
                throw new Error("Lütfen kaydedilecek sorular ekleyin.");
            }

            // YENİ: Guid çakışmalarını önlemek için eski ID'leri temizle
            const cleanQuestions = questions.map((q: any) => ({
                text: q.text || q.Text,
                timeLimitInSeconds: q.timeLimitInSeconds || q.TimeLimitInSeconds || 20,
                options: (q.options || q.Options || []).map((o: any) => ({
                    text: o.text || o.Text,
                    isCorrect: o.isCorrect || o.IsCorrect || false
                }))
            }));

            // Eğer daha önceden yüklenmiş bir taslağı (veya oyunu) düzenliyorsak, eski kaydı silip yenisini atalım
            const editingPin = localStorage.getItem("kahoot_editing_pin");
            if (editingPin && token) {
                try {
                    await fetch(`http://localhost:5252/api/Quiz/${editingPin}`, { method: "DELETE", headers: { "Authorization": `Bearer ${token}` } });
                } catch (e) { console.error("Eski taslak silinemedi:", e); }
            }

            const headers: Record<string, string> = { "Content-Type": "application/json" };
            if (token) headers["Authorization"] = `Bearer ${token}`;

            const createRes = await fetch("http://localhost:5252/api/Quiz/create", {
                method: "POST",
                headers: headers,
                body: JSON.stringify({ title: quizTitle.trim(), questions: cleanQuestions, requireGoogleAuth, isDraft: true })
            });

            if (!createRes.ok) throw new Error("Sisteme kaydedilemedi");

            const data = await createRes.json();
            localStorage.setItem("kahoot_editing_pin", data.pin || data.Pin); // Yeni pin ile eşleştir
            setHasUnsavedChanges(false); // Değişiklikler kaydedildi

            alert("Sorularınız Soru Bankasına başarıyla kaydedildi! Ana sayfadaki 'Kayıtlı Sorularım' menüsünden ulaşabilirsiniz.");
        } catch (err: any) {
            console.error("Kaydetme Hatası:", err);
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
                                        <span className="d-flex align-items-center gap-2">
                                            <span>{index === 0 ? "👑 1. " : index === 1 ? "🥈 2. " : index === 2 ? "🥉 3. " : `${index + 1}. `}</span>
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
                                            <span className="d-flex align-items-center gap-2">
                                                <span>{index === 0 ? "🥇 " : index === 1 ? "🥈 " : index === 2 ? "🥉 " : `${index + 1}. `}</span>
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
                                onClick={() => {
                                    if (window.confirm("Oyunu erken bitirmek istediğinize emin misiniz? (Şu anki puanlarla final tablosu gösterilecek)")) {
                                        connection?.invoke("EndGame", pin);
                                    }
                                }}
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
        const joinUrl = `${window.location.origin}/#/player?pin=${pin}`;
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
                            {players.map((p, i) => (
                                <span key={i} className="badge bg-primary text-white fs-5 py-2 px-3 shadow-sm d-flex align-items-center gap-2">
                                    {p.avatarUrl && <img src={p.avatarUrl} alt="avatar" className="rounded-circle bg-white" style={{ width: '28px', height: '28px', objectFit: 'cover' }} referrerPolicy="no-referrer" />}
                                    {p.nickname}
                                    <button 
                                        type="button" 
                                        className="btn-close btn-close-white ms-2" 
                                        style={{ fontSize: '0.65rem' }} 
                                        aria-label="Kick"
                                        onClick={() => {
                                            if (window.confirm(`'${p.nickname}' adlı oyuncuyu atmak istediğinize emin misiniz?`)) {
                                                connection?.invoke("KickPlayer", pin, p.nickname);
                                            }
                                        }}
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
                        <label className="fw-bold text-dark mb-2">Oyun Başlığı (İsteğe Bağlı)</label>
                        <input type="text" className="form-control form-control-lg fw-bold" placeholder="Örn: Vize Hazırlık Testi" value={quizTitle} onChange={(e) => {
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
                                                <button className="btn btn-sm btn-outline-dark fw-bold" onClick={handleDownloadMarkdown}>⬇️ .md İndir</button>
                                            </div>
                                            <ul className="list-group shadow-sm">
                                                {visualQuestions.map((vq, idx) => (
                                                    <li key={idx} className="list-group-item d-flex justify-content-between align-items-center bg-light">
                                                        <span className="text-truncate fw-bold text-dark me-3" style={{maxWidth: '65%'}}>{idx + 1}. {vq.text}</span>
                                                        <div className="d-flex gap-2">
                                                            <button className="btn btn-sm btn-outline-primary fw-bold" onClick={() => handleEditVisualQuestion(idx)}>Düzenle</button>
                                                            <button className="btn btn-sm btn-outline-danger fw-bold" onClick={() => {
                                                                setVisualQuestions(visualQuestions.filter((_, i) => i !== idx));
                                                                if (editingIndex === idx) {
                                                                    setEditingIndex(null);
                                                                    setCurrentQText('');
                                                                    setCurrentQTime(20);
                                                                    setCurrentQOptions([{ text: '', isCorrect: true }, { text: '', isCorrect: false }, { text: '', isCorrect: false }, { text: '', isCorrect: false }]);
                                                                }
                                                            }}>Sil</button>
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
                                                <div key={i} className="col-md-6">
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
                                                                onClick={() => {
                                                                    const newOpts = currentQOptions.filter((_, idx) => idx !== i);
                                                                    // Eğer silinen şık doğru cevap ise, kalan ilk şıkkı doğru cevap yap
                                                                    if (opt.isCorrect && newOpts.length > 0) newOpts[0].isCorrect = true;
                                                                    setCurrentQOptions(newOpts);
                                                                }}
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