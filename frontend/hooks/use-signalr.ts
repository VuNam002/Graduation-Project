import { useEffect, useState } from 'react';
import * as signalR from '@microsoft/signalr';

const useSignalR = (hubUrl: string) => {
  const [connection, setConnection] = useState<signalR.HubConnection | null>(null);

  useEffect(() => {
    // Ensure the URL is absolute
    const absoluteUrl = `${process.env.NEXT_PUBLIC_API_BASE_URL}${hubUrl}`;

    const newConnection = new signalR.HubConnectionBuilder()
      .withUrl(absoluteUrl)
      .withAutomaticReconnect()
      .build();

    setConnection(newConnection);

    console.log(`SignalR: Hub connection configured for ${absoluteUrl}`);

  }, [hubUrl]);

  useEffect(() => {
    if (connection) {
      connection.start()
        .then(() => console.log('SignalR: Connection started successfully.'))
        .catch(err => console.error('SignalR: Connection failed to start.', err));

      // Cleanup on component unmount
      return () => {
        console.log('SignalR: Stopping connection.');
        connection.stop();
      };
    }
  }, [connection]);

  return connection;
};

export default useSignalR;
