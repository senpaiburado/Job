using Hangfire;
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
        private List<JobPlace> places = new List<JobPlace>();

        public static Sender AdminSender;
        public static long AdminID = 0L;

        public bool Contains(long EmployerID)
        {
            return employers.Keys.Contains(EmployerID);
        }

        public bool Contains(int key)
        {
            return employers.Values.SingleOrDefault(x => x.Key == key) != null;
        }

        public string[] GetEmployersAndKeysAsString()
        {
            List<string> str = new List<string>();
            if (employers.Count == 0)
                return new string[1] { "Список пуст." };
            foreach (var item in employers.Values)
            {
                str.Add($"{item.Key} | {item.Name}\n");
            }
            return str.ToArray();
        }

        public Employer GetEmployerByID(long ID)
        {
            try
            {
                return employers.Values.SingleOrDefault(x => x.ID == ID);
            }
            catch (ArgumentNullException ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }

        public Employer GetEmployerByKey(int key)
        {
            return employers.Values.SingleOrDefault(x => x.Key == key);
        }

        public async Task<string> Init(Telegram.Bot.TelegramBotClient bot)
        {
            AdminSender = new Sender(AdminID, bot);
            if (!await RestoreFromDatabase())
                return "Ошибка восстановления из базы данных!";
            InitSenders(bot);
            return "Данные успешно восстановлены!";
        }

        public async Task<int> AddEmployerUnsigned(long EmployerID, string name, Telegram.Bot.TelegramBotClient bot)
        {
            if (Contains(EmployerID))
                return 1;
            Employer employer = new Employer(name, EmployerID, bot, false);
            employer.Days = 0;
            employer.Salary = 0.0f;
            employer.TimeToNotify = 18;
            using (var con = new MySqlConnection(BotProgram.ConnectionString))
            {
                var command = new MySqlCommand($"INSERT INTO employers SET id = {EmployerID}, name = '{employer.Name}';", con);
                try
                {
                    await con.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                    command.CommandText = $"SELECT ikey FROM employers WHERE id = {EmployerID};";
                    var reader = command.ExecuteReader();
                    await reader.ReadAsync();
                    employer.Key = reader.GetInt32("ikey");
                    await con.CloseAsync();
                    employers.Add(EmployerID, employer);


                    employer.Event = Employer.ActiveState.SetTime;
                    RecurringJob.AddOrUpdate($"{employer.Key}", () => employer.Notify().Wait(), $"00 {employer.TimeToNotify} * * *");

                    return 2;
                }
                catch (MySqlException ex)
                {
                    Console.WriteLine(ex.Message);
                    if (con.State == System.Data.ConnectionState.Open)
                        await con.CloseAsync();
                    return 0;
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
            try
            {
                employers.Remove(GetEmployerByKey(key).ID);
                places.RemoveAll(x => x.EmployerKey == key);
                RecurringJob.RemoveIfExists($"{key}");
                using (var con = new MySqlConnection(BotProgram.ConnectionString))
                {
                    var command = new MySqlCommand($"DELETE FROM employers WHERE ikey = {key};", con);
                    await con.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                    command.CommandText = $"DELETE FROM jobs WHERE empkey = {key};";
                    await command.ExecuteNonQueryAsync();
                    await con.CloseAsync();
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        private async Task<bool> RestoreFromDatabase()
        {
            try
            {
                await UpdateJobPlaces();
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

                    foreach (var item in employers.Values)
                    {
                        RecurringJob.AddOrUpdate($"{item.Key}", () => item.Notify().Wait(), $"00 {item.TimeToNotify} * * *");
                    }

                    return true;
                }
            }
            catch (System.Data.Common.DbException ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        public string[] GetPlacesAsString()
        {
            List<string> list = new List<string>();


            if (places.Count == 0)
                return new string[1] { "Список работ пуст." };

            list.Add("Список работ:\n\n");

            foreach (var item in places)
            {
                list.Add($"{item.Key} | {item.EmployerName} | {item.PlaceName} | {item.Salary} | {item.Time.ToString()}\n");
            }



            return list.ToArray();
        }

        public string[] GetPlacesByEmployerKey(int key)
        {
            List<string> list = new List<string>();

            string name = GetEmployerByKey(key)?.Name;

            if (places.Count(x => x.EmployerName == name) == 0)
                return new string[1] { $"Список работ пуст у работника {name}." };

            list.Add($"Список работ {name}:\n\n");

            foreach (var item in places)
            {
                if (item.EmployerName == name)
                    list.Add($"{item.Key} | {item.PlaceName} | {item.Salary} | {item.Time.ToString()}\n");
            }

            return list.ToArray();
        }

        private async Task<List<JobPlace>> GetPlacesFromDatabase()
        {
            List<JobPlace> list = new List<JobPlace>();
            try
            {
                using (var con = new MySqlConnection(BotProgram.ConnectionString))
                {
                    var command = new MySqlCommand("SELECT * FROM jobs;", con);
                    await con.OpenAsync();
                    using (var reader = command.ExecuteReader())
                    {
                        while (await reader.ReadAsync())
                        {
                            JobPlace place = new JobPlace(reader.GetString("place"), reader.GetString("name"),
                                reader.GetFloat("salary"), reader.GetDateTime("DateOfWork"), reader.GetInt32("ikey"),
                                reader.GetInt32("empkey"));
                            list.Add(place);
                        }
                    }
                    await con.CloseAsync();
                    return list;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }

        public async Task UpdateJobPlaces()
        {
            List<JobPlace> list = await GetPlacesFromDatabase();

            await Task.Run(() =>
            {
                foreach (var item in list)
                {
                    if (places.Find(x => x.Key == item.Key) == null)
                        places.Add(item);
                }
            });
        }

        private void InitSenders(Telegram.Bot.TelegramBotClient sender)
        {
            foreach (var item in employers.Values)
            {
                item.InitSender(sender);
            }
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
