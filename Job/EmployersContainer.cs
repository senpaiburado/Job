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

        public async Task<bool> Contains(long EmployerID)
        {
            foreach (var key in employers.Keys)
            {
                if (key == EmployerID)
                    return await Task<bool>.FromResult(true);
            }
            return await Task<bool>.FromResult(false); ;
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

        public async Task<string> Init(Telegram.Bot.TelegramBotClient bot)
        {
            AdminSender = new Sender(0L, bot);
            if (await RestoreFromDatabase())
                return "Ошибка восстановления из базы данных!";
            await InitSenders(bot);
            return "Данные успешно восстановлены!";
        }

        public async Task<int> AddEmployerUnsigned(long ID, string firstname, string lastname, Telegram.Bot.TelegramBotClient bot)
        {
            if (await Contains(ID))
                return 0;
            Employer employer = new Employer(firstname, lastname, ID, bot, false);
            employer.Days = 0;
            employer.Salary = 0.0f;
            using (var con = new MySqlConnection(BotProgram.ConnectionString))
            {
                var command = new MySqlCommand($"INSERT INTO employers SET id = {ID}, name = '{employer.Name}';", con);
                await con.OpenAsync();
                try
                {
                    await command.ExecuteNonQueryAsync();
                    command.CommandText = $"SELECT ikey FROM employers WHERE id = {ID};";
                    var reader = command.ExecuteReader();
                    employer.Key = reader.GetInt32("ikey");
                    await con.CloseAsync();
                    employers.Add(ID, employer);
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

        public async Task<bool> RestoreFromDatabase()
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
                            Employer emp = new Employer(reader.GetString("name"), "", reader.GetInt64("id"),
                                null, reader.GetBoolean("confirmed"));
                            emp.Days = reader.GetInt32("days");
                            emp.Salary = reader.GetFloat("salary");
                            emp.Key = reader.GetInt32("ikey");
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

        public async Task InitSenders(Telegram.Bot.TelegramBotClient sender)
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
