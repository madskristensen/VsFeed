using System.Net;
using System.ServiceModel.Syndication;
using System.Text.RegularExpressions;
using System.Xml;

namespace FriendsOf.Web.Services
{
    public class RssService
    {
        private static readonly string _masterFile = "master.xml";
        private static readonly string _feedFile = "feed.xml";
        private readonly IConfiguration config;

        public RssService(IConfiguration config)
        {
            this.config = config;
        }

        public async Task DownloadFeeds()
        {
            var rss = new SyndicationFeed(config["title"], config["description"], null);

            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            var feedsConfig = config.GetSection("feeds").GetChildren();

            foreach (var feedConfig in feedsConfig)
            {
                SyndicationFeed feed = await DownloadFeed(feedConfig.Value);

                foreach (var item in feed.Items)
                {
                    //twitter handle
                    item.Authors.Add(new SyndicationPerson(feedConfig.Key));

                    //hack - also putting the authors Twitter handle in the copyright so that I can get to this field in a Logic App
                    item.Copyright = new TextSyndicationContent(feedConfig.Key);
                }
                rss.Items = rss.Items.Union(feed.Items).GroupBy(i => i.Title.Text).Select(i => i.First()).OrderByDescending(i => i.PublishDate.Date);
            }

            using (XmlWriter writer = XmlWriter.Create(_masterFile))
                rss.SaveAsAtom10(writer);

            using (XmlWriter writer = XmlWriter.Create(_feedFile))
            {
                rss.Items = rss.Items.Take(10);
                rss.SaveAsAtom10(writer);
            }
        }

        private static async Task<SyndicationFeed> DownloadFeed(string url)
        {
            try
            {
                using var client = new HttpClient();
                var stream = await client.GetStreamAsync(url);
                return SyndicationFeed.Load(XmlReader.Create(stream));
            }
            catch (Exception)
            {
                return new SyndicationFeed();
            }
        }

        public IEnumerable<SyndicationItem> GetData(int page)
        {
            using XmlReader reader = XmlReader.Create(_masterFile);

            var count = int.Parse(config["postsPerPage"]);
            var items = SyndicationFeed.Load(reader).Items.Skip((page - 1) * count).Take(count);
            return items.Select(item => { CleanItem(item); return item; });
        }

        private static void CleanItem(SyndicationItem item)
        {
            string summary = item.Summary != null ? item.Summary.Text : ((TextSyndicationContent)item.Content).Text;
            summary = Regex.Replace(summary, "<[^>]*>", ""); // Strips out HTML
            item.Summary = new TextSyndicationContent(string.Join("", summary.Take(300)) + "...");
        }
    }
}
