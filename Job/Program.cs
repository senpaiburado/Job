using System;
using Telegram.Bot;
using MySql.Data.MySqlClient;

namespace Job
{
    class Program
    {
        static void Main(string[] args)
        {
            string token = "";
            string connection = "Server=localhost;Uid=root;pwd=xjkfr2017;";
            using (var con = new MySqlConnection(connection))
            {
                con.Open();
                var command = new MySqlCommand("CREATE DATABASE IF NOT EXISTS Job;", con);
                command.ExecuteNonQuery();
                command.CommandText = "USE Job;";
                command.ExecuteNonQuery();
                command.CommandText = "CREATE TABLE IF NOT EXISTS employers (ikey INT PRIMARY KEY NOT NULL AUTO_INCREMENT, id BIGINT NOT NULL, name VARCHAR(100) NOT NULL, days INT NOT NULL DEFAULT 0, salary DECIMAL(15,2) NOT NULL DEFAULT 0.00, notify_time INT NOT NULL DEFAULT 18, confirmed BOOLEAN NOT NULL DEFAULT False);";
                command.ExecuteNonQuery();
                command.CommandText = "CREATE TABLE IF NOT EXISTS jobs (ikey INT PRIMARY KEY NOT NULL AUTO_INCREMENT, name VARCHAR(100) NOT NULL, place VARCHAR(100) NOT NULL, DateOfWork DATETIME NOT NULL, salary DECIMAL(15,2) NOT NULL);";
                command.ExecuteNonQuery();
                command = null;
                con.Close();
            }
            var bot = new BotProgram();
            bot.Start(token).Wait();
        }
    }
}