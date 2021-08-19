using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using MySqlConnector;
using SkepyUniverseIndustry_DiscordBot.Database;
using SkepyUniverseIndustry_DiscordBot.Utilities;

namespace SkepyUniverseIndustry_DiscordBot.AccountManager
{
    public static class AccountManager
    {
        static MySqlConnection dbClient = new(Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING"));
        private static readonly Emoji SavedMessage = new Emoji("☑️");
        private static readonly Emoji OkMessage = new Emoji("✅");
        private static readonly Emoji CrossMessage = new Emoji("❌");

        public static async Task RegisterNewAccount(SocketMessage message)
        {
            await message.AddReactionAsync(SavedMessage);
            var result = DatabaseHandler.RegisterUser(message);
            if (result)
            {
                await message.AddReactionAsync(OkMessage);
                return;
            }
            await message.AddReactionAsync(CrossMessage);
        }

        public static async void CreatePayout(SocketMessage message)
        {
            //TODO: Validation
            await message.AddReactionAsync(SavedMessage);
            int minimumPaycheckCreateDays = Convert.ToInt32(DatabaseHandler.ExecuteScalar("SELECT settings_value FROM settings WHERE settings_key = 'BUYBACK_MIN_DAYS'"));
            var actualBalance = DatabaseHandler.ExecuteScalar($"SELECT balance FROM users WHERE registered_by = '{message.Author}'");
            var datetimeResult = DatabaseHandler.ExecuteScalar(
                $"SELECT created_date_time FROM payments WHERE registered_by = '{message.Author}' ORDER BY created_date_time DESC LIMIT 1");
            if (datetimeResult != string.Empty)
            {
                DateTime lastPaycheckCreated = Convert.ToDateTime(DatabaseHandler.ExecuteScalar(
                    $"SELECT created_date_time FROM payments WHERE registered_by = '{message.Author}' ORDER BY created_date_time DESC LIMIT 1"));
                if (lastPaycheckCreated > DateTime.Now.AddDays(minimumPaycheckCreateDays))
                {
                    await message.AddReactionAsync(CrossMessage);
                    await message.Channel.SendMessageAsync(
                        $"Last payment created: {lastPaycheckCreated}. Limit is one paycheck per {minimumPaycheckCreateDays} days.");
                    return;                
                }
            }
            if (actualBalance == "0")
            {
                await message.AddReactionAsync(CrossMessage);
                await message.Channel.SendMessageAsync(
                    $"We were not able to create your request since you do have 0 ISK balance.");
                return;                
            }
            var checkIfExists = DatabaseHandler.CheckPayCheck(message);
            if ((long)checkIfExists > 0)
            {
                await message.AddReactionAsync(CrossMessage);
                await message.Channel.SendMessageAsync(
                    $"We were not able to create your request since you do have one waiting.");
                return;
            }
            
            //TODO: transaction
            var result = DatabaseHandler.CreatePayCheck(message);
            if (result)
            {
                await message.AddReactionAsync(OkMessage);
                await message.Channel.SendMessageAsync(
                    $"Payment request was created, please expect it can take some time.");
                return;
            }
            await message.AddReactionAsync(CrossMessage);
            await message.Channel.SendMessageAsync($"We were not able to create your paycheck. No data affected.");
            //TODO: Error handling
        }

        public static async void GetPaychecksList(SocketMessage message)
        {
            ulong total = 0;
            var builder = new EmbedBuilder();
            await message.AddReactionAsync(SavedMessage);
            builder.WithTitle("Current paychecks waiting to complete.");

            List<Tuple<string, ulong, int>> results = DatabaseHandler.GetPaylist();
            foreach (var rows in results)
            {
                builder.AddField($"{rows.Item3} - {rows.Item1}", $"{rows.Item2:n0} ISK");
                total += rows.Item2;
            }
            builder.WithColor(Color.Gold);
            builder.WithFooter($"TOTAL: {total:n0} ISK.");
            await message.Channel.SendMessageAsync("", false, builder.Build());
            await message.AddReactionAsync(OkMessage);
        }
        public static void PaycheckCompleted(SocketMessage message)
        {
            dbClient.Open();
            MySqlTransaction transaction = dbClient.BeginTransaction();
            try
            {
                message.AddReactionAsync(SavedMessage);
                var authorizedList = DatabaseHandler.GetAdminList();
                if (authorizedList.Contains(message.Author.ToString()))
                {
                    string[] parseMessage = message.Content.Split(" ");
                    //TODO: check if string is empty (no paycheck)
                    double amount =
                        Convert.ToDouble(DatabaseHandler.ExecuteScalar(
                            $"SELECT payments.payment_number FROM payments INNER JOIN users ON users.registered_by = payments.registered_by WHERE payments.status != 'PAID' AND users.id = {parseMessage[1]}"));
                    message.AddReactionAsync(SavedMessage);

                    if (DatabaseHandler.ExecuteNonQueryTransaction($"UPDATE payments INNER JOIN users ON payments.registered_by = users.registered_by " +
                                                                   $"SET payments.status = 'PAID' " +
                                                                   $"WHERE users.id = '{parseMessage[1]}' AND payments.status = 'NEW'",dbClient,transaction) &&
                    DatabaseHandler.CreateTransactionsInProcessing(dbClient, transaction, message,parseMessage[1], "1", amount))
                    {
                        message.AddReactionAsync(OkMessage);
                        transaction.Commit();
                        return;
                    }
                    return;
                }
                message.Channel.SendMessageAsync($"You are not in authorized list!");
                message.AddReactionAsync(CrossMessage);
            }
            catch (Exception e)
            {
                transaction.Rollback();
                message.Channel.SendMessageAsync(
                    $"You do have permissions, however database writing failed. No data affected. Propably nothing to paid with this account number.");
                message.AddReactionAsync(CrossMessage);
            }
            finally
            {
                dbClient.Close();
            }
        }

        public static async void GetActualBalanceFromRegisteredBy(SocketMessage message)
        {
            await message.AddReactionAsync(SavedMessage);
            var balance = DatabaseHandler.GetUsersDatabaseFieldRegisteredBy(message.Author.ToString(), "balance");
            if (balance == null)
            {
                await message.AddReactionAsync(CrossMessage);
                await message.Channel.SendMessageAsync($"We were not able to get your balance, please check if you are registered or contact leadership for assistance.");
                return;
            }
            await message.Channel.SendMessageAsync($"You have currently: {balance:n0} ISK on corporation account.");
            await message.AddReactionAsync(OkMessage);
        }
        public static async void GetActualCorporationBalance(SocketMessage message)
        {
            await message.AddReactionAsync(SavedMessage);
            var balance = DatabaseHandler.ExecuteScalar("SELECT balance FROM users WHERE id = '1'");
            if (balance == null)
            {
                await message.AddReactionAsync(CrossMessage);
                await message.Channel.SendMessageAsync($"We were not able to get corporation balance, check if instalation is valid.");
                return;
            }
            await message.Channel.SendMessageAsync($"Corporation needs: {Convert.ToInt64(balance.Replace("-", "")):n0} ISK to cover corporation currency.");
            await message.AddReactionAsync(OkMessage);
        }
        public static async void AccountingCheck(SocketMessage message)
        {
            var builder = new EmbedBuilder();
            builder.WithTitle("Corporation Accounting");
            builder.WithColor(Color.Red);
            await message.AddReactionAsync(SavedMessage);
            long membersTotalBalance = 0;
            var corporationBalance = Convert.ToInt64(DatabaseHandler.ExecuteScalarObject("SELECT balance FROM users WHERE id = 1"));
            var membersBalance = DatabaseHandler.ExecuteReaderInt32Int64("SELECT id, balance FROM users WHERE id != 1");
            foreach (var record in membersBalance)
            {
                membersTotalBalance = membersTotalBalance + Convert.ToInt64(record.Item2);
            }
            builder.AddField("Corporation Balance", $"{corporationBalance:n0}");
            builder.AddField("Members Balance", $"{membersTotalBalance:n0}");
            var corpMinusMembers = corporationBalance + membersTotalBalance;
            if (corpMinusMembers == 0)
            {
                builder.WithFooter("Result is OK.");
                await message.AddReactionAsync(OkMessage);
                await message.Channel.SendMessageAsync("", false, builder.Build());
                return;
            }
            builder.WithFooter($"Result is WRONG. {corpMinusMembers:n0}");
            await message.Channel.SendMessageAsync("", false, builder.Build());
            await message.AddReactionAsync(CrossMessage);
        }
        public static async void AdminController(SocketMessage message)
        {
            try
            {
                string[] parseMessage = message.Content.Split(" ");
                if (parseMessage[1] == "add")
                {
                    await message.AddReactionAsync(SavedMessage);
                    if (DatabaseHandler.ExecuteNonQuery($"UPDATE users SET privileges = 'ADMIN' WHERE id = '{parseMessage[2]}'"))
                    {
                        await message.AddReactionAsync(OkMessage);
                        string registereBy =
                            DatabaseHandler.ExecuteScalar(
                                $"SELECT registered_by FROM users WHERE id = '{parseMessage[2]}'");
                        await message.Channel.SendMessageAsync($"User {registereBy} was added as admin.");
                        return;
                    }
                    await message.Channel.SendMessageAsync($"Failed to add account number {parseMessage[2]} as admin.");
                    await message.AddReactionAsync(CrossMessage);
                    return;
                }
                if (parseMessage[1] == "remove")
                {
                    await message.AddReactionAsync(SavedMessage);
                    if (DatabaseHandler.ExecuteNonQuery($"UPDATE users SET privileges = null WHERE id = '{parseMessage[2]}'"))
                    {
                        string registereBy =
                            DatabaseHandler.ExecuteScalar(
                                $"SELECT registered_by FROM users WHERE id = '{parseMessage[2]}'");
                        await message.AddReactionAsync(OkMessage);
                        await message.Channel.SendMessageAsync($"User {registereBy} was removed as admin.");
                        return;
                    }
                    await message.Channel.SendMessageAsync($"Failed to remove account number {parseMessage[2]} as admin.");
                    await message.AddReactionAsync(CrossMessage);
                    return;
                }
                await message.AddReactionAsync(CrossMessage);
                await message.Channel.SendMessageAsync($"Invalid input, please check help for correct one.");
            }
            catch (Exception e)
            {
                await message.AddReactionAsync(CrossMessage);
                await message.Channel.SendMessageAsync($"{e.Message}");
            }
        }

        public static async void GetUserAccountIdFromRegisteredBy(SocketMessage message)
        {
            string[] input = message.Content.Split("!id ");
            await message.AddReactionAsync(SavedMessage);
            try
            {
                string id;
                if (input.Length > 1)
                {
                    id = DatabaseHandler.ExecuteScalar($"SELECT id FROM users where paycheck_name = '{input[1]}'");
                }
                else
                {
                    id = DatabaseHandler.ExecuteScalar($"SELECT id FROM users where registered_by = '{message.Author}'");
                }

                if (id == string.Empty)
                {
                    await message.Channel.SendMessageAsync($"Looks like there is no account with name {input[1]}");
                    await message.AddReactionAsync(CrossMessage);
                    return;
                }

                if (input.Length > 1)
                {
                    await message.Channel.SendMessageAsync($"ID for {input[1]} name is {id}.");
                }
                else
                {
                    await message.Channel.SendMessageAsync($"Your ID is {id}.");
                }
                await message.AddReactionAsync(OkMessage);
            }
            catch (Exception e)
            {
                await message.AddReactionAsync(CrossMessage);
                await message.Channel.SendMessageAsync($"We were not able to get your id, please check if you are registered or contact leadership for assistance.");
                await message.Channel.SendMessageAsync(e.ToString());
                Console.WriteLine(e);
                throw;
            }
        }
        public static void TransferMoneyFromRegisteredBy(SocketMessage message)
        {            
            MySqlConnection client = new MySqlConnection(Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING"));
            MySqlCommand commands = client.CreateCommand();
            MySqlTransaction transaction;

            client.Open();
            transaction = client.BeginTransaction();
            commands.Connection = client;
            commands.Transaction = transaction;
            
            try
            {
                message.AddReactionAsync(SavedMessage);
                double amountToTransfer;
                string[] parseMessage = message.Content.Split(" ");
                string accountIdToTransferTo = parseMessage[1];
                parseMessage[2] = parseMessage[2].Replace(",", ".");
                parseMessage[2] = parseMessage[2].Replace("m", "M");
                parseMessage[2] = parseMessage[2].Replace("b", "B");
                if (parseMessage[2].Contains("B"))
                {
                    string[] parseAmount = parseMessage[2].Split("B");
                    string amountParsed = parseAmount[0];
                    amountToTransfer = Convert.ToDouble(amountParsed) * 1000000000;

                }
                else if (parseMessage[2].Contains("M"))
                {
                    string[] parseAmount = parseMessage[2].Split("M");
                    string amountParsed = parseAmount[0];
                    amountToTransfer = Convert.ToDouble(amountParsed) * 1000000;
                }
                else
                {
                    amountToTransfer = Convert.ToInt64(parseMessage[2]);
                }

                var testTargetAccountId =
                    Convert.ToInt32(DatabaseHandler.GetUsersDatabaseField(accountIdToTransferTo, "id"));
                var testSourceBalance =
                    DatabaseHandler.GetUsersDatabaseFieldRegisteredBy(message.Author.ToString(), "balance");
                var paycheckName =
                    DatabaseHandler.ExecuteScalar(
                        $"SELECT paycheck_name FROM users WHERE id = '{accountIdToTransferTo}'");
                if (paycheckName == string.Empty)
                {
                    message.Channel.SendMessageAsync(
                        $"Account ID {accountIdToTransferTo} is not registered with any account");
                    message.AddReactionAsync(CrossMessage);
                    return;
                }

                if (testTargetAccountId > 0 && (long) testSourceBalance - amountToTransfer >= 0)
                {
                    message.AddReactionAsync(OkMessage);
                    if (DatabaseHandler.CreateTransactionsInProcessing(client, transaction, message,
                        DatabaseHandler.GetUsersDatabaseFieldRegisteredBy(message.Author.ToString(), "id").ToString(),
                        accountIdToTransferTo, amountToTransfer))
                    {
                        message.Channel.SendMessageAsync(
                            $"Transfer to account {accountIdToTransferTo}: {paycheckName} with {amountToTransfer:n0} ISK was completed.");
                        transaction.Commit();
                    }
                }
                else
                {
                    message.AddReactionAsync(CrossMessage);
                    message.Channel.SendMessageAsync("You do not have enough ISKs to make a transfer.");
                }
            }
            catch (Exception e)
            {
                if (e is IndexOutOfRangeException)
                {
                    message.Channel.SendMessageAsync(
                        "You are not using this command properly, check help for assistance");
                    message.AddReactionAsync(CrossMessage);
                    return;
                }
                transaction.Rollback();
                message.AddReactionAsync(CrossMessage);
                message.Channel.SendMessageAsync(
                    $"{e}");
            }
            finally
            {
                client.Close();
            }
        }
        public static void SetSettingsValue(SocketMessage message)
        {
            message.AddReactionAsync(SavedMessage);
            try
            {
                string[] parsing = message.Content.Split(" ");
                if (DatabaseHandler.ExecuteNonQuery($"UPDATE settings SET settings_value = '{parsing[2]}'WHERE settings_key = '{parsing[1]}'"))
                {
                    message.Channel.SendMessageAsync($"We were able to edit settings: {parsing[1]} with value {parsing[2]}");
                    message.AddReactionAsync(OkMessage);
                    return;
                }
                message.Channel.SendMessageAsync($"We were NOT able to edit settings: {parsing[1]} with value {parsing[2]}");
                message.AddReactionAsync(CrossMessage);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
        public static void GetSettingsValue(SocketMessage message)
        {
            var builder = new EmbedBuilder();
            message.AddReactionAsync(SavedMessage);
            builder.WithTitle("Current settings.");
            builder.WithColor(Color.Gold);
            try
            {
                string[] parsing = message.Content.Split(" ");
                var settings = DatabaseHandler.ExecuteReader($"SELECT settings_key, settings_value FROM settings");
                foreach (var tuple in settings)
                {
                    builder.AddField($"{tuple.Item1}",$"{tuple.Item2}");
                }
                builder.WithFooter($"To change values use !set command.");
                message.Channel.SendMessageAsync("", false, builder.Build());
                message.AddReactionAsync(OkMessage);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
        public static void GetActualBuybackRate(SocketMessage message)
        {
            message.AddReactionAsync(SavedMessage);
            try
            {
                string[] parsing = message.Content.Split(" ");
                var buybackRate =
                    Convert.ToDouble(DatabaseHandler.ExecuteScalar(
                        "SELECT settings_value FROM settings WHERE settings_key = 'BUYBACK_GLOBAL_RATE'"));
                message.Channel.SendMessageAsync($"Current global rate is: {buybackRate*100} %");
                message.AddReactionAsync(OkMessage);
            }
            catch (Exception e)
            {
                message.Channel.SendMessageAsync($"We were NOT able to get buyback rate.");
                message.AddReactionAsync(CrossMessage);
                Console.WriteLine(e);
                throw;
            }
        }
        
        public static bool CreateFleet(SocketMessage message)
        {
            if (DatabaseHandler.CreateFleet(message.Author.ToString()))
            {
                message.Channel.SendMessageAsync($"Fleet was created under name {message.Author}");
                return true;
            }
            message.Channel.SendMessageAsync("Failed to create fleet");
            return false;
        }

        public static bool EndFleet(SocketMessage message)
        {
            if (DatabaseHandler.EndFleet(message.Author.ToString()))
            {
                message.Channel.SendMessageAsync($"Fleet was successfully closed for name {message.Author}.");
                return true;
            }
            message.Channel.SendMessageAsync("Failed to end fleet!");
            return false;
        }
        public static DateTime CheckActiveFleet(SocketMessage processMessage)
        {
            return DatabaseHandler.CheckActive(processMessage.Author.ToString());
        }
        private static bool CheckForEndOfFleet(string inputAccountId, SocketMessage message)
        {
            var accountId = DatabaseHandler.GetAccountIdFromRegisteredBy(message.Author.ToString());
            if (inputAccountId == accountId)
            {
                return true;
            }

            return false;
        }
        
        private static Dictionary<string, double> RemoveOreGatheredFromFleet(Dictionary<string, double> items, SocketMessage message)
        {
            dbClient.Open();
            MySqlCommand commandsRemoveFromFleet = dbClient.CreateCommand();
            var transactionRemoveFromFleet = dbClient.BeginTransaction();
            
            commandsRemoveFromFleet.Transaction = transactionRemoveFromFleet;
            commandsRemoveFromFleet.Connection = transactionRemoveFromFleet.Connection;

            var fleetId = DatabaseHandler.GetActiveFleetId(message.Author.ToString());
            var itemsFromFleet = DatabaseHandler.GetItemsFromFleet(fleetId, dbClient, transactionRemoveFromFleet);
            try
            {
                foreach (var fleetItems in itemsFromFleet)
                {
                    if (items.ContainsKey(fleetItems.Key))
                    {
                        items.TryGetValue(fleetItems.Key, out double amountStored);
                        items[fleetItems.Key] = amountStored - fleetItems.Value;
                    }
                    else
                    {
                        message.Channel.SendMessageAsync(
                            $"You are missing {fleetItems.Key} quantity {fleetItems.Value}");
                        message.AddReactionAsync(CrossMessage);
                        transactionRemoveFromFleet.Rollback();
                    }

                    if (items[fleetItems.Key] < 0)
                    {
                        message.Channel.SendMessageAsync(
                            $"You are missing {fleetItems.Key} quantity {fleetItems.Value}");
                        throw new Exception(
                            message: $"You are trying to save less items that you gathered from people.");
                    }
                }

                if (DatabaseHandler.UpdateItemsFromFleet(fleetId, dbClient, transactionRemoveFromFleet))
                {
                    transactionRemoveFromFleet.Commit();
                }
            }
            catch (Exception e)
            {
                if (e is KeyNotFoundException)
                {
                    message.Channel.SendMessageAsync(
                        $"We need to gather all items that you collected from fleet members. Please try again.");
                    return null;
                }
                transactionRemoveFromFleet.Rollback();
                throw;
            }
            finally
            {
                dbClient.Close();
            }

            return items;
        }

        public static void ProcessBuyback(SocketMessage message, string accountNumber, bool isFleetBuyback)
        {
            if (isFleetBuyback)
            {
                var activeFleetDatetime = CheckActiveFleet(message);
                if (activeFleetDatetime == default)
                {
                    try
                    {
                        CreateFleet(message);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        throw;
                    }
                }
                else
                {
                    message.Channel.SendMessageAsync($"Working with fleet from: {activeFleetDatetime}");
                }
                
            }
            Dictionary<string, double> items = null;
            var builder = new EmbedBuilder();
            var paycheckName = DatabaseHandler.GetUsersDatabaseField(accountNumber, "paycheck_name");
            double totalIsk = 0;
            builder.WithColor(Color.Gold);
            builder.WithTitle($"Imported items on account - {accountNumber} ({paycheckName})");
            
            try
            {
                items = ItemsOperation.GetItemsFromInput(message);
                if (CheckForEndOfFleet(accountNumber, message))
                {
                    items = RemoveOreGatheredFromFleet(items, message);
                }
                foreach (var item in items)
                {
                    if (isFleetBuyback && !CheckForEndOfFleet(accountNumber, message))
                    {
                        DatabaseHandler.InsertFleetInputDataFromDiscord(message.Author.ToString(), accountNumber,
                            item.Key, item.Value);
                    }
                    DatabaseHandler.InsertInputDataFromDiscord(
                            message.Author.ToString(), 
                            accountNumber, 
                            item.Key, 
                            item.Value);
                }

                if (CheckForEndOfFleet(accountNumber, message))
                {
                    EndFleet(message);
                }
                message.AddReactionAsync(SavedMessage);
            }
            catch (Exception ec)
            {
                if (ec is NullReferenceException)
                {
                    message.Channel.SendMessageAsync("Removing items you gathered from fleet failed. NO DATA, were saved.");
                    message.AddReactionAsync(CrossMessage);
                    return;
                }
                message.Channel.SendMessageAsync($"Parsing Error detailed message below.");
                message.AddReactionAsync(CrossMessage);
                message.Channel.SendMessageAsync(ec.Message);
            }

            bool buildFields = true;
            dbClient.Open();
            MySqlCommand commands = dbClient.CreateCommand();
            var transaction = dbClient.BeginTransaction();
            try
            {
                double globalBuybackRate = Convert.ToDouble(DatabaseHandler.ExecuteScalar(
                    "SELECT settings_value FROM settings WHERE settings_key = 'BUYBACK_GLOBAL_RATE'"));
                double usedBuybackRate = globalBuybackRate;
                var unprocessedItems = DatabaseHandler.GetUnprocessedItems(accountNumber, dbClient, transaction);
                foreach (var item in unprocessedItems)
                {
                    double priceForUnit = Market.Market.GetBuyPrice(item.Key);
                    totalIsk = totalIsk + item.Value * priceForUnit *  usedBuybackRate;
                    commands.Transaction = transaction;
                    commands.Connection = transaction.Connection;
                    
                    commands.CommandText = $"INSERT INTO processed_data(created_by, account_number, status, item_name, item_quantity, item_price) " +
                                           $"VALUES('{message.Author}','{accountNumber}','NEW', '{item.Key.Replace("'", "''")}', '{item.Value}', '{item.Value * priceForUnit *  usedBuybackRate}')";
                    try
                    {
                        DatabaseHandler.ExecuteNonQueryTransaction(commands.CommandText, dbClient, transaction);

                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        throw;
                    }

                    if (builder.Fields.Count() <= 24 && buildFields)
                    {
                        builder.AddField($"{item.Key} - {item.Value}x",
                            $"{Math.Ceiling(item.Value * priceForUnit * usedBuybackRate):n0} ISK, {priceForUnit * usedBuybackRate:n0} ISK/Piece");
                        continue;
                    }

                    builder.Fields.Clear();
                    buildFields = false;
                }

                if (DatabaseHandler.UpdateUnprocessedItems(accountNumber, dbClient, transaction) && 
                    DatabaseHandler.CreateTransactionsInProcessing(dbClient, transaction, message, "1", accountNumber, totalIsk) &&
                    DatabaseHandler.UpdateProcessedData(accountNumber, dbClient, transaction))
                {
                    //AccountFrom FROM might be differ, should be 1 on production.
                    transaction.Commit();
                    builder.WithColor(Color.Gold);
                    builder.WithFooter($"TOTAL: {Math.Ceiling(totalIsk):n0} ISK. BUYBACK: {usedBuybackRate*100} %.");
                    message.Channel.SendMessageAsync("", false, builder.Build());
                    message.AddReactionAsync(OkMessage);
                }
            }
            catch (Exception e)
            {
                transaction.Rollback();
                Console.WriteLine(e);
                throw;
            }
            finally
            {
                dbClient.Close();
            }
        }
    }
}