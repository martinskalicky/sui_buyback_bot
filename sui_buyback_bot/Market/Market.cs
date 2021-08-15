using System.IO;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace SkepyUniverseIndustry_DiscordBot.Market
{
    public static class Market
    {
        private static JToken GetItemId(string itemName)
        {
            var url = $@"https://www.fuzzwork.co.uk/api/typeid.php?typename={itemName}";

            var request = (HttpWebRequest) WebRequest.Create(url);
            using var response = (HttpWebResponse) request.GetResponse();
            using var stream = response.GetResponseStream();
            using var reader = new StreamReader(stream);
            var jsonResponse = reader.ReadToEnd();
            return JToken.Parse(jsonResponse);
        }

        public static double GetBuyPrice(string itemName)
        {
            var itemId = GetItemId(itemName);
            var typeId = itemId.SelectToken("typeID")?.ToString();
            var url = $"https://market.fuzzwork.co.uk/aggregates/?station=60003760&types={typeId}";

            HttpWebRequest request = (HttpWebRequest) WebRequest.Create(url);
            using var response = (HttpWebResponse) request.GetResponse();
            using var stream = response.GetResponseStream();
            using var reader = new StreamReader(stream);
            var jsonResponse = reader.ReadToEnd();
            var itemDescription = JToken.Parse(jsonResponse);
            var fivePercent = itemDescription.SelectToken("$..buy.median");
            return (double) fivePercent;
        }
    }
}