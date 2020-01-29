using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace RssPushNotification.Model
{
    public class RssPushNotificationContext : DbContext
    {
        public RssPushNotificationContext()
        {
        }

        public RssPushNotificationContext(DbContextOptions<RssPushNotificationContext> options)
            : base(options)
        {
        }

        public virtual DbSet<Item> Items { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Item>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired();
                entity.Property(e => e.Summary).IsRequired();
                entity.Property(e => e.PublishDate).IsRequired().HasColumnType("datetime");
                entity.Property(e => e.Categories);
                entity.Property(e => e.Link).IsRequired();
            });
        }

    }
}
