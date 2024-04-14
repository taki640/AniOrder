using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AniOrder.Core;

public class AniOrder
{
    private const string REQUEST_URI = "https://graphql.anilist.co";
    private const string ANIME_QUERY_DATA =
    """
    query ($id: Int) {
      Media(id: $id) {
        id
        title {
          romaji
          english
          native
        }
        startDate {
          year
          month
          day
        }
        endDate {
          year
          month
          day
        }
        relations {
          edges {
            node {
              id
              type
            }
            relationType
          }
        }
      }
    }
    """;

    private readonly HttpClient m_Client;
    private readonly Dictionary<int, Media> m_Media = new();
    private readonly List<int> m_FailedIDs = new();
    private readonly string[] m_ValidRelations;         // Relations: ADAPTATION, PREQUEL, SEQUEL, PARENT, SIDE_STORY, CHARACTER, SUMMARY, ALTERNATIVE, SPIN_OFF, OTHER
    private readonly bool m_UseEndDateAsReleaseDate;
    private readonly string m_MediaType;                // Types: ANY, ANIME, MANGA
#if DEBUG
    private int m_Count = 0;
#endif

    public AniOrder(string[] validRelations, bool useEndDateAsReleaseDate, string mediaType)
    {
        m_ValidRelations = validRelations;
        m_UseEndDateAsReleaseDate = useEndDateAsReleaseDate;
        m_MediaType = mediaType;
        m_Client = new HttpClient() { BaseAddress = new Uri(REQUEST_URI) };
    }

    public async Task Search(int mediaID)
    {
        Log.WriteLine($"Requesting media data for ID '{mediaID}'");
#if DEBUG
        Log.WriteLine($"Request count: {++m_Count}", LogLevel.TRACE);
        if (m_Count > 10)
            return;
#endif

        JObject json = await RequestMediaData(mediaID);
        Log.WriteLine($"Media JSON:\n{json.ToString(Formatting.Indented)}", LogLevel.TRACE);
    
        Media? media = DeserializeMedia(json);
        if (media == null)
        {
            m_FailedIDs.Add(mediaID);
            return;
        }

        m_Media.Add(mediaID, media);

        List<int> relations = GetRelationIDs(json);
        Log.WriteLine($"Resolving {relations.Count} relations for media '{mediaID}' ('{media.Title.Romaji}')");
        foreach (int relation in relations)
        {
            if (m_Media.ContainsKey(relation))
            {
                Log.WriteLine($"Relation '{relation}' already resolved, skipping");
                continue;
            }
            await Search(relation);
        }

        Log.WriteLine();
        await RetryFailedIDs();
    }

    public List<Media> GetOrderedMedia()
    {
        List<Media> media = m_Media.Values.ToList();
        media.Sort((x, y) => DateTime.Compare(x.ReleaseDate, y.ReleaseDate));
        return media;
    }

    private async Task RetryFailedIDs()
    {
        if (m_FailedIDs.Count > 0)
        {
            List<int> failedIDs = m_FailedIDs;
            m_FailedIDs.Clear();
            Log.WriteLine($"\nRetrying {failedIDs.Count} failed IDs");
            foreach (int id in failedIDs)
            {
                if (m_Media.ContainsKey(id))
                {
                    Log.WriteLine($"Relation '{id}' already requested, skipping");
                    continue;
                }
                await Search(id);
            }

            Log.WriteLine();
        }
    }

    private async Task<JObject> RequestMediaData(int mediaID)
    {
        string json = $"{{ \"query\": \"{ANIME_QUERY_DATA.ReplaceLineEndings(string.Empty)}\", \"variables\": {{ \"id\": {mediaID} }} }}";

        HttpRequestMessage requestMessage = new()
        {
            Method = HttpMethod.Post,
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        using HttpResponseMessage responseMessage = await m_Client.SendAsync(requestMessage);
        string content = await responseMessage.Content.ReadAsStringAsync();
        await CheckRateLimit(responseMessage.Headers);
        return JObject.Parse(content);
    }

    private static async Task CheckRateLimit(HttpResponseHeaders headers)
    {
        if (headers.Contains("Retry-After"))
        {
            int waitTime = int.Parse(headers.GetValues("Retry-After").First());
            Log.WriteLine($"Rate limited... Waiting {waitTime} seconds...");
            await Task.Delay(TimeSpan.FromSeconds(waitTime + 1));
            return;
        }

        if (!headers.Contains("X-RateLimit-Remaining"))   // if these header is not found wait a bit and try to get in the next request because reasons
        {
            Log.WriteLine($"Waiting 5 seconds...", LogLevel.TRACE);
            await Task.Delay(TimeSpan.FromSeconds(5));
            return;
        }

        int rateLimitRemaining = int.Parse(headers.GetValues("X-RateLimit-Remaining").First());
        Log.WriteLine($"Rate limit remaining: {rateLimitRemaining}", LogLevel.TRACE);

        if (rateLimitRemaining == 0)
        {
            int waitTime = 63;
            Log.WriteLine($"Rate limited... Waiting {waitTime} seconds...");
            await Task.Delay(TimeSpan.FromSeconds(waitTime + 1));
        }
    }

#pragma warning disable CS8602, CS8604, CS8600  // null reference warnings
    private Media? DeserializeMedia(JObject json)
    {
        JToken? media;
        try { media = json["data"]["Media"]; }
        catch { return null; }

        JToken titleToken = media["title"];
        JToken dateToken = media[m_UseEndDateAsReleaseDate ? "endDate" : "startDate"];

        Media.Titles title = new()
        {
            Romaji = titleToken["romaji"].Value<string>(),
            Native = titleToken["native"].Value<string>(),
            English = titleToken["english"].Value<string>()
        };

        int? year = dateToken["year"].Value<int?>();
        int? month = dateToken["month"].Value<int?>();
        int? day = dateToken["day"].Value<int?>();
        DateTime releaseDate = year == null || month == null || day == null ? DateTime.MaxValue : new(year.Value, month.Value, day.Value);
        return new Media()
        {
            Title = title,
            ReleaseDate = releaseDate
        };
    }

    private List<int> GetRelationIDs(JObject json)
    {
        JEnumerable<JToken> edges = json["data"]["Media"]["relations"]["edges"].Children();
        List<int> ids = new();
        foreach (JToken edge in edges)
        {
            string relationType = edge["relationType"].Value<string>()!;
            JToken node = edge["node"];
            if (m_ValidRelations.Contains(relationType) && (m_MediaType == "ANY" || node["type"].Value<string>() == m_MediaType))
                ids.Add(edge["node"]["id"].Value<int>());
        }

        return ids;
    }
#pragma warning restore CS8602, CS8604, CS8600

    private static string GetHeaderValue(HttpResponseHeaders responseHeaders, string header)
    {
        foreach (string value in responseHeaders.GetValues(header))
            return value;
        throw new Exception($"Header \"{header}\" was not found");
    }
}
