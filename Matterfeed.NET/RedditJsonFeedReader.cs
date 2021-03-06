﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Matterhook.NET.MatterhookClient;
using Newtonsoft.Json;

namespace Matterfeed.NET
{
    internal static class RedditJsonFeedReader
    {
        public static async Task PeriodicRedditAsync(TimeSpan interval, List<RedditJsonFeed> redditFeeds)
        {
            while (true)
            {
                foreach (var feed in redditFeeds)
                {
                    using (var wc = new WebClient())
                    {
                        var outputString = new StringBuilder();
                        outputString.Append($"\n{DateTime.Now}\nFetching Reddit URL: {feed.Url}");
                        
                        string json;
                        try
                        {
                            json = wc.DownloadString(feed.Url);
                        }
                        catch (Exception e)
                        {
                            outputString.Append($"\nUnable to get feed, exception: {e.Message}");
                            Console.WriteLine(outputString.ToString());
                            return;
                        }

                        //only get items we have not already processed


                        var items = JsonConvert.DeserializeObject<RedditJson>(json).RedditJsonData.RedditJsonChildren
                            .Where(y => y.Data.Created > feed.LastProcessedItem).OrderBy(x => x.Data.Created);

                        var itemCount = items.Count();
                        var procCount = 0;

                        foreach (var item in items)
                        {
                            var message = new MattermostMessage
                            {
                                Channel = feed.BotChannelOverride == "" ? null : feed.BotChannelOverride,
                                Username = feed.BotNameOverride == "" ? null : feed.BotNameOverride,
                                IconUrl = feed.BotImageOverride == "" ? null : new Uri(feed.BotImageOverride)
                            };

                            if (item.Kind == "t3")
                            {
                                var content = item.Data.PostHint == "link" ? $"Linked Content: {item.Data.Url}" : item.Data.Selftext;

                                message.Attachments = new List<MattermostAttachment>
                                {
                                    new MattermostAttachment
                                    {
                                        AuthorName = $"/u/{item.Data.Author}",
                                        AuthorLink = new Uri($"https://reddit.com/u/{item.Data.Author}"),
                                        Title = item.Data.Title,
                                        TitleLink = new Uri($"https://reddit.com{item.Data.Permalink}"),
                                        Text = content,
                                        Pretext = feed.FeedPretext
                                    }
                                };
                                message.Text =
                                    $"#{Regex.Replace(item.Data.Title.Replace(" ", "-"), "[^0-9a-zA-Z-]+", "")}";
                            }
                            else if (item.Kind == "t4")
                            {
                                message.Attachments = new List<MattermostAttachment>
                                {
                                    new MattermostAttachment
                                    {
                                        AuthorName = $"/u/{item.Data.Author}",
                                        AuthorLink = new Uri($"https://reddit.com/u/{item.Data.Author}"),
                                        Title = item.Data.Subject,
                                        TitleLink = new Uri($"https://reddit.com{item.Data.Permalink}"),
                                        Text =
                                            item.Data.Body.Replace("](/r/",
                                                "](https://reddit.com/r/"), //expand /r/ markdown links
                                        Pretext = feed.FeedPretext
                                    }
                                };
                            }


                            try
                            {
                                await Program.PostToMattermost(message);
                                feed.LastProcessedItem = item.Data.Created;
                                procCount++;
                            }
                            catch (Exception e)
                            {
                                outputString.Append($"\nException: {e.Message}");
                            }
                        }

                        outputString.Append($"\nProcessed {procCount}/{itemCount} items.");
                        Console.WriteLine(outputString.ToString());
                    }
                }
                Program.SaveConfigSection(redditFeeds);
                await Task.Delay(interval).ConfigureAwait(false);
            }
        }
    }
}