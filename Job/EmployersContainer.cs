using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Job
{
    class EmployersContainer
    {
        private Dictionary<long, Employer> employers = new Dictionary<long, Employer>();

        public static Sender AdminSender;
        public static long AdminID;

        public async Task<bool> Contains(long EmployerID)
        {
            return await Task<bool>.FromResult(employers.Keys.Contains(EmployerID));
        }

        public async Task<bool> Contains(string Name)
        {
            Name = Name.ToLower();
            foreach (var employer in employers)
            {
                if (employer.Value.Name == Name.ToLower())
                    return await Task<bool>.FromResult(true);
            }
            return await Task<bool>.FromResult(false);
        }

        public async Task<Employer> GetEmployerByID(long ID)
        {
            return await Task<Employer>.FromResult(employers.Values.SingleOrDefault(x => x.ID == ID));
        }

        public async Task<Employer> GetEmployerByKey(int key)
        {
            return await Task<Employer>.FromResult(employers.Values.SingleOrDefault(x => x.Key == key));
        }

        public async Task<string> Init(Telegram.Bot.TelegramBotClient bot)
        {
            AdminSender = new Sender(AdminID, bot);
            if (await RestoreFromDatabase())
                return "Ошибка восстановления из базы данных!";
            await InitSenders(bot);
            return "Данные успешно восстановлены!";
        }

        public async Task<int> AddEmployerUnsigned(long EmployerID, string name, Telegram.Bot.TelegramBotClient bot)
        {
            if (!await Contains(EmployerID))
                return 0;
            Employer employer = new Employer(name, EmployerID, bot, false);
            employer.Days = 0;
            employer.Salary = 0.0f;
            employer.TimeToNotify = 18;
            using (var con = new MySqlConnection(BotProgram.ConnectionString))
            {
                var command = new MySqlCommand($"INSERT INTO employers SET id = {EmployerID}, name = '{employer.Name}';", con);
                await con.OpenAsync();
                try
                {
                    await command.ExecuteNonQueryAsync();
                    command.CommandText = $"SELECT ikey FROM employers WHERE id = {EmployerID};";
                    var reader = command.ExecuteReader();
                    employer.Key = reader.GetInt32("ikey");
                    await con.CloseAsync();
                    employers.Add(EmployerID, employer);
                    return 1;
                }
                catch (System.Data.Common.DbException ex)
                {
                    Console.WriteLine(ex.Message);
                    if (con.State == System.Data.ConnectionState.Open)
                        await con.CloseAsync();
                    return -1;
                }
            }
        }

        public async Task<bool> DeleteEmployerByKey(int key)
        {
            if (employers.Remove(employers.Values.SingleOrDefault(x => x.Key == key).ID))
                return await Task.FromResult(true);
            else
                return await Task.FromResult(false);
        }

        private async Task<bool> RestoreFromDatabase()
        {
            try
            {
                using (var con = new MySqlConnection(BotProgram.ConnectionString))
                {
                    var command = new MySqlCommand("SELECT * FROM employers;", con);
                    await con.OpenAsync();
                    using (var reader = command.ExecuteReader())
                    {
                        while (await reader.ReadAsync())
                        {
                            Employer emp = new Employer(reader.GetString("name"), reader.GetInt64("id"),
                                null, reader.GetBoolean("confirmed"));
                            emp.Days = reader.GetInt32("days");
                            emp.Salary = reader.GetFloat("salary");
                            emp.Key = reader.GetInt32("ikey");
                            emp.TimeToNotify = reader.GetInt32("notify_time");
                            employers.Add(emp.ID, emp);
                        }
                    }
                    await con.CloseAsync();
                    return true;
                }
            }
            catch (System.Data.Common.DbException ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        private async Task InitSenders(Telegram.Bot.TelegramBotClient sender)
        {
            await Task.Run(() =>
            {
                foreach (var item in employers.Values)
                {
                    item.InitSender(sender);
                }
            });
        }

        public async Task<string> SetEmployerSigned(int key)
        {
            using (var con = new MySqlConnection(BotProgram.ConnectionString))
            {
                await con.OpenAsync();
                var command = new MySqlCommand($"SELECT 1 FROM employers WHERE ikey = {key} limit 1;", con);
                try
                {
                    await command.ExecuteNonQueryAsync();
                    if (await command.ExecuteScalarAsync() != DBNull.Value && await command.ExecuteScalarAsync() != null)
                    {
                        long id = 0L;
                        command.CommandText = $"SELECT id FROM employers WHERE ikey = {key};";
                        using (var reader = command.ExecuteReader())
                        {
                            await reader.ReadAsync();
                            id = reader.GetInt64("id");
                        }
                        var employer = employers.Values.SingleOrDefault(x => x.ID == id);
                        if (employer.state == Employer.State.Signed)
                        {
                            await con.CloseAsync();
                            return "Рабочий уже подтверждён!";
                        }
                        command.CommandText = $"UPDATE employers SET confirmed = True WHERE id = {id};";
                        await command.ExecuteNonQueryAsync();
                        await con.CloseAsync();
                        employer.state = Employer.State.Signed;
                        return $"{employer.Name} был подтверждён как рабочий.";
                    }
                    else
                        return "Рабочего нет в базе данных!";
                }
                catch (System.Data.Common.DbException ex)
                {
                    Console.WriteLine($"Error at Users 344: {ex.Message}");
                    if (con.State == System.Data.ConnectionState.Open)
                        await con.CloseAsync();
                    return "База данных не обнаружена! Обратитесь к разработчику бота - @BuradoSenpai";
                }
            }
        }
    }
}
