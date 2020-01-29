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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

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
        private readonly PushOverConfigurations _pushOverConfigurations;
        private readonly AppConfigurations _appConfigurations;
    
        public ScopedProcessingService(ILogger<ScopedProcessingService> logger,RssPushNotificationContext context,IMapper mapper, IOptions<PushOverConfigurations> pushOverConfigurations, IOptions<AppConfigurations> appConfigurations)
        {
            _logger = logger;
            _mapper = mapper;
            _context = context;
            _pushOverConfigurations = pushOverConfigurations.Value;
            _appConfigurations = appConfigurations.Value;
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
                    _appConfigurations.FilteringTerms.Any(t => i.Title.ToUpper().Contains(t) || i.Summary.ToUpper().Contains(t))).ToList();

                //Get new items not in db
                var dbItems = _context.Items.AsNoTracking().ToList();
                var filteredItems = newItems.Where(item => dbItems.All(dbi => dbi.Id != item.Id)).ToList();

                //Send Notification to pushover api
                var po = new Pushover(_pushOverConfigurations.Secret);

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
                        }
                    };
                    var sendtask = po.SendMessageAsync(msg, _pushOverConfigurations.User);

                    if (sendtask.IsFaulted)
                    {
                        _logger.LogError($"Error on notification pushed for : {newItem.Id} {sendtask.Exception.Message}");
                    }
                    else
                    {
                        _logger.LogInformation($"Notification pushed for : {newItem.Id}");
                    }
                    Thread.Sleep(2000);
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

                    text = newItemSummary.Substring(0, indexOfFooter).Substring(0, 1021-footer.Length);
                    text += "..." + footer;
                }
                else{
                    text = newItemSummary.Substring(0, 1024);
                }
            }

            return text;
        }

        private List<SyndicationItem> GetRssFeeds()
        {
            List<SyndicationItem> finalItems = new List<SyndicationItem>();

            foreach (string feed in _appConfigurations.Feeds)
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
