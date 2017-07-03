using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Job
{
    class Sender
    {
        private Telegram.Bot.TelegramBotClient bot;
        private long EmployerID;

        public Sender(long EmployerID, Telegram.Bot.TelegramBotClient bot)
        {
            this.EmployerID = EmployerID;
            this.bot = bot;
        }

        public async Task SendAsync(string text, Telegram.Bot.Types.ReplyMarkups.IReplyMarkup replyMarkup = null)
        {
            try
            {
                await bot.SendTextMessageAsync(EmployerID, text, replyMarkup: replyMarkup);
            }
            catch (Telegram.Bot.Exceptions.ApiRequestException ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        public void Init(Telegram.Bot.TelegramBotClient Sender)
        {
            bot = Sender;
        }
    }
}
