using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using ChatSharp;
using RedditSharp;
using RedditSharp.Things;
using System.IO;
using ChatSharp.Events;
using Newtonsoft.Json;
using System.Web;
using Newtonsoft.Json.Linq;

namespace worm
{
    partial class MainClass
    {
        static IrcClient client;
        static Config config;
        static Regex urlRegex = new Regex("((([A-Za-z]{3,9}:(?:\\/\\/)?)(?:[-;:&=\\+\\$,\\w]+@)?[A-Za-z0-9.-]+|(?:www.|[-;:&=\\+\\$,\\w]+@)[A-Za-z0-9.-]+)((?:\\/[\\+~%\\/.\\w-_]*)?\\??(?:[-\\+=&;%@.\\w_]*)#?(?:[\\w]*))?)");
        static Regex sourceRegex = new Regex("\\[(?<source>.*)\\]");
        static Reddit reddit;
        static Subreddit awwnime;
        static List<string> moeList;

        public static void Main(string[] args)
        {
            var configFile = "config.json";
            if (args.Length == 1)
                configFile = args[0];
            config = new Config();
            if (File.Exists(configFile))
                JsonConvert.PopulateObject(File.ReadAllText(configFile), config);
            else
            {
                File.WriteAllText(configFile, JsonConvert.SerializeObject(config, Formatting.Indented));
                return;
            }
            File.WriteAllText(configFile, JsonConvert.SerializeObject(config, Formatting.Indented));

            Console.WriteLine("Logging into Reddit...");
            reddit = new Reddit(config.RedditUser, config.RedditPassword);
            awwnime = reddit.GetSubreddit("/r/awwnime");
            moeList = new List<string>();
            LoadMoe();
            Console.WriteLine("Done.");

            client = new IrcClient(config.Network, new IrcUser(config.Nick, config.User, config.Password, config.RealName));
            client.RawMessageSent += (s, e) => Console.WriteLine(">> {0}", e.Message);
            client.RawMessageRecieved += (s, e) => Console.WriteLine("<< {0}", e.Message);
            client.ConnectionComplete += (s, e) =>
            {
                foreach (var channel in config.Channels)
                    client.JoinChannel(channel);
            };
            client.ChannelMessageRecieved += HandleMessage;
            client.ConnectAsync();
            
            while (true) Thread.Sleep(100);
        }
        
        public static void Reply(PrivateMessageEventArgs e, string text, params string[] args)
        {
            client.SendMessage(e.PrivateMessage.User.Nick + ": " + string.Format(text, args), e.PrivateMessage.Source);
        }

        public static void HandleMessage(object sender, PrivateMessageEventArgs e)
        {
            var matches = urlRegex.Matches(e.PrivateMessage.Message);
            if (e.PrivateMessage.Message.StartsWith("."))
            {
                var command = e.PrivateMessage.Message.Substring(1);
                var parameters = new string[0];
                if (command.Contains(" "))
                {
                    parameters = command.Split(' ').Skip(1).ToArray();
                    command = command.Remove(command.IndexOf(' '));
                }
                if (config.Admins.Any(a => e.PrivateMessage.User.Match(a)))
                    HandleAdminMessage(e, command, parameters);
                switch (command)
                {
                    case "ping":
                        Reply(e, "pong!");
                        break;
                    case "hug":
                        client.SendAction("hugs " + e.PrivateMessage.User.Nick, e.PrivateMessage.Source);
                        break;
                    case "moe":
                        Post moe;
                        if (parameters.Length == 0)
                            moe = awwnime.Hot.FirstOrDefault(p => !p.IsSelfPost && !moeList.Contains(p.FullName));
                        else
                            moe = awwnime.Search(string.Join(",", parameters)).FirstOrDefault(p => !p.IsSelfPost && !moeList.Contains(p.FullName));
                        if (moe == null)
                        {
                            Reply(e, "I'm all out of moe! Sorry :(");
                            break;
                        }
                        var source = sourceRegex.Match(moe.Title);
                        if (!source.Success)
                            Reply(e, "Here's a cute picture: {0}", moe.Url.ToString());
                        else
                        {
                            var s = source.Groups["source"].Value;
                            if (s.ToUpper() == "ORIGINAL")
                                Reply(e, "Here's a cute original picture: {0}", moe.Url.ToString());
                            else
                                Reply(e, "Here's a cute picture from {0}: {1}", source.Groups["source"].Value, moe.Url.ToString());
                        }
                        moeList.Add(moe.FullName);
                        SaveMoe();
                        break;
                    case "kos":
                    case "api":
                    case "kernel":
                        SearchKOS(e, parameters);
                        break;
                    case "yt":
                    case "youtube":
                        HandleYouTube(e, parameters);
                        break;
                    case "g":
                    case "google":
                    case "search":
                        HandleSearch(e, string.Join(" ", parameters));
                        break;
                    case "w":
                    case "wiki":
                    case "wikipedia":
                        HandleSearch(e, "site:en.wikipedia.org " + string.Join(" ", parameters));
                        break;
                    case "xkcd":
                        HandleSearch(e, "site:xkcd.com " + string.Join(" ", parameters));
                        break;
                    case "bbt":
                    case "baka":
                    case "bakabt":
                        HandleSearch(e, "site:bakabt.me " + string.Join(" ", parameters));
                        break;
                    case "nyaa":
                        HandleSearch(e, "site:nyaa.se " + string.Join(" ", parameters));
                        break;
                    case "bluba":
                    case "bulbapedia":
                    case "poke":
                        HandleSearch(e, "site:bulbapedia.bulbagarden.net " + string.Join(" ", parameters));
                        break;
                }
            }
            else
            {
                foreach (Match match in matches)
                {
                    Uri uri;
                    if (!Uri.TryCreate(match.Value, UriKind.Absolute, out uri))
                        Uri.TryCreate("http://" + match.Value, UriKind.Absolute, out uri);
                    if (uri != null)
                    {
                        if (uri.Scheme == "http" || uri.Scheme == "https")
                        {
                            // Handle URL
                            switch (uri.Host)
                            {
                                case "youtube.com":
                                case "www.youtube.com":
                                case "youtu.be":
                                case "www.youtu.be":
                                    HandleYouTube(e, new string[] { uri.ToString() });
                                    break;
                                default:
                                    ShowTitle(e, uri);
                                    break;
                            }
                        }
                    }
                }
            }
        }

        public static void HandleAdminMessage(PrivateMessageEventArgs e, string command, string[] parameters)
        {
        }

        static void LoadMoe()
        {
            if (File.Exists("moe.json"))
            {
                var json = JArray.Parse(File.ReadAllText("moe.json"));
                foreach (var item in json)
                {
                    moeList.Add(item.Value<string>());
                }
            }
        }

        static void SaveMoe()
        {
            var moe = "[" + string.Join(",", moeList.Select(m => "\"" + m + "\"")) + "]";
            File.WriteAllText("moe.json", moe);
        }

        static void SearchKOS(PrivateMessageEventArgs e, string[] parameters)
        {
            var webClient = new WebClient();
            var json = JObject.Parse(webClient.DownloadString("http://www.knightos.org/documentation/reference/data.json"));
            var functions = json.Children<JProperty>().SelectMany(c => ((JObject)c.Value).Properties());
            var results = functions.Where(f => f.Name.ToUpper() == string.Join(" ", parameters).ToUpper());
            if (!results.Any())
                Reply(e, "Can't find that function.");
            else
            {
                var item = results.First().Value as JObject;
                client.SendMessage(string.Format("{0}: {1}", results.First().Name,
                    new string(item["description"].Value<string>().Take(100).ToArray())), e.PrivateMessage.Source);
                if (item["sections"] != null && item["sections"]["Inputs"] != null)
                {
                    var inputs = new List<string>();
                    foreach (var _input in item["sections"]["Inputs"])
                    {
                        var input = (JProperty)_input;
                        var name = input.Name;
                        var use = input.Value.Value<string>();
                        if (use.Length > 25)
                            use = new string(use.Take(25).ToArray()) + "...";
                        inputs.Add(name + ": " + use);
                    }
                    client.SendMessage("Inputs: " + string.Join("; ", inputs.ToArray()), e.PrivateMessage.Source);
                }
                if (item["sections"] != null && item["sections"]["Outputs"] != null)
                {
                    var outputs = new List<string>();
                    foreach (var _input in item["sections"]["Outputs"])
                    {
                        var input = (JProperty)_input;
                        var name = input.Name;
                        var use = input.Value.Value<string>();
                        if (use.Length > 25)
                            use = new string(use.Take(25).ToArray()) + "...";
                        outputs.Add(name + ": " + use);
                    }
                    client.SendMessage("Outputs: " + string.Join("; ", outputs.ToArray()), e.PrivateMessage.Source);
                }
                client.SendMessage("Source: " + item["path"].Value<string>() + ", line " +
                    item["line"].Value<int>(), e.PrivateMessage.Source);
            }
        }

        static void HandleSearch(PrivateMessageEventArgs e, string terms)
        {
            var results = DoGoogleSearch(terms);
            if (!results.Any())
                Reply(e, "No results.");
            else
                Reply(e, results.First());
        }

        static void HandleYouTube(PrivateMessageEventArgs e, string[] parameters)
        {
            string vid;
            if (parameters.Length != 1)
            {
                var results = DoGoogleSearch("site:youtube.com " + string.Join(" ", parameters));
                if (results.Count == 0)
                {
                    Reply(e, "No results found.");
                    return;
                }
                else
                    vid = results.First().Substring(results.First().LastIndexOf("http://"));
            }
            else
                vid = parameters[0];
            if (!vid.StartsWith("http://") && !vid.StartsWith("https://"))
                vid = "http://" + vid;
            if (Uri.IsWellFormedUriString(vid, UriKind.Absolute))
            {
                Uri uri = new Uri(vid);
                if (uri.Host == "youtu.be" || uri.Host == "www.youtu.be")
                    vid = uri.LocalPath.Trim('/');
                else
                {
                    var query = HttpUtility.ParseQueryString(uri.Query);
                    vid = query["v"];
                }
                if (vid == null)
                {
                    Reply(e, "Video not found.");
                    return;
                }
            }
            var video = GetYoutubeVideo(vid);
            if (video == null)
            {
                Reply(e, "Video not found.");
                return;
            }

            string partOne = "\"\u0002" + video.Title + "\u000f\" [" +
                video.Duration.ToString("m\\:ss") + "] (\u000312" + video.Author + "\u000f)\u000303 " +
                (video.HD ? "HD" : "SD");

            string partTwo = video.Views.ToString("N0", CultureInfo.InvariantCulture) + " views";
            if (video.RatingsEnabled)
                partTwo += ", " +
                    "(+\u000303" + video.Likes.ToString("N0", CultureInfo.InvariantCulture) +
                    "\u000f|-\u000304" + video.Dislikes.ToString("N0", CultureInfo.InvariantCulture) + "\u000f) [" + video.Stars + "]";

            if (video.RegionLocked | !video.CommentsEnabled || !video.RatingsEnabled)
            {
                partTwo += " ";
                if (video.RegionLocked)
                    partTwo += "\u000304Region locked\u000f, ";
                if (!video.CommentsEnabled)
                    partTwo += "\u000304Comments disabled\u000f, ";
                if (!video.RatingsEnabled)
                    partTwo += "\u000304Ratings disabled\u000f, ";
                partTwo = partTwo.Remove(partTwo.Length - 3);
            }

            if (partOne.Length < partTwo.Length)
                partOne += "\u000f " + video.VideoUri.ToString();
            else
                partTwo += "\u000f " + video.VideoUri.ToString();

            client.SendMessage(partOne, e.PrivateMessage.Source);
            client.SendMessage(partTwo, e.PrivateMessage.Source);
        }

        public static void ShowTitle(PrivateMessageEventArgs e, Uri uri)
        {
            var title = FetchPageTitle(uri.ToString());
            if (title != null)
            {
                client.SendMessage(string.Format("{0}: \"{1}\"",
                    uri.Host, FetchPageTitle(uri.ToString())), e.PrivateMessage.Source);
            }
        }
    }
}
