import { useState, useEffect } from 'react';
import { HubConnection } from '@microsoft/signalr';
import { useSearchParams } from 'react-router-dom';
import { useGoogleLogin } from '@react-oauth/google';
import { useAuth } from '../context/AuthContext';
import type { QuestionPacket, WaitPhasePayload, Player, AnswerResult } from '../types/index';

export function useKahootPlayer(connection: HubConnection | null) {
    const { user, token } = useAuth();
    const [searchParams] = useSearchParams();
    
    const [pin, setPin] = useState(searchParams.get("pin") || '');
    const [nickname, setNickname] = useState('');
    const [isJoined, setIsJoined] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [isLoading, setIsLoading] = useState(false);

    const [currentQuestion, setCurrentQuestion] = useState<QuestionPacket | null>(null);
    const [waitPhase, setWaitPhase] = useState<WaitPhasePayload | null>(null);
    const [gameEndedLeaderboard, setGameEndedLeaderboard] = useState<Player[] | null>(null);
    const [timeLeft, setTimeLeft] = useState<number>(0);
    const [hasAnswered, setHasAnswered] = useState(false);
    const [answerResult, setAnswerResult] = useState<AnswerResult | null>(null);
    const [isLastQuestion, setIsLastQuestion] = useState<boolean>(false);
    const [isGettingReady, setIsGettingReady] = useState<boolean>(false);
    const [readyCountdown, setReadyCountdown] = useState<number>(3);
    const [answerStats, setAnswerStats] = useState({ answered: 0, total: 0 });

    const enableGoogleLogin = import.meta.env.VITE_ENABLE_GOOGLE_LOGIN === 'true';

    useEffect(() => {
        if (user && !isJoined) {
            setNickname(user.nickname);
        }
    }, [user, isJoined]);

    const loginWithGoogle = useGoogleLogin({
        onSuccess: async (tokenResponse) => {
            setIsLoading(true);
            setError(null);
            try {
                const res = await fetch("https://www.googleapis.com/oauth2/v3/userinfo", {
                    headers: { Authorization: `Bearer ${tokenResponse.access_token}` }
                });
                if (!res.ok) throw new Error("Google bilgileri alınamadı.");
                
                const userInfo = await res.json();
                const googleName = userInfo.name || userInfo.given_name;
                const avatarUrl = userInfo.picture || null;
                
                const sessionToken = sessionStorage.getItem("kahoot_session_token");
                const success = await connection?.invoke("JoinGame", pin, googleName, sessionToken || null, tokenResponse.access_token, avatarUrl || null);
                
                if (success) {
                    sessionStorage.setItem("kahoot_nickname", googleName);
                    sessionStorage.setItem("kahoot_player_pin", pin);
                    sessionStorage.setItem("kahoot_avatar_url", avatarUrl || "");
                    setNickname(googleName);
                    setIsJoined(true);
                } else {
                    setError("Bu oyuna katılamazsınız veya lobi dolmuş olabilir.");
                }
            } catch (err) {
                console.error("Google Login hatası:", err);
                setError("Google ile giriş yapılamadı.");
            } finally {
                setIsLoading(false);
            }
        },
        onError: () => setError("Google girişi iptal edildi veya başarısız oldu.")
    });

    useEffect(() => {
        if (!connection) return;

        connection.on("SessionTokenReceived", (token: string) => sessionStorage.setItem("kahoot_session_token", token));
        connection.on("Error", (message: string) => { setError(message); setIsLoading(false); });
        connection.on("AnswerResult", (result: AnswerResult) => setAnswerResult(result));
        connection.on("UpdateAnswerCount", (payload: any) => setAnswerStats({ answered: payload.answeredCount ?? payload.AnsweredCount ?? 0, total: payload.totalCount ?? payload.TotalCount ?? 0 }));
        
        connection.on("RedirectToNewGame", async (payload: any) => {
            try {
                const targetPin = payload.newPin || payload.NewPin;
                const targetPlayers = payload.players || payload.Players || [];
                const currentNick = sessionStorage.getItem("kahoot_nickname") || "";
                
                if (targetPlayers.some((p: any) => (typeof p === 'string' ? p : p.nickname || p.Nickname) === currentNick)) {
                    setPin(targetPin); setGameEndedLeaderboard(null); setWaitPhase(null); setCurrentQuestion(null); setHasAnswered(false); setAnswerResult(null);
                    const sessionToken = sessionStorage.getItem("kahoot_session_token");
                    sessionStorage.setItem("kahoot_player_pin", targetPin);
                    const globalToken = localStorage.getItem("kahoot_global_token");
                    const globalUserStr = localStorage.getItem("kahoot_global_user");
                    const avatarUrl = (globalUserStr ? JSON.parse(globalUserStr) : null)?.avatarUrl || sessionStorage.getItem("kahoot_avatar_url");
                    await connection.invoke("JoinGame", targetPin, currentNick, sessionToken || null, globalToken || null, avatarUrl || null);
                }
            } catch (err) { console.error("Yeni Lobiye Geçiş Hatası:", err); }
        });

        connection.on("GetReady", () => {
            setIsGettingReady(true); setReadyCountdown(3);
            let counter = 3;
            const interval = setInterval(() => {
                counter -= 1; setReadyCountdown(counter);
                if (counter <= 1) clearInterval(interval);
            }, 1000);
        });

        connection.on("ReceiveQuestion", (question: QuestionPacket) => {
            setCurrentQuestion(question); setWaitPhase(null); setIsGettingReady(false); setHasAnswered(false); setAnswerResult(null);
            setIsLastQuestion(question.currentIndex === question.totalQuestions);
            setAnswerStats({ answered: 0, total: question.totalPlayers });
        });

        connection.on("TimeUpdate", (time: number) => setTimeLeft(time));
        connection.on("WaitPhase", (payload: WaitPhasePayload) => { setWaitPhase(payload); setTimeLeft(payload.waitTime); });
        connection.on("WaitTimeUpdate", (time: number) => setTimeLeft(time));
        connection.on("GameEnded", (leaderboard: Player[]) => { setGameEndedLeaderboard(leaderboard); setWaitPhase(null); setCurrentQuestion(null); sessionStorage.removeItem("kahoot_player_pin"); });
        
        connection.on("LobbyReset", () => {
            sessionStorage.removeItem("kahoot_player_pin"); setPin(""); setIsJoined(false); setCurrentQuestion(null); setWaitPhase(null); setGameEndedLeaderboard(null); setHasAnswered(false); setAnswerResult(null); setError("Yönetici oyunu iptal etti. Lobi kapatıldı.");
        });

        connection.on("Kicked", () => {
            sessionStorage.removeItem("kahoot_player_pin"); setPin(""); setIsJoined(false); setCurrentQuestion(null); setWaitPhase(null); setGameEndedLeaderboard(null); setHasAnswered(false); setAnswerResult(null); setError("Yönetici tarafından lobiden atıldınız.");
        });

        return () => {
            connection.off("SessionTokenReceived"); connection.off("GetReady"); connection.off("Error"); connection.off("ReceiveQuestion"); connection.off("UpdateAnswerCount"); connection.off("TimeUpdate"); connection.off("WaitPhase"); connection.off("WaitTimeUpdate"); connection.off("GameEnded"); connection.off("AnswerResult"); connection.off("RedirectToNewGame"); connection.off("Kicked"); connection.off("LobbyReset");
        };
    }, [connection]);

    const handleJoin = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!connection) { setError("SignalR bağlantısı henüz kurulamadı."); return; }
        const cleanNickname = nickname.trim();
        if (cleanNickname.length === 0) return;

        setError(null); setIsLoading(true);
        try {
            const sessionToken = sessionStorage.getItem("kahoot_session_token");
            const avatarUrlToUse = user?.avatarUrl || null;
            const authToPass = token || null; 
            const success = await connection.invoke("JoinGame", pin, cleanNickname, sessionToken || null, authToPass, avatarUrlToUse);
            
            if (success) {
                sessionStorage.setItem("kahoot_nickname", cleanNickname);
                sessionStorage.setItem("kahoot_player_pin", pin);
                sessionStorage.setItem("kahoot_avatar_url", avatarUrlToUse || "");
                setIsJoined(true);
            }
        } catch (err) { setError("Oyuna katılırken beklenmeyen bir hata oluştu."); } 
        finally { setIsLoading(false); }
    };

    const submitAnswer = (optionId: string) => {
        if (!connection || !currentQuestion || hasAnswered) return;
        connection.invoke("SubmitAnswer", pin, nickname.trim(), currentQuestion.id, optionId);
        setHasAnswered(true);
    };

    return {
        pin, setPin, nickname, setNickname, isJoined, setIsJoined, error, setError,
        isLoading, setIsLoading, currentQuestion, setCurrentQuestion, waitPhase, setWaitPhase,
        gameEndedLeaderboard, setGameEndedLeaderboard, timeLeft, hasAnswered, setHasAnswered,
        answerResult, setAnswerResult, isLastQuestion, isGettingReady, readyCountdown,
        answerStats, enableGoogleLogin, loginWithGoogle, handleJoin, submitAnswer, user, token
    };
}