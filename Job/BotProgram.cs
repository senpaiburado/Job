using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;

namespace Job
{
    class BotProgram
    {
        public static string ConnectionString = "Server=localhost;Database=Job;Uid=root;pwd=xjkfr2017;";
        public async Task Start(string Token)
        {
            try
            {
                var Bot = new TelegramBotClient(Token);
                await Bot.SetWebhookAsync("");
                int offset = 0;
                while (true)
                {
                    var updates = await Bot.GetUpdatesAsync(offset);

                    foreach (var update in updates.Where(x => x.Message != null && x.Message.Type == Telegram.Bot.Types.Enums.MessageType.TextMessage))
                    {
                        var message = update.Message;

                        

                        offset = update.Id + 1;
                    }
                }
            }
            catch (Telegram.Bot.Exceptions.ApiRequestException ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
