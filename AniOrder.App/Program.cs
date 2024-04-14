using AniOrder.Core;
using System.CommandLine;
using System.Diagnostics;
using System.Text;

namespace AniOrder.App;

public class Program
{
    private static async Task Run(string anilistUrlOrID, string file, string relationTypes, string mediaType)
    {
        int anilistID = -1;
        if (anilistUrlOrID.Contains("anilist.co/anime/"))
            anilistID = GetIDFromUrl(anilistUrlOrID, "anime");
        else if (anilistUrlOrID.Contains("anilist.co/manga/"))
            anilistID = GetIDFromUrl(anilistUrlOrID, "manga");
        else if (!int.TryParse(anilistUrlOrID, out anilistID))
            throw new Exception("Invalid Anilist URL/ID");

        string[] relations = relationTypes.Replace(" ", string.Empty).ToUpper().Split(',');
        ValidateRelations(relations);

        mediaType = mediaType.ToUpper();
        ValidateMediaType(mediaType);

#if DEBUG
        Log.Init(true, LogLevel.TRACE, "requests.log", true);
#else
        Log.Init(true, LogLevel.INFO);
#endif

        Core.AniOrder aniOrder = new(relations, false, mediaType);
        await aniOrder.Search(anilistID);
        const int pad = 15;
        List<Media> media = aniOrder.GetOrderedMedia();

        StringBuilder buffer = new();
        buffer.Append("Release:".PadRight(pad));
        buffer.AppendLine("Anime:");

        foreach (Media m in media)
        {
            buffer.Append((m.ReleaseDate == DateTime.MaxValue ? "Unreleased": m.ReleaseDate.ToString("MM/dd/yyyy")).PadRight(pad));
            buffer.AppendLine(m.Title.Romaji);
        }

        string result = buffer.ToString();
        Log.Write(result);

        if (!string.IsNullOrEmpty(file))
        {
            string? directory = Path.GetDirectoryName(file);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(file, result);
            Process.Start(new ProcessStartInfo()
            {
                FileName = file,
                UseShellExecute = true
            });
        }
    }

    private static int GetIDFromUrl(string url, string type)
    {
        url = url.Remove(0, url.IndexOf($"/{type}/") + $"/{type}/".Length);
        int slashIndex = url.IndexOf('/');
        url = url.Remove(slashIndex, url.Length - slashIndex);
        if (int.TryParse(url, out int id))
            return id;
        throw new Exception("Invalid Anilist ID");
    }

    private static void ValidateRelations(string[] relations)
    {
        string[] valid = {
            "ADAPTATION",
            "PREQUEL",
            "SEQUEL",
            "PARENT",
            "SIDE_STORY",
            "CHARACTER",
            "SUMMARY",
            "ALTERNATIVE",
            "SPIN_OFF",
            "OTHER"
        };

        foreach (string relation in relations)
        {
            if (!valid.Contains(relation))
                throw new Exception($"Invalid relation type: {relation}");
        }
    }

    private static void ValidateMediaType(string type)
    {
        string[] valid = {
            "ANY",
            "ANIME",
            "MANGA"
        };

        if (!valid.Contains(type))
            throw new Exception($"Invalid media type: {type}");
    }

    private static void SetupCommands(RootCommand rootCommand)
    {
        Argument<string> anilistArg = new("Anime/Manga", "Anilist URL or ID");
        Option<string> fileOption = new("--file", "The path to write the results of the search. If empty no file is created.");
        fileOption.AddAlias("-f");
        fileOption.SetDefaultValue(string.Empty);
        Option<string> relationsOption = new("--relations", "Types of relations allowed for searching, separated by commas. Allowed values: ADAPTATION, PREQUEL, SEQUEL, PARENT, SIDE_STORY, CHARACTER, SUMMARY, ALTERNATIVE, SPIN_OFF and OTHER");
        relationsOption.AddAlias("-r");
        relationsOption.SetDefaultValue("PREQUEL,SEQUEL,PARENT,SIDE_STORY,ALTERNATIVE,SPIN_OFF");
        Option<string> mediaTypesOption = new("--media-type", "The type of media allowed for searching. One of: ANY, MANGA, ANIME");
        mediaTypesOption.AddAlias("-mt");
        mediaTypesOption.SetDefaultValue("ANIME");

        rootCommand.AddArgument(anilistArg);
        rootCommand.AddOption(fileOption);
        rootCommand.AddOption(relationsOption);
        rootCommand.AddOption(mediaTypesOption);
        rootCommand.SetHandler(Run, anilistArg, fileOption, relationsOption, mediaTypesOption);
    }

    public static void Main(string[] args)
    {
        RootCommand root = new("AniOrder, get the release order of anime, manga and light novels from Anilist.");
        SetupCommands(root);
        root.Invoke(args);
    }
}
