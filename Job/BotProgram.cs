using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Telegram.Bot;

namespace Job
{
    class BotProgram
    {
        public static string ConnectionString = "Server=localhost;Database=Job;Uid=root;pwd=xjkfr2017;";
        private EmployersContainer container;
        public static TelegramBotClient Bot;

        public static Dictionary<int, string> requests = new Dictionary<int, string>();
        public async Task Start(string Token)
        {
            try
            {
                container = new EmployersContainer();
                Bot = new TelegramBotClient(Token);
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
                            if (!container.Contains(message.Chat.Id))
                            {
                                int result = await container.AddEmployerUnsigned(message.Chat.Id, "", Bot);
                            }

                            Employer employer = container.GetEmployerByID(message.Chat.Id);

                            if (employer.state == Employer.State.Signed)
                            {
                                if (employer.Event == Employer.ActiveState.Default)
                                {
                                    if (message.isCommand("/add_day"))
                                    {
                                        await employer.Sender.SendAsync("Введите зарплату:");
                                        employer.Event = Employer.ActiveState.AddDaySalary;
                                    }
                                    else if (message.isCommand("/set_time"))
                                    {
                                        await employer.Sender.SendAsync("Установите время (0-23):");
                                        employer.Event = Employer.ActiveState.SetTime;
                                    }
                                    else if (message.isCommand("/get_info"))
                                    {
                                        string msg = "Профиль:\n";
                                        msg += $"Имя и фамилия - {employer.Name}\n";
                                        msg += $"Ключ - {employer.Key}\n";
                                        msg += $"Зарплата - {employer.Salary}\n";
                                        msg += $"Дней проработано - {employer.Salary}\n";
                                        msg += $"Время уведомлений - {employer.TimeToNotify}\n";
                                        await employer.Sender.SendAsync(msg);
                                    }
                                    else if (message.isCommand("/get_places"))
                                    {
                                        string msg = "Список мест:\n";
                                        foreach (var item in container.GetPlacesByEmployerKey(employer.Key))
                                        {
                                            msg += item;
                                        }
                                        await employer.Sender.SendAsync(msg);
                                    }
                                    else if (message.isCommand("/commands"))
                                    {
                                        string msg = "Команды:\n";
                                        msg += "/add_day - Добавить рабочий день\n";
                                        msg += "/set_time - Установить время, когда будуть приходить уведомления (по-умолчанию 18:00)\n";
                                        msg += "/get_info - Получить информацию о себе\n";
                                        msg += "/get_places - Получить список мест, где Вы работали\n";
                                        await employer.Sender.SendAsync(msg);
                                    }
                                    else
                                    {
                                        await employer.Sender.SendAsync("Получить информацию - /commands\n");
                                    }
                                }
                                else if (employer.Event == Employer.ActiveState.AddDaySalary)
                                {
                                    if (message.isNumber())
                                    {
                                        await employer.Sender.SendAsync("Зарплата добавлена! Введите место работы:");
                                        employer.Event = Employer.ActiveState.AddDayPlace;
                                        employer.SetDaySalary(Convert.ToSingle(message.Text));
                                    }
                                    else
                                        await employer.Sender.SendAsync("Допускаются только цифры! Введите зарплату:");
                                }
                                else if (employer.Event == Employer.ActiveState.AddDayPlace)
                                {
                                    await employer.AddDay(message.Text);
                                    await container.UpdateJobPlaces();
                                }
                                else if (employer.Event == Employer.ActiveState.SetTime)
                                {
                                    if (message.isNumber() && Convert.ToInt32(message.Text) >= 0
                                        && Convert.ToInt32(message.Text) <= 23)
                                    {
                                        await employer.SetTime(Convert.ToInt32(message.Text));
                                    }
                                    else
                                        await employer.Sender.SendAsync("Допускаются числа от 0 до 23. Установите время (0-23):");
                                }
                            }
                            else
                            {
                                if (employer.Event == Employer.ActiveState.SetName)
                                {
                                    employer.Name = message.Text;
                                    var keyboard = new Telegram.Bot.Types.ReplyMarkups.ReplyKeyboardMarkup();
                                    keyboard.ResizeKeyboard = true;
                                    keyboard.OneTimeKeyboard = true;
                                    keyboard.Keyboard = new Telegram.Bot.Types.KeyboardButton[][]
                                    {
                                        new[]
                                        {
                                            new Telegram.Bot.Types.KeyboardButton("Да"),
                                            new Telegram.Bot.Types.KeyboardButton("Нет")
                                        }
                                    };
                                    await employer.Sender.SendAsync($"Имя - {employer.Name}.\nПодтверждаете? (Да/Нет)", keyboard);
                                    employer.Event = Employer.ActiveState.SetNameConfirm;
                                }
                                else if (employer.Event == Employer.ActiveState.SetNameConfirm)
                                {
                                    if (message.isCommand("Да") || message.isCommand("Нет"))
                                    {
                                        var hkb = new Telegram.Bot.Types.ReplyMarkups.ReplyKeyboardHide();
                                        if (message.isCommand("Да"))
                                        {
                                            await employer.SendRequestToConfirmEmployer();
                                            await employer.Sender.SendAsync("Ваши данные отправлены начальнику! Ждите ответа...", hkb);
                                        }
                                        else
                                        {
                                            employer.Name = "";
                                            employer.Event = Employer.ActiveState.SetName;
                                            await employer.Sender.SendAsync("Введите имя:", hkb);
                                        }
                                    }
                                    else
                                        await employer.Sender.SendAsync("Напишите 'Да' или 'Нет'!");
                                }
                                else if (employer.Event == Employer.ActiveState.Wait)
                                {
                                    await employer.Sender.SendAsync("Ждите ответа от начальника.");
                                }
                            }
                        }
                        else
                        {
                            if (message.isCommand("/start"))
                            {
                                await EmployersContainer.AdminSender.SendAsync("Здравствуйте! Список команд - /commands.");
                            }
                            else if (message.isCommand("Подтвердить:"))
                            {
                                int key;
                                int.TryParse(Regex.Match(message.Text, @"\d+").Value, out key);
                                await SendAnswer(true, key);
                            }
                            else if (message.isCommand("Отклонить:"))
                            {
                                int key;
                                int.TryParse(Regex.Match(message.Text, @"\d+").Value, out key);
                                await SendAnswer(false, key);
                            }
                            else if (message.isCommand("Список работников"))
                            {
                                string msg = "Формат: Ключ | Имя.\nСписок:\n";
                                foreach (var item in container.GetEmployersAndKeysAsString())
                                {
                                    msg += item;
                                }
                                await EmployersContainer.AdminSender.SendAsync(msg);
                            }
                            else if (message.isCommand("Удалить:"))
                            {
                                int key;
                                int.TryParse(Regex.Match(message.Text, @"\d+").Value, out key);
                                await container.GetEmployerByKey(key)?.Result?.Sender.SendAsync("Вас удалил начальник.");
                                await container.DeleteEmployerByKey(key);
                            }
                            else if (message.isCommand("Информация о работнике:"))
                            {
                                int key;
                                int.TryParse(Regex.Match(message.Text, @"\d+").Value, out key);
                                Employer employer = await container.GetEmployerByKey(key);
                                string msg = "Профиль:\n";
                                msg += $"Имя и фамилия - {employer.Name}\n";
                                msg += $"Ключ - {employer.Key}\n";
                                msg += $"Зарплата - {employer.Salary}\n";
                                msg += $"Дней проработано - {employer.Salary}\n";
                                msg += $"Время уведомлений - {employer.TimeToNotify}\n";
                                await EmployersContainer.AdminSender.SendAsync(msg);
                            }
                            else if (message.isCommand("Все работы"))
                            {
                                await EmployersContainer.AdminSender.SendAsync(string.Join("", container.GetPlacesAsString()));
                            }
                            else if (message.isCommand("Работы:"))
                            {
                                int key;
                                int.TryParse(Regex.Match(message.Text, @"\d+").Value, out key);
                                await EmployersContainer.AdminSender.SendAsync(string.Join("", container.GetPlacesByEmployerKey(key)));
                            }
                        }
                        offset = update.Id + 1;
                    }
                }
            }
            catch (Telegram.Bot.Exceptions.ApiRequestException ex)
            {
                Console.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private async Task<bool> SendAnswer(bool answer, int key)
        {
            if (!await container.Contains(key))
            {
                await EmployersContainer.AdminSender.SendAsync($"Работник не найден за ключом {key}");
                return false;
            }
            if (!requests.Keys.Contains(key))
            {
                await EmployersContainer.AdminSender.SendAsync($"Этот работник не отправлял запрос! ({key})");
                return false;
            }
            Employer employer = await container.GetEmployerByKey(key);
            if (answer)
            {
                await employer.GetAnswerFromChief(true);
                await EmployersContainer.AdminSender.SendAsync($"Вы подтвердили {employer.Name} как работника!");
            }
            else
            {
                await employer.GetAnswerFromChief(false);
                await EmployersContainer.AdminSender.SendAsync($"Вы не подтвердили {employer.Name}.");
                await container.DeleteEmployerByKey(key);
            }
            requests.Remove(key);
            return true;
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

        public static bool isNumber(this Telegram.Bot.Types.Message message)
        {
            float temp;
            int temp1;
            if (float.TryParse(message.Text, out temp) || int.TryParse(message.Text, out temp1))
                return true;
            return false;
        }
    }
}
