using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;


namespace IndexerBenchmark
{
    public class AppDbContext : DbContext
    {
        // protected readonly IConfiguration configuration;
        // public AppDbContext(IConfiguration configuration)
        // {
        //     this.configuration = configuration;
        // }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            // options.UseNpgsql(configuration.GetConnectionString("PostgresConnection"));
            options.UseNpgsql("Host=localhost;Port=5432;Database=IndexerBenchmark;Username=postgres;Password=drb");
        }

        public DbSet<Block> BlockTable { get; set; } 
        public DbSet<Transaction> TransactionTable { get; set; }
        public DbSet<Input> InputTable { get; set; }
        public DbSet<Output> OutputTable { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Block>()
            .HasKey(b => b.BlockHash);

            modelBuilder.Entity<Transaction>()
            .HasKey(t => t.TXID);

            modelBuilder.Entity<Input>()
            .HasKey(i => new { i.TXID, i.VOUT });

            modelBuilder.Entity<Output>()
            .HasKey(o => new { o.TXID, o.VOUT });

            modelBuilder.Entity<Transaction>()
            .HasOne(t => t.Block)
            .WithMany(b => b.Transactions)
            .HasForeignKey(t => t.BlockHash);

            modelBuilder.Entity<Input>()
            .HasOne<Transaction>(i => i.Transaction)
            .WithMany(t => t.Inputs)
            .HasForeignKey(i => i.TXID);

            modelBuilder.Entity<Input>()
            .HasOne<Transaction>(i => i.Transaction)
            .WithMany(t => t.Inputs)
            .HasForeignKey(i => i.OutpointTXID);

            modelBuilder.Entity<Output>()
            .HasOne<Transaction>(o => o.Transaction)
            .WithMany(t => t.Outputs)
            .HasForeignKey(o => o.TXID);
        }
    }

}