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
                TimeChecker.SetEmployersList(container.GetMap());
                TimeChecker.Start();
                await Bot.SetWebhookAsync("");
                int offset = 0;

                // Admin vars
                bool gettingmoney = false;
                int temp_key = 0;

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
                                        msg += $"Аванс - {employer.Prepayment}\n";
                                        msg += $"Дней проработано - {employer.Days}\n";
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
                                    else if (message.isCommand("/reset"))
                                    {
                                        employer.Event = Employer.ActiveState.ResetData;
                                        var kb = new Telegram.Bot.Types.ReplyMarkups.ReplyKeyboardMarkup(new Telegram.Bot.Types.KeyboardButton[][]
                                        {
                                            new Telegram.Bot.Types.KeyboardButton[]
                                            {
                                                new Telegram.Bot.Types.KeyboardButton("Да"),
                                                new Telegram.Bot.Types.KeyboardButton("Нет")
                                            }
                                        }, resizeKeyboard: true, oneTimeKeyboard: true);
                                        await employer.Sender.SendAsync("Подтверждаете? (Да/Нет)", kb);
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
                                else if (employer.Event == Employer.ActiveState.ConfirmDay)
                                {
                                    var hkb = new Telegram.Bot.Types.ReplyMarkups.ReplyKeyboardHide();
                                    if (message.isCommand("Да") || message.isCommand("Нет"))
                                    {
                                        if (message.isCommand("Да"))
                                        {
                                            await employer.Sender.SendAsync("Отлично! Введите зарплату:", hkb);
                                            employer.Event = Employer.ActiveState.AddDaySalary;
                                        }
                                        else
                                        {
                                            await employer.Sender.SendAsync("Хорошо, удачи!", hkb);
                                            employer.Event = Employer.ActiveState.Default;
                                        }
                                    }
                                }
                                else if (employer.Event == Employer.ActiveState.ResetData)
                                {
                                    var hkb = new Telegram.Bot.Types.ReplyMarkups.ReplyKeyboardHide();
                                    employer.Event = Employer.ActiveState.Default;
                                    if (message.isCommand("Да") || message.isCommand("Нет"))
                                    {
                                        if (message.isCommand("Да"))
                                        {
                                            await employer.Reset();
                                        }
                                        else
                                        {
                                            await employer.Sender.SendAsync("Хорошо, удачи!", hkb);
                                            employer.Event = Employer.ActiveState.Default;
                                        }
                                    }
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
                            else if (message.isCommand("Подтвердить"))
                            {
                                int key;
                                int.TryParse(Regex.Match(message.Text, @"\d+").Value, out key);
                                await SendAnswer(true, key);
                            }
                            else if (message.isCommand("Отклонить"))
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
                            else if (message.isCommand("Удалить"))
                            {
                                int key;
                                int.TryParse(Regex.Match(message.Text, @"\d+").Value, out key);
                                if (!container.Contains(key))
                                    await EmployersContainer.AdminSender.SendAsync("Рабочий за данным ключом не найден.");
                                else
                                {
                                    await container.GetEmployerByKey(key)?.Sender.SendAsync("Вас удалил начальник.");
                                    await container.DeleteEmployerByKey(key);
                                }
                            }
                            else if (message.isCommand("Инфо"))
                            {
                                int key;
                                int.TryParse(Regex.Match(message.Text, @"\d+").Value, out key);
                                if (!container.Contains(key))
                                    await EmployersContainer.AdminSender.SendAsync("Рабочий за данным ключом не найден.");
                                else
                                {
                                    Employer employer = container.GetEmployerByKey(key);
                                    string msg = "Профиль:\n";
                                    msg += $"Имя и фамилия - {employer.Name}\n";
                                    msg += $"Ключ - {employer.Key}\n";
                                    msg += $"Зарплата - {employer.Salary}\n";
                                    msg += $"Аванс - {employer.Prepayment}\n";
                                    msg += $"Дней проработано - {employer.Days}\n";
                                    msg += $"Время уведомлений - {employer.TimeToNotify}\n";
                                    await EmployersContainer.AdminSender.SendAsync(msg);
                                }
                            }
                            else if (message.isCommand("Все работы"))
                            {
                                string str = string.Join("", container.GetPlacesAsString());
                                while (str.Count() > 4000)
                                {
                                    string temp = str.Take(4000).ToString();
                                    await EmployersContainer.AdminSender.SendAsync(temp);
                                    str.Remove(0, 4000);
                                }
                                await EmployersContainer.AdminSender.SendAsync(str);
                            }
                            else if (message.isCommand("Работы"))
                            {
                                int key;
                                int.TryParse(Regex.Match(message.Text, @"\d+").Value, out key);
                                if (container.Contains(key))
                                    await EmployersContainer.AdminSender.SendAsync(string.Join("", container.GetPlacesByEmployerKey(key)));
                                else
                                    await EmployersContainer.AdminSender.SendAsync("Рабочий за данным ключом не найден.");
                            }
                            else if (message.isCommand("Запросы"))
                            {
                                if (requests.Count == 0)
                                    await EmployersContainer.AdminSender.SendAsync("Запросов нет.");
                                else
                                {
                                    string msg = "Запросы: (Ключ | Имя)\n";
                                    foreach (var item in requests)
                                    {
                                        msg += $"{item.Key} | {item.Value}\n";
                                    }
                                    await EmployersContainer.AdminSender.SendAsync(msg);
                                }
                            }
                            else if (message.isCommand("/commands"))
                            {
                                StringBuilder builder = new StringBuilder();
                                builder.AppendLine("Список:");
                                builder.AppendLine("Подтвердить - Подтвердить запрос работника. Формат: 'Подтвердить ключ'");
                                builder.AppendLine("Отклонить - Отклонить запрос работника. Формат: 'Отклонить ключ'");
                                builder.AppendLine("Аванс - Записать аванс рабочему. Формат: 'Аванс ключ'");
                                builder.AppendLine("Список рабочих - Получить список всех рабочих. Формат - 'Список рабочих'");
                                builder.AppendLine("Все работы - Получить список проделаных работ. Формат - 'Все работы'");
                                builder.AppendLine("Работы - Получить список проделаных работ рабочего. Формат - 'Работы ключ'");
                                builder.AppendLine("Инфо - Получить профиль рабочего. Формат - 'Инфо ключ'");
                                builder.AppendLine("Запросы - Получить список всех активных запросов. Формат - 'Запросы'");
                                builder.AppendLine("Удалить - Удалить рабочего из данных. Формат - 'Удалить ключ'");
                                builder.AppendLine("Ключ - уникальный индентификатор рабочего. Узнать можно используя команду 'Список рабочих'");
                                await EmployersContainer.AdminSender.SendAsync(builder.ToString());
                            }
                            else if (message.isCommand("Аванс"))
                            {
                                int key;
                                int.TryParse(Regex.Match(message.Text, @"\d+").Value, out key);
                                if (!container.Contains(key))
                                    await EmployersContainer.AdminSender.SendAsync("Рабочий за данным ключом не найден.");
                                else
                                {
                                    temp_key = key;
                                    gettingmoney = true;
                                    await EmployersContainer.AdminSender.SendAsync("Введите сумму:");
                                }
                            }
                            else
                            {
                                if (gettingmoney)
                                {
                                    float money = 0.0f;
                                    float.TryParse(Regex.Match(message.Text, @"\d+").Value, out money);
                                    Employer emp = container.GetEmployerByKey(temp_key);
                                    if (emp != null)
                                    {
                                        emp.Prepayment = money;
                                        await EmployersContainer.AdminSender.SendAsync($"Вы установили аванс ({money}) рабочему {emp.Name}.");
                                        await emp.Sender.SendAsync($"Начальство установило вам аванс - {money}.");
                                    }
                                    else
                                        await EmployersContainer.AdminSender.SendAsync("Рабочий не найден или был удалён.");
                                    money = 0;
                                    temp_key = 0;
                                    gettingmoney = false;
                                }
                                else
                                    await EmployersContainer.AdminSender.SendAsync("Неизвестная команда. Проверьте правильность набора.");
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
            if (!container.Contains(key))
            {
                await EmployersContainer.AdminSender.SendAsync($"Работник не найден за ключом {key}");
                return false;
            }
            if (!requests.Keys.Contains(key))
            {
                await EmployersContainer.AdminSender.SendAsync($"Этот работник не отправлял запрос! ({key})");
                return false;
            }
            Employer employer = container.GetEmployerByKey(key);
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
