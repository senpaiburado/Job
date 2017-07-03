using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

namespace Job
{
    class Employer
    {
        Sender Sender { get; set; }
        public string Name { get; set; }
        public long ID { get; set; }
        public enum State
        {
            Unsigned, Signed
        }

        public enum ActiveState
        {
            Default, SetName
        }

        public int Key { get; set; }

        public int Days { get; set; }
        public float Salary { get; set; }

        public int TimeToNotify { get; set; }

        public State state { get; set; }
        public ActiveState Event { get; set; }

        public Employer(string name, long ChatId, Telegram.Bot.TelegramBotClient sender, bool signed)
        {
            Name = name;
            ID = ChatId;
            InitSender(sender);
            state = signed ? State.Signed : State.Unsigned;
            if (string.IsNullOrWhiteSpace(Name))
            {
                Sender.SendAsync("Напишите Ваше имя:").Wait();
                Event = ActiveState.SetName;
            }
        }

        public void InitSender(Telegram.Bot.TelegramBotClient Sender)
        {
            this.Sender.Init(Sender);
        }

        public async Task AddDay(float salary, string place)
        {
            Days++;
            Salary += salary;
            await EmployersContainer.AdminSender.SendAsync($"{Name} сегодня работал. Заработал: {salary} руб. Место: {place}.");
            using (var con = new MySqlConnection(BotProgram.ConnectionString))
            {
                var command = new MySqlCommand($"UPDATE employers SET days := (days+1), salary := (salary+{salary}) WHERE id = {ID};");
                await con.OpenAsync();
                await command.ExecuteNonQueryAsync();
                command.CommandText = $"INSERT INTO jobs SET name = '{Name}, place = '{place}', salary = {salary}, DateOfWork = now();";
                await command.ExecuteNonQueryAsync();
                await con.CloseAsync();
            }
            await Sender.SendAsync("День работы добавлен!");
        }
    }
}
