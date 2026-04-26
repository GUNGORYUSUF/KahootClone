import { Link } from 'react-router-dom';

export default function Home() {
    return (
        <div className="container mt-5 text-center">
            <h1 className="display-4 text-primary fw-bold mb-4">Kahoot Clone</h1>
            <div className="d-flex justify-content-center gap-4 mt-5">
                <Link to="/player" className="btn btn-success btn-lg px-5 py-3 fs-4 shadow-sm">
                    🎮 Oyuna Katıl
                </Link>
                <Link to="/host" className="btn btn-primary btn-lg px-5 py-3 fs-4 shadow-sm">
                    👨‍🏫 Oyun Kur (Host)
                </Link>
            </div>
        </div>
    );
}