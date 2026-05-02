import * as signalR from "@microsoft/signalr";
import { getApiBaseUrl } from "./api";

/** Connects to the ASP.NET Core SignalR hub; pass JWT via query string (see API JWT bearer setup). */
export function createNotificationsHub(accessToken: string): signalR.HubConnection {
  const url = `${getApiBaseUrl()}/hubs/notifications`;
  return new signalR.HubConnectionBuilder()
    .withUrl(url, {
      accessTokenFactory: () => Promise.resolve(accessToken),
      skipNegotiation: false,
      transport: signalR.HttpTransportType.WebSockets,
    })
    .withAutomaticReconnect()
    .build();
}
