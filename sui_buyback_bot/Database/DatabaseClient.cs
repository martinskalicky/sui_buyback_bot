using System;
using MySqlConnector;

namespace SkepyUniverseIndustry_DiscordBot.Database
{
    public class DatabaseClient
    {
        public MySqlConnection GetClient()
        {
            string connectionString = "server=localhost;userid=root;password=testingpass;database=sui";
            using var client = new MySqlConnection(connectionString);
            return client;
        }

        public bool OpenConnection(MySqlConnection client)
        {
            try
            {
                client.Open();
                return true;
            }
            catch (MySqlException ex)
            {
                switch (ex.Number)
                {
                    case 0:
                        Console.WriteLine("Cannot connect to server.");
                        break;
                        
                    case 1045:
                        Console.WriteLine("Invalid login credentials.");
                        break;
                }

                return false;
            }
        }
        

        public bool CloseConnection(MySqlConnection client)
        {
            try
            {
                client.Close();
                return true;
            }
            catch (MySqlException ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        public bool CheckVersion()
        {
            var client = GetClient();
            try
            {
                Console.WriteLine($"MySQL version : {client.ServerVersion}");
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }
        }
    }
}