using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Discord.WebSocket;

namespace SkepyUniverseIndustry_DiscordBot.Utilities
{
    public static class ItemsOperation
    {
        private static Dictionary<string, double> CompressIfPossible(Dictionary<string, double> items)
        {
            string[] iceList = {"Blue Ice", "Clear Icide", "Dark Glitter", "Enriched Clear Icicle", "Gelidus",
                "Glare Crust", "Krystallos", "Pristine White Glaze", "Smoots Glacial Mass", "Thick Blue Ice", "White Glaze"};   
            List<string> iceRange = new List<string>(iceList);
            Dictionary<string, double> compressedItems = new Dictionary<string, double>();
            foreach (var item in items)
            {
                if (CheckIfCouldBeCompressed(item.Key))
                {
                    double compression = 100;
                    if (iceRange.Contains(item.Key)) compression = 1;
                    if (item.Value >= compression)
                    {
                        string newItem = "Compressed " + item.Key;
                        double resultCompressionValue = item.Value / compression;
                        compressedItems.TryAdd(newItem, resultCompressionValue);
                        continue;
                    }
                }

                if (compressedItems.ContainsKey(item.Key))
                {
                    items.TryGetValue(item.Key, out double amountStored);
                    compressedItems[item.Key] = item.Value + amountStored;
                    continue;
                }
                compressedItems.Add(item.Key, item.Value);
            }

            return compressedItems;
        }
        public static Dictionary<string, double> GetItemsFromInput(SocketMessage processMessage)
        {
            string separator = "    ";
            string[] rows = processMessage.Content.Split('\n');
            if (processMessage.Attachments.Any())
            {
                rows = GetFromAttachment(processMessage);
                separator = "\t";
            }
            if (!processMessage.Attachments.Any()) {
                rows = processMessage.Content.Split('\n');
                separator = "    ";

            }


            Dictionary<string, double> itemsParsed = new Dictionary<string, double>();
            foreach (var row in rows)
            {
                string[] items = row.Split(separator);
                if (itemsParsed.ContainsKey(items[0]))
                {
                    itemsParsed.TryGetValue(items[0], out double amountStored);
                    itemsParsed[items[0]] = amountStored + long.Parse(items[1].Replace(",",""));
                }
                else
                {
                    itemsParsed.Add(items[0], double.Parse(items[1].Replace(",", "")));
                }
            }
            
            return CompressIfPossible(itemsParsed);
        }
        private static string[] GetFromAttachment(SocketMessage processMessage)
        {
            var file = processMessage.Attachments.First().Url;
            WebClient myWebClient = new WebClient();
            byte[] buffer = myWebClient.DownloadData(file);
            string download = Encoding.UTF8.GetString(buffer);
            string[] lines = download.Split("\n");
            return lines;
        }
        private static bool CheckIfCouldBeCompressed(string itemName)
        {
            string tryCompressKey = "Compressed " + itemName;
            double getPrice = Market.Market.GetBuyPrice(tryCompressKey);
            if (getPrice != 0) return true;
            return false;
        }
    }
}