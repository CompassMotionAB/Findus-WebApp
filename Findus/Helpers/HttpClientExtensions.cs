using System.Net;
using System.Threading.Tasks;
using System.Net.Http;

namespace Findus.Helpers {
    public static class HttpClientExtensions {
        public static async Task<byte[]> FetchFile(this HttpClient httpClient,string fileLink)
        {
            var uri = WebUtility.HtmlDecode(fileLink);
            var response = await httpClient.GetAsync(uri);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync();
        }
    }
}