namespace Jaberah.Models.ViewModels.Notifications
{
    public class NotificationMessage
    {
        public MessageModel message { get; set; }
    }

    public class MessageModel
    {
        public string token { get; set; }
        public NotificationModel notification { get; set; }
    }

    public class NotificationModel
    {
        public string title { get; set; }
        public string body { get; set; }
    }

}
