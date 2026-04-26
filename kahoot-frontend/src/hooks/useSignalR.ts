import { useState, useEffect } from 'react';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';

// Backend (C#) API adresimiz. Nginx kullandığımız için 5252 portuna gidiyoruz.
const HUB_URL = "http://localhost:5252/gamehub";

export const useSignalR = () => {
    const [connection, setConnection] = useState<HubConnection | null>(null);
    const [isConnected, setIsConnected] = useState<boolean>(false);

    useEffect(() => {
        // SignalR bağlantısını inşa et
        const newConnection = new HubConnectionBuilder()
            .withUrl(HUB_URL, {
                accessTokenFactory: () => sessionStorage.getItem("kahoot_host_token") || ""
            })
            .configureLogging(LogLevel.Information)
            .withAutomaticReconnect() // Kopsa bile otomatik tekrar bağlanmayı dener
            .build();

        setConnection(newConnection);
    }, []);

    useEffect(() => {
        if (connection) {
            connection.start()
                .then(() => {
                    console.log("🚀 SignalR Bağlantısı Başarılı!");
                    setIsConnected(true);
                })
                .catch(e => {
                    console.error("❌ SignalR Bağlantı Hatası: ", e);
                    setIsConnected(false);
                });
        }
    }, [connection]);

    // Hem bağlantı nesnesini hem de durumunu dışarıya aktarıyoruz
    return { connection, isConnected };
};