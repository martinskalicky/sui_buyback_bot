using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using Discord.WebSocket;
using MySqlConnector;

namespace SkepyUniverseIndustry_DiscordBot.Database
{
    public class DatabaseHandler
    {
        public static bool RegisterUser(SocketMessage message)
        {
            string[] paycheckName = message.Content.Split("!register ");
            MySqlConnection client = new MySqlConnection(Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING"));
            client.Open();
            var sqlQuery = $"INSERT INTO users(registered_by, paycheck_name, status) VALUES('{message.Author}', '{paycheckName[1]}', 'ACTIVE') ";
            var command = new MySqlCommand(sqlQuery, client);
            try
            {
                command.ExecuteNonQuery();
                message.Channel.SendMessageAsync($"New Account was created. Use balance or id commands to get more details."); ;
                return true;
            }
            catch (MySqlException ex)
            {
                if (ex.Message.Contains("Duplicate entry"))//TODO: Change text for exception number
                {

                    message.Channel.SendMessageAsync("You already registered use !balance for details.");
                    return false;

                }
                else
                {
                    message.Channel.SendMessageAsync(
                        "Unexpected error, please contact Martin Skalicky to resolve.");
                    return false;
                }
            }
            finally
            {
                client.CloseAsync();
            }
        }//:TODO method for execution instead of logic in those (scalar, NQ, Reader. BT)
        public static bool CreateFleet(string registeredBy)
        {
            MySqlConnection client = new MySqlConnection(Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING"));
            client.Open();
            var sqlCommand = $"INSERT INTO fleets(fleet_started_by) " +
                             $"VALUES('{registeredBy}')";
            var command = new MySqlCommand(sqlCommand, client);
            try
            {
                command.ExecuteNonQuery();
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }
        }
        public static bool EndFleet(string registeredBy)
        {
            MySqlConnection client = new MySqlConnection(Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING"));
            client.Open();
            var sqlCommand = $"UPDATE fleets SET status = 'ENDED' WHERE fleet_started_by = '{registeredBy}'";
            var command = new MySqlCommand(sqlCommand, client);
            try
            {
                command.ExecuteNonQuery();
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }
        }
        public static DateTime CheckActive(string registeredBy)
        {
            MySqlConnection client = new MySqlConnection(Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING"));
            client.Open();
            var sqlCommand = $"SELECT fleet_started FROM fleets WHERE fleet_started_by = '{registeredBy}' AND status = 'NEW'";
            var command = new MySqlCommand(sqlCommand, client);
            try
            {
                var test = command.ExecuteScalar();
                return Convert.ToDateTime(command.ExecuteScalar());
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return default;
        }
        public static string GetActiveFleetId(string registeredBy)
        {
            MySqlConnection client = new MySqlConnection(Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING"));
            client.Open();
            var sqlCommand = $"SELECT id FROM fleets WHERE fleet_started_by = '{registeredBy}' AND status = 'NEW'";
            var command = new MySqlCommand(sqlCommand, client);
            try
            {
                var test = command.ExecuteScalar();
                return Convert.ToString(command.ExecuteScalar());
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                client.Close();
            }

            return default;
        }
        public static bool UpdateItemsFromFleet(string fleetId, MySqlConnection client, MySqlTransaction transaction)
        {
            MySqlCommand commands = client.CreateCommand();

            commands.Connection = client;
            commands.Transaction = transaction;

            commands.CommandText = $"UPDATE input_data_fleets SET status = 'PROCESSED'" +
                                   $"WHERE status = 'NEW' AND id = '{fleetId}'";
            try
            {
                var reader = commands.ExecuteNonQuery();
                if (reader >= 1)
                {
                    return true;
                }
            }
            catch (Exception e)
            {
                transaction.Rollback();
                return false;
                throw;
            }
            return false;
        }
        public static Dictionary<string, double> GetItemsFromFleet(string fleetId, MySqlConnection client, MySqlTransaction transaction)
        {
            Dictionary<string, double> fleetItems = new Dictionary<string, double>();
            //MySqlConnection client = new MySqlConnection(Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING"));
            MySqlCommand commands = client.CreateCommand();

            commands.Connection = client;
            commands.Transaction = transaction;

            commands.CommandText = $"SELECT item_name, item_quantity FROM input_data_fleets " +
                                   $"WHERE status = 'NEW' AND id = '{fleetId}'";
            try
            {
                var reader = commands.ExecuteReader();
                if (reader.HasRows)
                {
                    while (reader.Read())
                    {
                        if (fleetItems.ContainsKey(reader.GetString(0)))
                        {
                            fleetItems.TryGetValue(reader.GetString(0), out double amountStored);
                            fleetItems[reader.GetString(0)] = amountStored + reader.GetDouble(1);
                        }
                        else
                        {
                            fleetItems.Add(reader.GetString(0), reader.GetDouble(1));
                        }
                    }
                }
                reader.Close();

                return fleetItems;
            }
            catch (Exception e)
            {
                transaction.Rollback();
                throw;
            }
        }
        public static string GetAccountIdFromRegisteredBy(string registeredBy)
        {
            MySqlConnection client = new MySqlConnection(Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING"));
            client.Open();
            var sqlCommand = $"SELECT id FROM users WHERE registered_by = '{registeredBy}' AND status = 'ACTIVE'";
            var command = new MySqlCommand(sqlCommand, client);
            try
            {
                return Convert.ToString(command.ExecuteScalar());
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                client.Close();
            }

            return default;
        }
        public static int InsertInputDataFromDiscord(string registeredBy, string accountNumber, string inputItemName, double inputItemQuantity)
        {
            inputItemName = inputItemName.Replace("'", "''");
            MySqlConnection client = new MySqlConnection(Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING"));
            client.Open();
            var sqlCommand = $"INSERT INTO input_data(created_by, account_number, input_item_name, input_item_quantity) " +
                             $"VALUES('{registeredBy}','{accountNumber}', '{inputItemName}', '{inputItemQuantity}')";
            var command = new MySqlCommand(sqlCommand, client);
            try
            {
                return command.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            finally
            {
                client.Close();
            }
        }
        public static int InsertFleetInputDataFromDiscord(string fleetStartedBy, string accountNumber, string inputItemName, double inputItemQuantity)
        {
            MySqlConnection client = new MySqlConnection(Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING"));
            client.Open();
            var sqlCommandFleetId = $"SELECT id FROM fleets WHERE fleet_started_by = '{fleetStartedBy}' AND status = 'NEW'";
            var fleetIdCmd = new MySqlCommand(sqlCommandFleetId, client);
            var fleetId = fleetIdCmd.ExecuteScalar();
            inputItemName = inputItemName.Replace("'", "''");
            var sqlCommand = $"INSERT INTO input_data_fleets(id, account_number, item_name, item_quantity) " +
                             $"VALUES('{fleetId}','{accountNumber}', '{inputItemName}', '{inputItemQuantity}')";
            var command = new MySqlCommand(sqlCommand, client);
            return command.ExecuteNonQuery();
        }
        public static object GetUsersDatabaseFieldRegisteredBy(string registeredBy, string databaseField)
        {
            MySqlConnection client = new MySqlConnection(Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING"));
            client.Open();
            var sqlCommand = $"SELECT {databaseField} FROM users WHERE registered_by = '{registeredBy}' LIMIT 1";
            var command = new MySqlCommand(sqlCommand, client);
            return command.ExecuteScalar();
        }
        public static object GetUsersDatabaseField(string accountId, string databaseField)
        {
            MySqlConnection client = new MySqlConnection(Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING"));
            client.Open();
            var sqlCommand = $"SELECT {databaseField} FROM users WHERE id = '{accountId}'";
            var command = new MySqlCommand(sqlCommand, client);
            return command.ExecuteScalar();
        }
        public static object CheckIfUserExists(string accountId)
        {
            MySqlConnection client = new MySqlConnection(Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING"));
            client.Open();
            var sqlCommand = $"SELECT id FROM users WHERE id = '{accountId}'";
            var command = new MySqlCommand(sqlCommand, client);
            return command.ExecuteScalar();
        }
        
        public static object CheckPayCheck(SocketMessage message)
        {
            MySqlConnection client = new MySqlConnection(Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING"));
            client.Open();
            var sqlCommand = $"SELECT COUNT(payment_id) FROM sui.payments WHERE registered_by = '{message.Author}' AND status = 'NEW'";
            var command = new MySqlCommand(sqlCommand, client);
            return command.ExecuteScalar();
        }
        public static bool CreatePayCheck(SocketMessage message)
        {
            MySqlConnection client = new MySqlConnection(Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING"));
            MySqlCommand commands = client.CreateCommand();
            MySqlTransaction transaction;

            client.Open();
            transaction = client.BeginTransaction();
            commands.Connection = client;
            commands.Transaction = transaction;

            var selectPaycheckValue = $"SELECT balance FROM users WHERE registered_by = '{message.Author}'";
            var selectPaycheckCommand = new MySqlCommand(selectPaycheckValue, client);
            selectPaycheckCommand.Transaction = transaction;
            var stringPaycheckValue = selectPaycheckCommand.ExecuteScalar();

            try
            {
                commands.CommandText = $"INSERT INTO payments(registered_by, status, payment_number) VALUES('{message.Author}', 'NEW', '{stringPaycheckValue}') ";
                commands.ExecuteNonQuery();
                transaction.Commit();
                return true;
            }
            catch (Exception e)
            {
                try//TODO: remove t/c in t/c
                {
                    transaction.Rollback();
                    return true;

                }
                catch (MySqlException ex)
                {
                    if (transaction.Connection != null)//TODO: X
                    {
                        Console.WriteLine($"Exception of type {ex.GetType()} occured when trying to rollback.");
                    }
                }

                Console.WriteLine($"Exception of type {e.GetType()} occured.");
                Console.WriteLine("Nothing was written to database.");
                Console.WriteLine(e);
                return false;
            }
            finally
            {
                client.Close();
            }
        }
        public static List<Tuple<string, ulong, int>> GetPaylist()
        {
            var returnPaylist = new List<Tuple<string, ulong, int>>();
            MySqlConnection client = new MySqlConnection(Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING"));
            client.Open();
            var sqlCommand = $"SELECT users.paycheck_name, payments.payment_number, users.id FROM users " +
                             $"INNER JOIN payments " +
                             $"ON payments.registered_by = users.registered_by " +
                             $"WHERE payments.status = 'NEW'";
            var command = new MySqlCommand(sqlCommand, client);
            var reader = command.ExecuteReader();
            if (reader.HasRows)
            {
                while (reader.Read())
                {
                    returnPaylist.Add(new Tuple<string, ulong, int>(reader.GetString(0), reader.GetUInt64(1), reader.GetInt32(2)));
                }
            }
            return returnPaylist;
        }
        public static List<string> GetAdminList()
        {
            List<string> authList = new List<string>();
            MySqlConnection client = new MySqlConnection(Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING"));
            client.Open();
            var sqlCommand = $"SELECT registered_by FROM users WHERE privileges = 'ADMIN'";
            var command = new MySqlCommand(sqlCommand, client);
            var authorizedList = command.ExecuteReader();
            if (authorizedList.HasRows)
            {
                while (authorizedList.Read())
                {
                    authList.Add(authorizedList.GetString(0));
                }
            }
            return authList;
        }
        public static Dictionary<string, double> GetUnprocessedItems(string accountNumber, MySqlConnection client, MySqlTransaction transaction)
        {
            Dictionary<string, double> unprocessedItems = new Dictionary<string, double>();
            //MySqlConnection client = new MySqlConnection(Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING"));
            MySqlCommand commands = client.CreateCommand();

            commands.Connection = client;
            commands.Transaction = transaction;

            commands.CommandText = $"SELECT input_item_name, input_item_quantity FROM input_data " +
                             $"WHERE status = 'NEW' AND account_number = '{accountNumber}'";
            try
            {
                var reader = commands.ExecuteReader();
                if (reader.HasRows)
                {
                    while (reader.Read())
                    {
                        if (unprocessedItems.ContainsKey(reader.GetString(0)))
                        {
                            unprocessedItems.TryGetValue(reader.GetString(0), out double amountStored);
                            unprocessedItems[reader.GetString(0)] = amountStored + reader.GetDouble(1);
                        }
                        else
                        {
                            unprocessedItems.Add(reader.GetString(0), reader.GetDouble(1));
                        }
                    }
                }
                reader.Close();

                return unprocessedItems;
            }
            catch (Exception e)
            {
                transaction.Rollback();
                throw;
            }
        }
        public static bool UpdateUnprocessedItems(string accountNumber, MySqlConnection client, MySqlTransaction transaction)
        {
            MySqlCommand commands = client.CreateCommand();

            commands.Connection = client;
            commands.Transaction = transaction;

            commands.CommandText = $"UPDATE input_data SET status = 'PROCESSED' " +
                                   $"WHERE status = 'NEW' AND account_number = '{accountNumber}'";
            try
            {
                var reader = commands.ExecuteNonQuery();
                if (reader >= 1)
                {
                    return true;
                }

                return false;
            }
            catch (Exception e)
            {
                transaction.Rollback();
                return false;
            }
        }
        public static bool UpdateProcessedData(string accountNumber, MySqlConnection client, MySqlTransaction transaction)
        {
            MySqlCommand commands = client.CreateCommand();

            commands.Connection = client;
            commands.Transaction = transaction;

            commands.CommandText = $"UPDATE processed_data SET status = 'PROCESSED' " +
                                   $"WHERE status = 'NEW' AND account_number = '{accountNumber}'";
            try
            {
                var reader = commands.ExecuteNonQuery();
                if (reader >= 1)
                {
                    return true;
                }

                return false;
            }
            catch (Exception e)
            {
                transaction.Rollback();
                return false;
            }
        }
        public static bool TransferMoney(string accountFrom, string accountTo, double amount)
        {
            //TODO: Check dependency injection if that will work.
            //TODO: close all connections explicitely
            
            MySqlConnection client = new MySqlConnection(Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING"));
            MySqlCommand commands = client.CreateCommand();
            MySqlTransaction transaction;

            client.Open();
            transaction = client.BeginTransaction();
            commands.Connection = client;
            commands.Transaction = transaction;

            try
            {
                commands.CommandText = $"UPDATE users SET balance = balance - {amount} WHERE id = '{accountFrom}'";
                commands.ExecuteNonQuery();
                commands.CommandText = $"INSERT";
                commands.ExecuteNonQuery();
                commands.CommandText = $"UPDATE users SET balance = balance - {amount} WHERE id = '{accountFrom}'";
                commands.ExecuteNonQuery();
                commands.CommandText = $"UPDATE users SET balance = balance + {amount} WHERE id = '{accountTo}'";
                commands.ExecuteNonQuery();
                transaction.Commit();
                return true;
            }
            catch (Exception e)
            {
                try //TODO: t/c in t/c 
                {
                    transaction.Rollback();
                    return true;

                }
                catch (MySqlException ex)
                {
                    Console.WriteLine($"Exception of type {ex.GetType()} occured when trying to rollback.");
                }

                Console.WriteLine($"Exception of type {e.GetType()} occured.");
                Console.WriteLine("Nothing was written to database.");
                Console.WriteLine(e);
                return false;
            }
            finally
            {
                client.Close();
            }
        }
        public static bool CreateTransactionsInProcessing(MySqlConnection client, MySqlTransaction transaction, SocketMessage message,  string accountFrom, string accountTo, double amount)
        {
            //TODO: Check dependency injection if that will work.
            //TODO: close all connections explicitely

            MySqlCommand commands = client.CreateCommand();
            
            commands.Connection = client;
            commands.Transaction = transaction;

            try
            {
                commands.CommandText = $"SELECT balance_after FROM transactions WHERE account_number_to = {accountTo} ORDER BY id DESC LIMIT 1";
                double balanceTo = Convert.ToDouble(commands.ExecuteScalar());
                commands.CommandText = $"SELECT balance_after FROM transactions WHERE account_number_to = {accountFrom} ORDER BY id DESC LIMIT 1";
                double balanceFrom = Convert.ToDouble(commands.ExecuteScalar());
                commands.CommandText = $"INSERT INTO transactions(created_by, account_number_from, account_number_to, amount, balance_after) " +
                                       $"VALUES('{message.Author}', '{accountFrom}', '{accountTo}', '{amount}', '{balanceTo+amount}' )";
                commands.ExecuteNonQuery();
                commands.CommandText = $"INSERT INTO transactions(created_by, account_number_from, account_number_to, amount, balance_after) " +
                                       $"VALUES('{message.Author}', '{accountTo}', '{accountFrom}', '-{amount}', '{balanceFrom-amount}' )";
                commands.ExecuteNonQuery();
                commands.CommandText = $"UPDATE users SET balance = balance + {amount} WHERE id = '{accountTo}'";
                commands.ExecuteNonQuery();
                commands.CommandText = $"UPDATE users SET balance = balance - {amount} WHERE id = '{accountFrom}'";
                commands.ExecuteNonQuery();
                return true;
            }
            catch (Exception e)
            {
                try //TODO: t/c in t/c 
                {
                    transaction.Rollback();
                    return false;

                }
                catch (MySqlException ex)
                {
                    Console.WriteLine($"Exception of type {ex.GetType()} occured when trying to rollback.");
                }

                Console.WriteLine($"Exception of type {e.GetType()} occured.");
                Console.WriteLine("Nothing was written to database.");
                Console.WriteLine(e);
                return false;
            }
        }

        public static string ExecuteScalar(string sqlCommand)
        {
            {
                MySqlConnection client = new MySqlConnection(Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING"));
                client.Open();
                var command = new MySqlCommand(sqlCommand, client);
                try
                {
                    var value = command.ExecuteScalar();
                    if (value != null)
                    {
                        return Convert.ToString(command.ExecuteScalar());
                    }

                    return "";
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
        }
        public static object ExecuteScalarObject(string sqlCommand)
        {
            {
                MySqlConnection client = new MySqlConnection(Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING"));
                client.Open();
                var command = new MySqlCommand(sqlCommand, client);
                try
                {
                    var value = command.ExecuteScalar();
                    if (value != null)
                    {
                        return command.ExecuteScalar();
                    }

                    return "";
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
        }
        public static List<Tuple<string, string>> ExecuteReader(string sqlCommand)
        {
            {
                MySqlConnection client = new MySqlConnection(Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING"));
                client.Open();
                var returnData = new List<Tuple<string, string>>();
                var command = new MySqlCommand(sqlCommand, client);
                try
                {
                    var reader = command.ExecuteReader();
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            returnData.Add(new Tuple<string, string>(reader.GetString(0), reader.GetString(1)));
                        }
                    }
                    reader.Close();
                    return returnData;

                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
                finally
                {
                    client.Close();
                }
            }
        }
        public static List<Tuple<int, long>> ExecuteReaderInt32Int64(string sqlCommand)
        {
            {
                MySqlConnection client = new MySqlConnection(Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING"));
                client.Open();
                var returnData = new List<Tuple<int, long>>();
                var command = new MySqlCommand(sqlCommand, client);
                try
                {
                    var reader = command.ExecuteReader();
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            returnData.Add(new Tuple<int, long>(reader.GetInt32(0), reader.GetInt64(1)));
                        }
                    }
                    reader.Close();
                    return returnData;

                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
                finally
                {
                    client.Close();
                }
            }
        }
        public static bool ExecuteNonQuery(string sqlCommand)
        {
            {
                MySqlConnection client = new MySqlConnection(Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING"));
                client.Open();
                var command = new MySqlCommand(sqlCommand, client);
                try
                {
                    var result = command.ExecuteNonQuery();
                    if (result >= 1)
                    {
                        return true;
                    }

                    return false;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
                finally
                {
                    client.Close();
                }
            }
        }
        public static bool ExecuteNonQueryTransaction(string sqlCommand, MySqlConnection client, MySqlTransaction transaction)
        {
            {
                MySqlCommand command = client.CreateCommand();
                command.Connection = client;
                command.Transaction = transaction;
                command.CommandText = sqlCommand;
                try
                {
                    var result = command.ExecuteNonQuery();
                    if (result >= 1)
                    {
                        return true;
                    }

                    return false;
                }
                catch (Exception e)
                {
                    transaction.Rollback();
                    Console.WriteLine(e);
                    throw;
                }
            }
        }
    }
}