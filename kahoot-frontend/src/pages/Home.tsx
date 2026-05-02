import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';

export default function Home() {
    const { user, token } = useAuth();
    const navigate = useNavigate();
    const [myQuizzes, setMyQuizzes] = useState<any[]>([]);
    const [showModal, setShowModal] = useState(false);
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const fetchMyQuizzes = async () => {
        setShowModal(true);
        setIsLoading(true);
        setError(null);
        try {
            const res = await fetch("http://localhost:5252/api/Quiz/my-quizzes", {
                headers: {
                    "Authorization": `Bearer ${token}`
                }
            });
            if (!res.ok) throw new Error("Oyunlar yüklenirken bir hata oluştu.");
            
            const data = await res.json();
            setMyQuizzes(data);
        } catch (err: any) {
            setError(err.message);
        } finally {
            setIsLoading(false);
        }
    };

    // YENİ: Kayıtlı oyunu Soru Oluşturucuya (HostView) yükle
    const handleEditSavedGame = (quiz: any) => {
        const mappedQuestions = quiz.questions?.map((q: any) => ({
            text: q.text || q.Text,
            timeLimitInSeconds: q.timeLimitInSeconds || q.TimeLimitInSeconds || 20,
            options: q.options?.map((o: any) => ({
                text: o.text || o.Text,
                isCorrect: o.isCorrect || o.IsCorrect
            })) || []
        })) || [];
        
        localStorage.setItem("kahoot_draft_visual", JSON.stringify(mappedQuestions));
        localStorage.setItem("kahoot_draft_markdown", ""); 
        localStorage.setItem("kahoot_draft_title", quiz.title || "");
        localStorage.setItem("kahoot_editing_pin", quiz.pin || "");
        localStorage.setItem("kahoot_unsaved", "false"); // Yüklenen oyun "kayıtlı" kabul edilir
        setShowModal(false);
        navigate("/host");
    };

    // YENİ: İstenmeyen oyunu veritabanından tamamen sil
    const handleDeleteSavedGame = async (pin: string) => {
        if (!window.confirm("Bu oyunu tamamen silmek istediğinize emin misiniz?")) return;
        try {
            const res = await fetch(`http://localhost:5252/api/Quiz/${pin}`, {
                method: "DELETE",
                headers: { "Authorization": `Bearer ${token}` }
            });
            if (!res.ok) throw new Error("Oyun silinemedi.");
            setMyQuizzes(prev => prev.filter(q => q.pin !== pin));
        } catch (err: any) {
            alert(err.message);
        }
    };

    return (
        <div className="container mt-5 text-center">
            <h1 className="display-4 text-primary fw-bold mb-4">Kahoot Clone</h1>
            
            {user && (
                <div className="mb-5 animate__animated animate__fadeIn">
                    <h2 className="fw-bold text-dark">Hoş Geldin, {user.nickname}! 👋</h2>
                    <p className="text-muted fs-5">Buradan yeni oyun kurabilir, oyunlara katılabilir veya profilini yönetebilirsin.</p>
                </div>
            )}

            <div className="d-flex flex-wrap justify-content-center gap-4 mt-4">
                <Link to="/player" className="btn btn-success btn-lg px-5 py-3 fs-4 fw-bold shadow-sm">
                    🎮 Oyuna Katıl
                </Link>
                <Link to="/host" className="btn btn-primary btn-lg px-5 py-3 fs-4 fw-bold shadow-sm">
                    👨‍🏫 Oyun Kur (Host)
                </Link>
            </div>

            {user && (
                <div className="row justify-content-center mt-5 animate__animated animate__fadeInUp">
                    <div className="col-md-8 col-lg-6">
                        <div className="card shadow-sm border-0 rounded-4">
                            <div className="card-body p-4 d-flex flex-column flex-sm-row justify-content-around gap-3">
                                <button className="btn btn-info text-white fw-bold px-4 py-3 d-flex align-items-center justify-content-center gap-2 shadow-sm" onClick={() => alert("Yakında: Backend entegrasyonu ile geçmiş oyun istatistikleri buraya gelecek!")}>
                                    📊 İstatistiklerim
                                </button>
                                <button className="btn btn-primary fw-bold px-4 py-3 d-flex align-items-center justify-content-center gap-2 shadow-sm" onClick={fetchMyQuizzes}>
                                    📁 Kayıtlı Sorularım
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            )}

            {/* KAYITLI SORULARIM MODALI */}
            {showModal && (
                <div className="modal d-block animate__animated animate__fadeIn" style={{ backgroundColor: 'rgba(0,0,0,0.5)' }} tabIndex={-1}>
                    <div className="modal-dialog modal-lg modal-dialog-centered modal-dialog-scrollable">
                        <div className="modal-content border-0 shadow-lg rounded-4">
                            <div className="modal-header bg-primary text-white border-0 py-3">
                                <h5 className="modal-title fw-bold">📁 Kayıtlı Sorularım</h5>
                                <button type="button" className="btn-close btn-close-white" onClick={() => setShowModal(false)}></button>
                            </div>
                            <div className="modal-body p-4">
                                {isLoading ? (
                                    <div className="text-center py-5">
                                        <div className="spinner-border text-primary" role="status"></div>
                                        <p className="mt-3 text-muted fw-bold">Oyunlarınız yükleniyor...</p>
                                    </div>
                                ) : error ? (
                                    <div className="alert alert-danger fw-bold">{error}</div>
                                ) : myQuizzes.length === 0 ? (
                                    <div className="text-center py-5">
                                        <h1 className="display-1 text-muted opacity-50">📂</h1>
                                        <h4 className="text-muted fw-bold mt-3">Henüz kaydedilmiş bir oyununuz yok.</h4>
                                        <p className="text-muted">Host ekranından yeni bir oyun kurduğunuzda burada listelenecektir.</p>
                                    </div>
                                ) : (
                                    <ul className="list-group list-group-flush text-start">
                                        {myQuizzes.map((quiz, i) => (
                                            <li key={quiz.id || i} className="list-group-item d-flex justify-content-between align-items-center py-3 px-0 border-bottom">
                                                <div>
                                                    <h5 className="fw-bold mb-1 text-dark">{quiz.title || "İsimsiz Oyun"}</h5>
                                                    <small className="text-muted fw-bold"> Toplam {quiz.questions?.length || 0} Soru İçerir</small>
                                                </div>
                                                <div className="d-flex align-items-center gap-2">
                                                    <button className="btn btn-sm btn-primary fw-bold px-4 py-2 shadow-sm" onClick={() => handleEditSavedGame(quiz)} disabled={isLoading}>
                                                        ✏️ Yükle / Düzenle
                                                    </button>
                                                    <button className="btn btn-sm btn-outline-danger fw-bold px-3 py-2 shadow-sm" onClick={() => handleDeleteSavedGame(quiz.pin)} disabled={isLoading}>
                                                        🗑️
                                                    </button>
                                                </div>
                                            </li>
                                        ))}
                                    </ul>
                                )}
                            </div>
                            <div className="modal-footer border-0 bg-light rounded-bottom-4">
                                <button type="button" className="btn btn-secondary fw-bold px-4" onClick={() => setShowModal(false)}>Kapat</button>
                            </div>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}