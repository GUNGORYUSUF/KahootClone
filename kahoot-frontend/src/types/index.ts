export interface Player {
    id: string;
    nickname: string;
    score: number;
}

export interface QuestionPacket {
    id: string;
    text: string;
    timeLimit: number;
    options: { id: string; text: string }[];
    currentIndex: number;
    totalQuestions: number;
    totalPlayers: number;
}

export interface AnswerResult {
    isCorrect: boolean;
    points: number;
}

export interface WaitPhasePayload {
    waitTime: number;
    correctOptionId: string | null;
    leaderboard: Player[];
    allAnswered: boolean;
}
