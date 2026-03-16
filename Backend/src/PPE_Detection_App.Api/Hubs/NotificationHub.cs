using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace PPE_Detection_App.Api.Hubs
{
    public class NotificationHub : Hub
    {
        // This hub is intentionally left empty.
        // The business logic for sending notifications is handled by the NotificationService,
        // which uses IHubContext<NotificationHub> to access the hub and send messages.
        // This approach separates concerns and keeps the hub clean.
    }
}
