﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Policy;
using System.ServiceModel.Syndication;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Hosting;
using System.Web.UI;
using System.Xml;

using config = System.Configuration.ConfigurationManager;

public partial class _Default : Page
{
    private static string _masterFile = HostingEnvironment.MapPath("~/master.xml");
    private static string _feedFile = HostingEnvironment.MapPath("~/feed.xml");
    protected int _page;

    protected void Page_Load(object sender, EventArgs e)
    {
        _page = int.Parse(Request.QueryString["page"] ?? "1");
        Task task = Task.Run(() => DownloadFeeds());

        if (!File.Exists(_masterFile))
            task.Wait();
    }

    private async Task DownloadFeeds()
    {
        var rss = new SyndicationFeed(config.AppSettings["title"], config.AppSettings["description"], null);

        ServicePointManager.Expect100Continue = true;
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

        foreach (var key in config.AppSettings.AllKeys.Where(key => key.StartsWith("feed:")))
        {
            SyndicationFeed feed = await DownloadFeed(config.AppSettings[key]);

            var vsItems = from i in feed.Items
                          where i.Title.Text.IndexOf("visual studio", StringComparison.OrdinalIgnoreCase) > -1 ||
                                i.Title.Text.IndexOf("visualstudio", StringComparison.OrdinalIgnoreCase) > -1 ||
                                i.Summary.Text.IndexOf("visual studio", StringComparison.OrdinalIgnoreCase) > -1 ||
                                i.Summary.Text.IndexOf("visualstudio", StringComparison.OrdinalIgnoreCase) > -1 ||
                                i.Categories.Any(c => c.Name.Equals("visual studio", StringComparison.OrdinalIgnoreCase)) ||
                                i.Categories.Any(c => c.Name.Equals("visualstudio", StringComparison.OrdinalIgnoreCase))
                          select i;

            rss.Items = rss.Items.Union(vsItems).GroupBy(i => i.Title.Text).Select(i => i.First()).OrderByDescending(i => i.PublishDate.Date);

            foreach (var item in rss.Items)
            {
                var link = item.Links.FirstOrDefault();

                if (link != null && !link.Uri.OriginalString.Contains('?'))
                {
                    var url = new Uri(link.Uri.OriginalString + "?utm_source=vsblogfeed&utm_medium=referral");
                    item.Links[0] = new SyndicationLink(url);
                }
            }
        }

        using (XmlWriter writer = XmlWriter.Create(_masterFile))
            rss.SaveAsRss20(writer);

        using (XmlWriter writer = XmlWriter.Create(_feedFile))
        {
            rss.Items = rss.Items.Take(10);
            rss.SaveAsRss20(writer);
        }
    }

    private async Task<SyndicationFeed> DownloadFeed(string url)
    {
        try
        {
            using (WebClient client = new WebClient())
            {
                var stream = await client.OpenReadTaskAsync(url);
                return SyndicationFeed.Load(XmlReader.Create(stream));
            }
        }
        catch (Exception ex)
        {
            Trace.Warn("Feed Collector", "Couldn't download: " + url, ex);
            return new SyndicationFeed();
        }
    }

    public IEnumerable<SyndicationItem> GetData()
    {
        using (XmlReader reader = XmlReader.Create(_masterFile))
        {
            var count = int.Parse(config.AppSettings["postsPerPage"]);
            var items = SyndicationFeed.Load(reader).Items.Skip((_page - 1) * count).Take(count);
            return items.Select(item => { CleanItem(item); return item; });
        }
    }

    private static void CleanItem(SyndicationItem item)
    {
        string summary = item.Summary != null ? item.Summary.Text : ((TextSyndicationContent)item.Content).Text;
        summary = Regex.Replace(summary, "<[^>]*>", ""); // Strips out HTML
        item.Summary = new TextSyndicationContent(string.Join("", summary.Take(300)) + "...");
    }
}