using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Newtonsoft.Json.Linq;
using HtmlAgilityPack;
using System.Xml;
using System.Xml.Linq;
using System.Linq;

namespace worm
{
    partial class MainClass
    {
        static string FetchPageTitle(string url)
        {
            try
            {
                WebClient wc = new WebClient(); // I'm sorry, okay?
                StreamReader sr = new StreamReader(wc.OpenRead(url));
                string data = sr.ReadToEnd();
                sr.Close();
                HtmlDocument hDocument = new HtmlDocument();
                hDocument.LoadHtml(data);
                var title = hDocument.DocumentNode.Descendants("title");
                if (title != null)
                {
                    if (title.Count() > 0)
                    {
                        string text = title.First().InnerText;
                        text = text.Replace("\n", "").Replace("\r", "").Trim();
                        if (text.Length < 100)
                            return WebUtility.HtmlDecode(HtmlRemoval.StripTagsRegexCompiled(text));
                    }
                }
            }
            catch { return null; }
            return null;
        }

        class Video
        {
            public string Title, Author;
            public int Views, Likes, Dislikes;
            public TimeSpan Duration;
            public bool RegionLocked, HD, CommentsEnabled, RatingsEnabled;
            public string Stars;
            public Uri VideoUri;
        }

        private static Video GetYoutubeVideo(string vid)
        {
            try
            {
                WebClient client = new WebClient();
                var sr = new StreamReader(client.OpenRead(string.Format("http://gdata.youtube.com/feeds/api/videos/{0}?v=2", Uri.EscapeUriString(vid))));
                string xml = sr.ReadToEnd();
                XDocument document = XDocument.Parse(xml);
                XNamespace media = XNamespace.Get("http://search.yahoo.com/mrss/");
                XNamespace youtube = XNamespace.Get("http://gdata.youtube.com/schemas/2007");
                XNamespace root = XNamespace.Get("http://www.w3.org/2005/Atom");
                XNamespace googleData = XNamespace.Get("http://schemas.google.com/g/2005");
                Video video = new Video();
                video.Title = document.Root.Element(root + "title").Value;
                video.Author = document.Root.Element(root + "author").Element(root + "name").Value;

                video.CommentsEnabled = document.Root.Elements(youtube + "accessControl").Where(e =>
                    e.Attribute("action").Value == "comment").First().Attribute("permission").Value == "allowed";
                video.RatingsEnabled = document.Root.Elements(youtube + "accessControl").Where(e =>
                    e.Attribute("action").Value == "rate").First().Attribute("permission").Value == "allowed";
                if (video.RatingsEnabled)
                {
                    video.Likes = int.Parse(document.Root.Element(youtube + "rating").Attribute("numLikes").Value);
                    video.Dislikes = int.Parse(document.Root.Element(youtube + "rating").Attribute("numDislikes").Value);
                }
                video.Views = int.Parse(document.Root.Element(youtube + "statistics").Attribute("viewCount").Value);
                video.Duration = TimeSpan.FromSeconds(
                    double.Parse(document.Root.Element(media + "group").Element(youtube + "duration").Attribute("seconds").Value));
                video.RegionLocked = document.Root.Element(media + "group").Element(media + "restriction") != null;
                video.VideoUri = new Uri("http://youtu.be/" + vid);
                video.HD = document.Root.Element(youtube + "hd") != null;
                if (video.RatingsEnabled)
                {
                    video.Stars = "\u000303";
                    int starCount = (int)Math.Round(double.Parse(document.Root.Element(googleData + "rating").Attribute("average").Value));
                    for (int i = 0; i < 5; i++)
                    {
                        if (i < starCount)
                            video.Stars += "★";
                        else if (i == starCount)
                            video.Stars += "\u000315☆";
                        else
                            video.Stars += "☆";
                    }
                    video.Stars += "\u000f";
                }
                return video;
            }
            catch
            {
                return null;
            }
        }

        static List<string> DoGoogleSearch(string terms)
        {
            List<string> results = new List<string>();
            try
            {
                WebClient client = new WebClient();
                StreamReader sr = new StreamReader(client.OpenRead("http://ajax.googleapis.com/ajax/services/search/web?v=1.0&q=" + Uri.EscapeUriString(terms)));
                string json = sr.ReadToEnd();
                sr.Close();
                JObject jobject = JObject.Parse(json);
                foreach (var result in jobject["responseData"]["results"])
                    results.Add(WebUtility.HtmlDecode(HtmlRemoval.StripTagsRegexCompiled(Uri.UnescapeDataString(result["title"].Value<string>())) +
                                " " + Uri.UnescapeDataString(result["url"].Value<string>())));
            }
            catch (Exception)
            {
            }
            return results;
        }
    }
}
