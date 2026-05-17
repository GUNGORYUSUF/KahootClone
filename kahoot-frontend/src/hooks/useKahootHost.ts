import { useState, useEffect } from 'react';
import { HubConnection } from '@microsoft/signalr';
import { useAuth } from '../context/AuthContext';
import type { QuestionPacket, WaitPhasePayload, Player } from '../types/index';

export function useKahootHost(connection: HubConnection | null) {
    const { user, token } = useAuth();
    const [quizTitle, setQuizTitle] = useState(() => localStorage.getItem("kahoot_draft_title") || '');
    const [markdown, setMarkdown] = useState(() => localStorage.getItem("kahoot_draft_markdown") || '');
    const [pin, setPin] = useState<string | null>(null);
    const [players, setPlayers] = useState<any[]>([]);
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const [inputMode, setInputMode] = useState<'visual' | 'markdown'>('visual');
    const [visualQuestions, setVisualQuestions] = useState<any[]>(() => {
        const saved = localStorage.getItem("kahoot_draft_visual");
        return saved ? JSON.parse(saved) : [];
    });
    const [currentQText, setCurrentQText] = useState('');
    const [currentQTime, setCurrentQTime] = useState<number>(20);
    const [currentQOptions, setCurrentQOptions] = useState([
        { text: '', isCorrect: true }, { text: '', isCorrect: false },
        { text: '', isCorrect: false }, { text: '', isCorrect: false }
    ]);
    const [editingIndex, setEditingIndex] = useState<number | null>(null);
    const [requireGoogleAuth, setRequireGoogleAuth] = useState(false);

    const [hasUnsavedChanges, setHasUnsavedChanges] = useState(() => localStorage.getItem("kahoot_unsaved") === "true");
    useEffect(() => { localStorage.setItem("kahoot_unsaved", hasUnsavedChanges ? "true" : "false"); }, [hasUnsavedChanges]);

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

        connection.on("RedirectToNewGame", async (payload: any) => {
            const targetPin = payload.newPin || payload.NewPin;
            setPin(targetPin); sessionStorage.setItem("kahoot_host_pin", targetPin);
            setPlayers(payload.players || payload.Players || []);
            setGameEndedLeaderboard(null); setWaitPhase(null); setCurrentQuestion(null);
            try { await connection.invoke("JoinAsManager", targetPin); } catch (err) {}
        });

        connection.on("PlayerJoined", (playerObj: any) => {
            const nick = typeof playerObj === 'string' ? playerObj : (playerObj.nickname || playerObj.Nickname);
            const avatar = typeof playerObj === 'string' ? undefined : (playerObj.avatarUrl || playerObj.AvatarUrl);
            setPlayers(prev => prev.some(p => p.nickname === nick) ? prev : [...prev, { nickname: nick, avatarUrl: avatar }]);
        });

        connection.on("PlayerLeft", (nickname: string) => setPlayers(prev => prev.filter(p => p.nickname !== nickname)));

        connection.on("GetReady", () => {
            setIsGettingReady(true); setReadyCountdown(3);
            let counter = 3;
            const interval = setInterval(() => {
                counter -= 1; setReadyCountdown(counter);
                if (counter <= 1) clearInterval(interval);
            }, 1000);
        });

        connection.on("UpdateAnswerCount", (payload: any) => setAnswerStats({ answered: payload.answeredCount ?? payload.AnsweredCount ?? 0, total: payload.totalCount ?? payload.TotalCount ?? 0 }));

        connection.on("ReceiveQuestion", (question: QuestionPacket) => {
            setCurrentQuestion(question); setWaitPhase(null); setIsGettingReady(false);
            setTimeLeft(question.timeLimit); setIsLastQuestion(question.currentIndex === question.totalQuestions);
            setAnswerStats({ answered: 0, total: question.totalPlayers });
        });

        connection.on("TimeUpdate", (time: number) => setTimeLeft(time));
        connection.on("WaitPhase", (payload: WaitPhasePayload) => { setWaitPhase(payload); setCurrentQuestion(null); setTimeLeft(payload.waitTime); });
        connection.on("WaitTimeUpdate", (time: number) => setTimeLeft(time));
        connection.on("GameEnded", (leaderboard: Player[]) => { setGameEndedLeaderboard(leaderboard); setWaitPhase(null); setCurrentQuestion(null); });

        connection.on("RestoreGameState", (fullState: any) => {
            const quiz = fullState.quiz || fullState.Quiz;
            const gameState = fullState.gameState || fullState.GameState;
            const currentQ = fullState.currentQuestion || fullState.CurrentQuestion;
            
            setAnswerStats({ answered: fullState.answeredCount ?? fullState.AnsweredCount ?? 0, total: fullState.totalActiveCount ?? fullState.TotalActiveCount ?? 0 });
            if (currentQ) setIsLastQuestion((currentQ.currentIndex || currentQ.CurrentIndex) === (currentQ.totalQuestions || currentQ.TotalQuestions));
            
            if (quiz) {
                const restoredPin = quiz.pin || quiz.Pin;
                setPin(restoredPin); sessionStorage.setItem("kahoot_host_pin", restoredPin);
                setPlayers((quiz.players || quiz.Players || []).map((p: any) => ({ nickname: p.nickname || p.Nickname, avatarUrl: p.avatarUrl || p.AvatarUrl })));
            }

            if (gameState) {
                const phase = (gameState.phase !== undefined) ? gameState.phase.toString() : (gameState.Phase !== undefined ? gameState.Phase.toString() : "");
                const timeRem = gameState.timeRemaining || gameState.TimeRemaining;
                if (phase === "Question" || phase === "0") {
                    setCurrentQuestion(currentQ); setTimeLeft(timeRem); setWaitPhase(null); setGameEndedLeaderboard(null);
                } else if (phase === "Transition" || phase === "1") {
                    setWaitPhase({ waitTime: timeRem, correctOptionId: null, leaderboard: [], allAnswered: false }); setTimeLeft(timeRem); setCurrentQuestion(null); setGameEndedLeaderboard(null);
                } else if (phase === "Ended" || phase === "2") {
                    const pinToUse = quiz ? (quiz.pin || quiz.Pin) : null;
                    if (pinToUse) connection.invoke("ShowLeaderboard", pinToUse).catch(console.error);
                }
            } else { setCurrentQuestion(null); setWaitPhase(null); setGameEndedLeaderboard(null); }
        });

        connection.on("UpdateLeaderboard", (leaderboard: Player[]) => { setGameEndedLeaderboard(leaderboard); setWaitPhase(null); setCurrentQuestion(null); sessionStorage.removeItem("kahoot_host_pin"); });
        connection.on("LobbyReset", () => { setPin(null); setPlayers([]); setCurrentQuestion(null); setWaitPhase(null); setGameEndedLeaderboard(null); sessionStorage.removeItem("kahoot_host_pin"); setError("Lobi başarıyla iptal edildi."); });

        return () => {
            connection.off("PlayerJoined"); connection.off("PlayerLeft"); connection.off("GetReady"); connection.off("ReceiveQuestion"); connection.off("TimeUpdate"); connection.off("WaitPhase"); connection.off("WaitTimeUpdate"); connection.off("UpdateAnswerCount"); connection.off("GameEnded"); connection.off("RedirectToNewGame"); connection.off("RestoreGameState"); connection.off("UpdateLeaderboard"); connection.off("LobbyReset");
        };
    }, [connection]);

    const handleAddVisualQuestion = () => {
        if (!currentQText.trim()) return setError("Soru metni boş olamaz.");
        if (currentQOptions.some(o => !o.text.trim())) return setError("Tüm şıkları eksiksiz doldurmalısınız.");
        const newQuestion = { text: currentQText, timeLimitInSeconds: currentQTime, options: currentQOptions.map(o => ({ ...o })) };
        if (editingIndex !== null) {
            const updated = [...visualQuestions]; updated[editingIndex] = newQuestion;
            setVisualQuestions(updated); setEditingIndex(null);
        } else { setVisualQuestions([...visualQuestions, newQuestion]); }
        setHasUnsavedChanges(true); setCurrentQText(''); setCurrentQTime(20); setCurrentQOptions([{ text: '', isCorrect: true }, { text: '', isCorrect: false }, { text: '', isCorrect: false }, { text: '', isCorrect: false }]); setError(null);
    };

    const handleEditVisualQuestion = (idx: number) => {
        const q = visualQuestions[idx];
        setCurrentQText(q.text); setCurrentQTime(q.timeLimitInSeconds || 20); setCurrentQOptions(q.options.map((o: any) => ({ ...o }))); setEditingIndex(idx);
    };

    const handleDownloadMarkdown = () => {
        if (visualQuestions.length === 0) return;
        let md = "";
        visualQuestions.forEach(q => {
            md += `# Soru: ${q.text}\nSüre: ${q.timeLimitInSeconds || 20}\n`;
            q.options.forEach((o: any) => { md += `- ${o.text} ${o.isCorrect ? '(*)' : ''}\n`; }); md += "\n";
        });
        const url = URL.createObjectURL(new Blob([md], { type: 'text/markdown' }));
        const a = document.createElement('a'); a.href = url; a.download = 'kahoot_sorulari.md'; a.click(); URL.revokeObjectURL(url);
    };

    const handleSyncMarkdownToVisual = async () => {
        if (!markdown.trim()) return;
        setIsLoading(true); setError(null);
        try {
            const cleanLines = markdown.split('\n').map(l => l.trim()).filter(l => l.length > 0);
            if (cleanLines.length > 0 && !cleanLines[0].startsWith('#')) throw new Error("Markdown formatı hatalı: Metin '#' işareti ile başlamalıdır.");
            if (/^[ \t]*(?!Süre|Time|Timer|Duration|Zaman)([A-Za-zğüşıöçĞÜŞİÖÇ]+[ \t]*:|\d+[\.\)])/im.test(markdown)) throw new Error("Bazı soruların başında '#' işareti eksik olabilir.");
            const parseRes = await fetch("http://localhost:5252/api/Quiz/parse-markdown", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ markdownText: markdown }) });
            if (!parseRes.ok) throw new Error((await parseRes.json()).message || "Markdown ayrıştırma hatası");
            const parsedQuestions = await parseRes.json();
            if (parsedQuestions.length === 0) throw new Error("Geçerli bir soru bulunamadı.");
            setVisualQuestions(parsedQuestions.map((q: any, index: number) => {
                const text = q.text || q.Text; const opts = q.options || q.Options || [];
                if (opts.length < 2) throw new Error(`${index + 1}. Soru için en az 2 şık eklemelisiniz.`);
                if (!opts.some((o: any) => o.isCorrect || o.IsCorrect)) throw new Error(`${index + 1}. Soru için doğru cevap seçilmemiş.`);
                return { text, timeLimitInSeconds: q.timeLimitInSeconds || q.TimeLimitInSeconds || 20, options: opts.map((o: any) => ({ text: o.text || o.Text, isCorrect: o.isCorrect || o.IsCorrect || false })) };
            }));
            setInputMode('visual'); setHasUnsavedChanges(true);
        } catch (err: any) { setError(err.message); } finally { setIsLoading(false); }
    };

    const handleCreateGame = async () => {
        setIsLoading(true); setError(null);
        try {
            if (hasUnsavedChanges) { alert("Lütfen oyunu başlatmadan önce sorularınızı kaydedin."); return; }
            const oldPin = sessionStorage.getItem("kahoot_host_pin");
            if (oldPin && connection) connection.invoke("ResetLobby", oldPin).catch(() => {});
            let questions = inputMode === 'markdown' && markdown.trim() ? (await (await fetch("http://localhost:5252/api/Quiz/parse-markdown", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ markdownText: markdown }) })).json()) : visualQuestions;
            
            const cleanQuestions = questions.map((q: any) => ({ text: q.text || q.Text, timeLimitInSeconds: q.timeLimitInSeconds || q.TimeLimitInSeconds || 20, options: (q.options || q.Options || []).map((o: any) => ({ text: o.text || o.Text, isCorrect: o.isCorrect || o.IsCorrect || false })) }));
            const headers: Record<string, string> = { "Content-Type": "application/json" };
            if (token) headers["Authorization"] = `Bearer ${token}`;
            const createRes = await fetch("http://localhost:5252/api/Quiz/create", { method: "POST", headers, body: JSON.stringify({ title: quizTitle.trim() || "Canlı Oyun", questions: cleanQuestions, requireGoogleAuth, isDraft: false }) });
            if (!createRes.ok) throw new Error("Oyun kurulamadı");
            const data = await createRes.json();
            const generatedPin = data.pin || data.Pin; const generatedToken = data.token || data.Token;
            sessionStorage.setItem("kahoot_host_token", generatedToken); sessionStorage.setItem("kahoot_host_pin", generatedPin);
            if (connection) { await connection.stop(); await connection.start(); await connection.invoke("JoinAsManager", generatedPin); }
            setPin(generatedPin); localStorage.removeItem("kahoot_draft_visual"); localStorage.removeItem("kahoot_draft_markdown"); localStorage.removeItem("kahoot_draft_title"); localStorage.removeItem("kahoot_editing_pin");
        } catch (err: any) { setError(err.message); } finally { setIsLoading(false); }
    };

    const handleSaveToSystem = async () => {
        setIsLoading(true); setError(null);
        try {
            if (!quizTitle.trim()) return setError("Oyun Başlığı girin.");
            let questions = inputMode === 'markdown' && markdown.trim() ? (await (await fetch("http://localhost:5252/api/Quiz/parse-markdown", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ markdownText: markdown }) })).json()) : visualQuestions;
            if (questions.length === 0) throw new Error("Soru ekleyin.");
            const cleanQuestions = questions.map((q: any) => ({ text: q.text || q.Text, timeLimitInSeconds: q.timeLimitInSeconds || q.TimeLimitInSeconds || 20, options: (q.options || q.Options || []).map((o: any) => ({ text: o.text || o.Text, isCorrect: o.isCorrect || o.IsCorrect || false })) }));
            const editingPin = localStorage.getItem("kahoot_editing_pin");
            if (editingPin && token) try { await fetch(`http://localhost:5252/api/Quiz/${editingPin}`, { method: "DELETE", headers: { "Authorization": `Bearer ${token}` } }); } catch (e) {}
            const headers: Record<string, string> = { "Content-Type": "application/json" };
            if (token) headers["Authorization"] = `Bearer ${token}`;
            const createRes = await fetch("http://localhost:5252/api/Quiz/create", { method: "POST", headers, body: JSON.stringify({ title: quizTitle.trim(), questions: cleanQuestions, requireGoogleAuth, isDraft: true }) });
            if (!createRes.ok) throw new Error("Kaydedilemedi");
            localStorage.setItem("kahoot_editing_pin", (await createRes.json()).pin || (await createRes.json()).Pin);
            setHasUnsavedChanges(false); alert("Başarıyla kaydedildi!");
        } catch (err: any) { setError(err.message); } finally { setIsLoading(false); }
    };

    return {
        user, token, quizTitle, setQuizTitle, markdown, setMarkdown, pin, setPin, players, setPlayers,
        isLoading, setIsLoading, error, setError, inputMode, setInputMode, visualQuestions, setVisualQuestions,
        currentQText, setCurrentQText, currentQTime, setCurrentQTime, currentQOptions, setCurrentQOptions,
        editingIndex, setEditingIndex, requireGoogleAuth, setRequireGoogleAuth, hasUnsavedChanges, setHasUnsavedChanges,
        currentQuestion, setCurrentQuestion, waitPhase, setWaitPhase, gameEndedLeaderboard, setGameEndedLeaderboard,
        timeLeft, setTimeLeft, isLastQuestion, setIsLastQuestion, isGettingReady, setIsGettingReady, readyCountdown,
        setReadyCountdown, answerStats, setAnswerStats, handleAddVisualQuestion, handleEditVisualQuestion,
        handleDownloadMarkdown, handleSyncMarkdownToVisual, handleCreateGame, handleSaveToSystem
    };
}