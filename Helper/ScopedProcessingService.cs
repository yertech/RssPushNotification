using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Syndication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NPushover;
using NPushover.RequestObjects;
using RssPushNotification.Model;

namespace RssPushNotification.Helper
{
    internal interface IScopedProcessingService
    {
        Task DoWork(CancellationToken stoppingToken);
    }

    internal class ScopedProcessingService : IScopedProcessingService
    {
        private int executionCount = 0;
        private readonly ILogger _logger;
        private readonly RssPushNotificationContext _context;
        private readonly IMapper _mapper;
        private readonly List<string> filteringTerms = new List<string>{".NET",".NET CORE","ASP.NET", "ASP.NET MVC", "C#"};
        private readonly string[] feeds = new string[] {
            "https://www.freelancer.com/rss.xml",
            "https://www.upwork.com/ab/feed/jobs/rss?q=.net+OR+C%23&proposals=0-4%2C5-9%2C10-14&verified_payment_only=1&sort=recency&paging=0%3B10&api_params=1&securityToken=58e44659ae871d542fa6eff3ced8a927d26735d111ff61b699fde1f8be90cd1b14bb86e07f97035ab69f5712c7654bc41451aa5bccce3df882d038c6cfeea50c&userUid=1215640702124244992&orgUid=1215640702136827905"
        };
    
        public ScopedProcessingService(ILogger<ScopedProcessingService> logger,RssPushNotificationContext context,IMapper mapper)
        {
            _logger = logger;
            _mapper = mapper;
            _context = context;
        }

        public async Task DoWork(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                executionCount++;

                _logger.LogInformation(
                    "Scoped Processing Service is working. Count: {Count}", executionCount);

                //Get items for rss feeds
                var itemsfeed = GetRssFeeds();
                List<Item> items = _mapper.Map<List<Item>>(itemsfeed);

                //filter items 
                List<Item> newItems = items.Where(i =>
                    filteringTerms.Any(t => i.Title.ToUpper().Contains(t) || i.Summary.ToUpper().Contains(t))).ToList();

                //Get new items not in db
                var dbItems = _context.Items.AsNoTracking().ToList();
                var filteredItems = newItems.Where(item => dbItems.All(dbi => dbi.Id != item.Id)).ToList();

                //Send Notification to pushover api
                var po = new Pushover("agkxq9gsn3v16gsnuz4summp7haxch");

                // Quick message:
                foreach (var newItem in filteredItems)
                {
                    newItem.CreatedDate = DateTime.Now;
                    var msg = new Message(Sounds.Siren)
                    {
                        Title = newItem.Id.ToLower().Contains("freelancer") ? $"Freelancer : {newItem.Title}" : $"Upwork : {newItem.Title}",
                        Body = SubStringBody(newItem.Summary),
                        Priority = Priority.Normal,
                        IsHtmlBody = true,
                        Timestamp = DateTime.Now,
                        SupplementaryUrl = new SupplementaryURL
                        {
                            Uri = new Uri(newItem.Link),
                            Title = newItem.Title
                        },
                    };
                    var sendtask = po.SendMessageAsync(msg, "u789prun7x9xeqbdvgsusybysa5cra");
                    Thread.Sleep(2000);

                    if (sendtask.IsFaulted)
                    {
                        _logger.LogError($"Error on notification pushed for : {newItem.Id} {sendtask.Exception.Message}");
                    }
                    else
                    {
                        _logger.LogInformation($"Notification pushed for : {newItem.Id}");
                    }
                }

                //Insert new items in db
                if (filteredItems.Any())
                {
                    try
                    {
                        _context.AddRange(filteredItems);
                        await _context.SaveChangesAsync(stoppingToken);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e.Message);
                        throw;
                    }
                }
                await Task.Delay(120000, stoppingToken);
            }

        }

        private string SubStringBody(string newItemSummary)
        {
            //Max length  for message = 1024
            var text = newItemSummary;
            if (text.Length > 1024)
            {
                var indexOfFooter = newItemSummary.IndexOf("<br /><b>Posted On", StringComparison.Ordinal);
                if (indexOfFooter != -1)
                {
                    //get the footer for upwork message
                    var footer = newItemSummary.Substring(indexOfFooter);

                    text = newItemSummary.Substring(0, indexOfFooter -1).Substring(0, 1021-footer.Length);
                    text += "..." + footer;
                }

                text = newItemSummary.Substring(0, 1024);
            }

            return text;
        }

        private List<SyndicationItem> GetRssFeeds()
        {
            List<SyndicationItem> finalItems = new List<SyndicationItem>();

            foreach (string feed in feeds)
            {
                try
                {
                    XmlReader reader = XmlReader.Create(feed);
                    Rss20FeedFormatter formatter = new Rss20FeedFormatter();
                    formatter.ReadFrom(reader);
                    reader.Close();
                    finalItems.AddRange(formatter.Feed.Items);
                }
                catch (Exception e)
                {
                    _logger.LogError($"Error to read Rss feed : {feed}  {e.InnerException}");
                }
            }

            return finalItems.OrderBy(x => x.PublishDate).ToList();
        }
    }
}
