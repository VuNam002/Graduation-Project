using Microsoft.AspNetCore.SignalR;
using PPE_Detection_App.Api.Hubs;
using PPE_Detection_App.Api.Models.DTO;
using System.Threading.Tasks;

namespace PPE_Detection_App.Api.Services
{
    public class NotificationService
    {
        private readonly IHubContext<NotificationHub> _hubContext;

        public NotificationService(IHubContext<NotificationHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task SendViolationNotificationAsync(ViolationNotificationDto notification)
        {
            await _hubContext.Clients.All.SendAsync("ReceiveViolation", notification);
        }
    }
}
