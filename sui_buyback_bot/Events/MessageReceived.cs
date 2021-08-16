using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using SkepyUniverseIndustry_DiscordBot.Database;

namespace SkepyUniverseIndustry_DiscordBot.Events
{
    public class MessageReceived
    {
        private Dictionary<SocketUser, SocketMessage> _entrymessagesBuffer = new();
        private readonly Emoji _savedMessage = new Emoji("☑️");
        private readonly Emoji _okMessage = new Emoji("✅");
        private readonly Emoji _crossMessage = new Emoji("❌");

        public async Task MessageReceivedHandler(SocketMessage message)
        {
            List<string> authorizedList = DatabaseHandler.GetAdminList();
            if ((!message.Content.Contains("!") && authorizedList.Contains(message.Author.ToString())) || message.Content == "!c")
            {
                if (_entrymessagesBuffer.ContainsKey(message.Author))
                {
                    var rMessage = await message.Channel.GetMessageAsync(_entrymessagesBuffer[message.Author].Id);
                    try
                    {
                        if (rMessage.Content.Contains("!bb"))
                        {
                            AccountManager.AccountManager.ProcessBuyback(message, rMessage.Content.Split("!bb ")[1],
                                false);
                            return;
                        }

                        if (message.Content == "!c")
                        {
                            await message.AddReactionAsync(_savedMessage);
                            await message.Channel.SendMessageAsync("Account number was canceled.");
                            _entrymessagesBuffer.Remove(message.Author);
                            return;
                        }
                        else
                        {
                            AccountManager.AccountManager.ProcessBuyback(message, rMessage.Content, true);
                        }
                    }
                    catch (Exception ex)
                    {
                        await message.Channel.SendMessageAsync(ex.ToString());
                        await message.AddReactionAsync(_crossMessage);
                    }
                    finally
                    {
                        _entrymessagesBuffer.Remove(message.Author);
                    }
                    return;
                }
                try
                {
                    await message.AddReactionAsync(_savedMessage);
                    var accountNumber = Convert.ToInt32(message.Content);
                    int checkAccount = Convert.ToInt32(DatabaseHandler.CheckIfUserExists(accountNumber.ToString()));
                    if (checkAccount == accountNumber)
                    {
                        _entrymessagesBuffer.Add(message.Author, message);
                        await message.AddReactionAsync(_okMessage);
                    }
                    else
                    {
                        await message.AddReactionAsync(_crossMessage);
                        await message.Channel.SendMessageAsync("Account number does not exists, please provide valid account number.");
                    }

                    return;
                }
                catch {
                    await message.Channel.SendMessageAsync("Invalid account number, please provide valid account number.");
                    await message.AddReactionAsync(_crossMessage);
                    return;
                }
            }
            if (message.Content.Contains("!bb") && authorizedList.Contains(message.Author.ToString()))
            {
                try
                {
                    await message.AddReactionAsync(_savedMessage);
                    var accountNumber = Convert.ToInt32(message.Content.Split("!bb ")[1]);
                    int checkAccount = Convert.ToInt32(DatabaseHandler.CheckIfUserExists(accountNumber.ToString()));
                    if (checkAccount == accountNumber)
                    {
                        _entrymessagesBuffer.Add(message.Author, message);
                        await message.AddReactionAsync(_okMessage);
                    }
                    else
                    {
                        await message.AddReactionAsync(_crossMessage);
                        await message.Channel.SendMessageAsync("Account number does not exists, please provide valid account number.");
                    }
                    return;
                }
                catch {
                    await message.Channel.SendMessageAsync("Invalid account number, please provide valid account number.");
                    await message.AddReactionAsync(_crossMessage);
                    return;
                }
            }

            if (message.Content.Contains("!set") && authorizedList.Contains(message.Author.ToString()))
            {
                AccountManager.AccountManager.SetSettingsValue(message);
            }
            if (message.Content.Contains("!get") && authorizedList.Contains(message.Author.ToString()))
            {
                AccountManager.AccountManager.GetSettingsValue(message);
            }
            if (message.Content == "!balance")
            {
                AccountManager.AccountManager.GetActualBalanceFromRegisteredBy(message);
            }
            if (message.Content == "!corpbalance")
            {
                AccountManager.AccountManager.GetActualCorporationBalance(message);
            }
            if (message.Content == "!accountingcheck")
            {
                AccountManager.AccountManager.AccountingCheck(message);
            }
            if (message.Content.Contains("!admin"))
            {
                AccountManager.AccountManager.AdminController(message);
            }
            if (message.Content == "!buybackRate" || message.Content == "!buybackrate")
            {
                AccountManager.AccountManager.GetActualBuybackRate(message);
            }
            if (message.Content.Contains("!id"))
            {
                AccountManager.AccountManager.GetUserAccountIdFromRegisteredBy(message);
            }
            if (message.Content.Contains("!transfer"))
            {
                AccountManager.AccountManager.TransferMoneyFromRegisteredBy(message);
            }
            if (message.Content.Contains("!register") && !message.Content.Contains("EX: !register Martin Skalicky"))
            {

                if (message.Content.Equals("!register"))
                {
                    await message.AddReactionAsync(_crossMessage);
                    await message.Channel.SendMessageAsync(
                        "You need to provide character which will receive paycheck. EX: !register Martin Skalicky");
                    return;
                }
                await AccountManager.AccountManager.RegisterNewAccount(message);
                return;
            }
            if (message.Content.Contains("!jf"))
            {
                try
                {
                    string[] parseArgument = message.Content.Split(" ");
                    await message.AddReactionAsync(_savedMessage);
                    parseArgument[1] = parseArgument[1].Replace(",", ".");
                    var isNumeric = double.TryParse(parseArgument[1], out _);
                    if (isNumeric)
                    {
                        double m3 = Convert.ToDouble(parseArgument[1]);
                        var jfPriceForm3 =
                            Convert.ToDouble(DatabaseHandler.ExecuteScalar($"SELECT settings_value FROM settings WHERE settings_key = 'JF_BASIC_MOVE_PRICE'"));
                        double jfReturnPrice = m3 * jfPriceForm3;
                        await message.Channel.SendMessageAsync($"Price for {m3} m3 - JF from 85-B52 - ISKPrinter to Jita is {jfReturnPrice:n0} ISK");
                        await message.AddReactionAsync(_okMessage);
                        return;
                    }
                    await message.Channel.SendMessageAsync($"You entered {parseArgument[1]} which is not numeric value.");
                    await message.AddReactionAsync(_crossMessage);
                    return;
                }
                catch (IndexOutOfRangeException e)
                {
                    await message.Channel.SendMessageAsync(
                        "You are not using this command properly, please check help section.");
                    await message.AddReactionAsync(_crossMessage);
                }
            }

            if (message.Content == "!paylist")
            {
                AccountManager.AccountManager.GetPaychecksList(message);
            }
            if (message.Content.Contains("!paid"))
            {
                AccountManager.AccountManager.PaycheckCompleted(message);
            }

            if (message.Content == "!payme")
            {
                AccountManager.AccountManager.CreatePayout(message);
            }
            if (message.Content == "!help")
            {
                var builder = new EmbedBuilder();
                builder.WithTitle("(EVERYONE)");
                builder.AddField("!help", "Will display this window with all user commands. " +
                                          "\n EXAMPLE: !help");
                builder.AddField("!register name_of_eve_character",
                    "Creates corporate account. Be VERY carefull with name! You need just one account for all your characters. " +
                    "\n EXAMPLE: !register Martin Skalicky");
                builder.AddField("!id playername", "If you will not provide any name it will get back your ID." +
                                        "\n EXAMPLES: \n!id Martin Skalicky \n!id");
                builder.AddField("!balance", "Returns your actual ISK balance in corporate account." +
                                             "\n EXAMPLE: !balance");
                builder.AddField("!corpbalance", "Returns how much corporation needs to cover currency." +
                                             "\n EXAMPLE: !corpbalance");
                builder.AddField("!accountingcheck", "Checks if accounting is OK or NOT." +
                                                 "\n EXAMPLE: !accountingcheck");
                builder.AddField("!payme", "Creates order for management to pay you. " +
                                           "\n EXAMPLE: !payme");
                builder.AddField("!paylist", "Shows actual paychecks to be completed. \n EXAMPLE: !paylist");
                builder.AddField("!buybackRate", "Actual buyback rate. " +
                                                 "\n EXAMPLE: !buybackRate");
                builder.AddField("!transfer account_id isk_amount", "Transfer money to anybody in our corporation system. You can use M for millions and B for billions. " +
                                                                    "\n Number after !transfer is destination account number. Following examples will transfer same money." +
                                                                    "\n EXAMPLES: \n!transfer 2 0.1M \n!transfer 2 0.001B \n!transfer 2 1000000");
                builder.AddField("!jf amount_of_m3", "Calculates cost for JF from our HQ to Jita.");
                builder.WithColor(Color.Gold);
                await message.Channel.SendMessageAsync("", false, builder.Build());
                
                var authorizedBuilder = new EmbedBuilder();
                authorizedBuilder.WithTitle("(AUTHORIZED)");
                authorizedBuilder.AddField("!bb accountNumber", "Will create buyback without fleet inicialization. " +
                                                                "\n EXAMPLE: !bb 1");
                authorizedBuilder.AddField("!c", "Cancels previous entry of account number. \n EXAMPLE: !c");
                authorizedBuilder.AddField("PARSING ITEMS after !bb", "Items that are going to be buybacked. " +
                                                                      "\n EXAMPLE: Veldspar    1");
                authorizedBuilder.AddField("!set variableKey variableValue", "Sets variables (buyback rate, etc). " +
                                                                             "\n EXAMPLE: !set BUYBACK_GLOBAL_RATE 0.80");
                authorizedBuilder.AddField("accountNumber", "Checks for account number if exists and create/reuse fleet. " +
                                                            "\n EXAMPLE: 1");
                authorizedBuilder.AddField("PARSING ITEMS after accountNumber", "Adds items to the account and to the active fleet. " +
                    "\n EXAMPLE: Veldspar    1");
                authorizedBuilder.AddField("!paid accountNumber", "Completes paycheck of specified account number. \n EXAMPLE: !paid 1");
                authorizedBuilder.AddField("!admin add accountNumber", "Will add user as ADMIN in bot. \n EXAMPLE: !admin add 2");
                authorizedBuilder.AddField("!admin remove accountNumber", "Will remove user as ADMIN in bot. \n EXAMPLE: !admin remove 2");
                authorizedBuilder.WithColor(Color.Red);
                await message.Channel.SendMessageAsync("", false, authorizedBuilder.Build());
            }
        }
    }
}