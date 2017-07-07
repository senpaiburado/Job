using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using Hangfire;

namespace Job
{
    class Employer
    {
        public Sender Sender { get; set; }
        public string Name { get; set; }
        public long ID { get; set; }
        public enum State
        {
            Unsigned, Signed
        }

        public enum ActiveState
        {
            Default, SetName, Wait, SetNameConfirm, AddDaySalary, AddDayPlace, SetTime, ResetData, ConfirmDay
        }

        public int Key { get; set; }

        public int Days { get; set; }
        public float Salary { get; set; }

        public int TimeToNotify { get; set; }

        public State state { get; set; }
        public ActiveState Event { get; set; }

        private float temp_salary { get; set; }

        public bool ResetConfirming { get; set; }

        public Employer(string name, long ChatId, Telegram.Bot.TelegramBotClient sender, bool signed)
        {
            Name = name;
            ID = ChatId;
            InitSender(sender);
            if (Sender.bot == null)
                Sender.bot = BotProgram.Bot;
            state = signed ? State.Signed : State.Unsigned;
            ResetConfirming = false;
            if (string.IsNullOrWhiteSpace(Name))
            {
                Sender.SendAsync("Напишите Ваше имя:").Wait();
                Event = ActiveState.SetName;
            }
        }

        public void InitSender(Telegram.Bot.TelegramBotClient Sender)
        {
            this.Sender = new Sender(ID, Sender);
        }

        public void SetDaySalary(float salary)
        {
            temp_salary = salary;
        }

        public async Task Notify()
        {
            if (state == State.Unsigned)
                return;
            if (Event == ActiveState.SetTime)
                return;
            await Sender.SendAsync("Работал? (Да/Нет)", new Telegram.Bot.Types.ReplyMarkups.ReplyKeyboardMarkup(new Telegram.Bot.Types.KeyboardButton[][]
                {
                    new Telegram.Bot.Types.KeyboardButton[]
                    {
                        new Telegram.Bot.Types.KeyboardButton("Да"),
                        new Telegram.Bot.Types.KeyboardButton("Нет")
                    }
                },
                resizeKeyboard: true, oneTimeKeyboard: true));
            Event = ActiveState.ConfirmDay;
        }

        public async Task AddDay(string place)
        {
            Days++;
            Salary += temp_salary;
            await EmployersContainer.AdminSender.SendAsync($"{Name} сегодня работал. Заработал: {temp_salary} руб. Место: {place}. Дата: {DateTime.Now.ToString()}");
            using (var con = new MySqlConnection(BotProgram.ConnectionString))
            {
                var command = new MySqlCommand($"UPDATE employers SET days := (days+1), salary := (salary+{temp_salary}) WHERE id = {ID};", con);
                await con.OpenAsync();
                await command.ExecuteNonQueryAsync();
                command.CommandText = $"INSERT INTO jobs SET name = '{Name}', place = '{place}', salary = {temp_salary}, empkey = {Key}, DateOfWork = now();";
                await command.ExecuteNonQueryAsync();
                await con.CloseAsync();
            }
            temp_salary = 0;
            await Sender.SendAsync("День работы добавлен!");
            Event = ActiveState.Default;
        }

        public async Task SetTime(int time)
        {
            try
            {
                using (var con = new MySqlConnection(BotProgram.ConnectionString))
                {
                    var command = new MySqlCommand($"UPDATE employers SET notify_time = {time} WHERE ikey = {Key};", con);
                    await con.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                    await con.CloseAsync();
                }
                TimeToNotify = time;
                RecurringJob.AddOrUpdate($"{Key}", () => Notify().Wait(), $"00 {TimeToNotify} * * *");
                await Sender.SendAsync($"Время установлено. Уведомления будут приходить в {time}:00");
            }
            catch (Exception ex)
            {
                await Sender.SendAsync("Ошибка при работе с базой данных!");
                Console.WriteLine(ex.Message);
            }
            Event = ActiveState.Default;
        }

        public async Task SendRequestToConfirmEmployer()
        {
            string message = $"Работник {Name} просит Вашего подтверждения! Ключ - {Key}.\n";
            message += $"Чтобы подтвердить, отправьте мне сообщение: Подтвердить: {Key}\n";
            message += $"Шаблоны:\nПодтвердить: ключ\nОтклонить: ключ";
            await EmployersContainer.AdminSender.SendAsync(message);
            Event = ActiveState.Wait;
            BotProgram.requests.Add(Key, Name);
        }

        public async Task GetAnswerFromChief(bool confirmed)
        {
            if (confirmed)
            {
                state = State.Signed;
                Event = ActiveState.Default;
                using (var con = new MySqlConnection(BotProgram.ConnectionString))
                {
                    var command = new MySqlCommand($"UPDATE employers SET confirmed = true WHERE ikey = {Key};", con);
                    await con.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                    command.CommandText = $"UPDATE employers SET name = '{Name}' WHERE ikey = {Key};";
                    await command.ExecuteNonQueryAsync();
                    await con.CloseAsync();
                }
                RecurringJob.AddOrUpdate($"{Key}", () => Notify().Wait(), $"00 {TimeToNotify} * * *", timeZone: TimeZoneInfo.Local);
                await Sender.SendAsync("Вы были подтверждены! Теперь Вам доступны команды - /commands. Удачи!");
            }
            else
            {
                Event = ActiveState.SetName;
                await Sender.SendAsync("К сожалению, Вас не подтвердило начальство. Ваши данные удалены.");
                Sender = null;
            }
        }

        public async Task Reset()
        {
            try
            {
                using (var con = new MySqlConnection(BotProgram.ConnectionString))
                {
                    var command = new MySqlCommand($"UPDATE employers SET days = 0, salary = 0 WHERE ikey = {Key};", con);
                    await con.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                    await con.CloseAsync();
                }
                ResetConfirming = false;
                Days = 0;
                Salary = 0;
                await Sender.SendAsync("Количество дней и денег сброшено.");
                await EmployersContainer.AdminSender.SendAsync($"{Name} (Ключ: {Key}) сбросил значения дней и денег.");
            }
            catch (Exception ex)
            {
                await Sender.SendAsync("Ошибка при работе с базой данных!");
                Console.WriteLine(ex.Message);
            }
            Event = ActiveState.Default;
        }
    }
}
