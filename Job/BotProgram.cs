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
                var container = new EmployersContainer();
                var Bot = new TelegramBotClient(Token);
                await container.Init(Bot);
                await Bot.SetWebhookAsync("");
                int offset = 0;
                while (true)
                {
                    var updates = await Bot.GetUpdatesAsync(offset);

                    foreach (var update in updates.Where(x => x.Message != null && x.Message.Type == Telegram.Bot.Types.Enums.MessageType.TextMessage))
                    {
                        var message = update.Message;

                        if (message.Chat.Id != EmployersContainer.AdminID)
                        {
                            if (!await container.Contains(message.Chat.Id))
                            {
                                int result = await container.AddEmployerUnsigned(message.Chat.Id, "", Bot);
                            }

                            Employer employer = await container.GetEmployerByID(message.Chat.Id);

                            if (employer.state == Employer.State.Signed)
                            {
                                if (employer.Event == Employer.ActiveState.Default)
                                {
                                    if (message.isCommand("/add_day"))
                                    {

                                    }
                                    else if (message.isCommand("/set_time"))
                                    {

                                    }
                                    else if (message.isCommand("/get_info"))
                                    {

                                    }
                                    else
                                    {

                                    }
                                }
                            }
                            else
                            {

                            }
                        }
                        else
                        {

                        }
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
    public static class AdditionalCommands
    {
        public static bool isCommand(this Telegram.Bot.Types.Message message, string text)
        {
            if (message.Text.ToLower().Contains(text.ToLower()))
                return true;
            else
                return false;
        }
    }
}
