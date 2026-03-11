using FirebaseAdmin.Messaging;

namespace Jaberah.Helpers
{
    public class FirebaseService
    {
        public async Task SendToTopicAsync(string title, string body, string topic)
        {
            var message = new Message()
            {
                Notification = new Notification()
                {
                    Title = title,
                    Body = body,
                },
                Data = new Dictionary<string, string>
                {
                    { "topic", topic },
                },
                Topic = topic
            };

            await FirebaseMessaging.DefaultInstance.SendAsync(message);
        }

        public async Task SendToTokenAsync(string title, string body, string token)
        {
            var message = new Message()
            {
                Notification = new Notification()
                {
                    Title = title,
                    Body = body,
                },
                Data = new Dictionary<string, string>
                {
                    { "topic", token },
                },
                Token = token
            };

            await FirebaseMessaging.DefaultInstance.SendAsync(message);
        }
    }
}
