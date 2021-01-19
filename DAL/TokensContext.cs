using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QnABot.DAL
{
    
    public class TokensContext : DbContext
    {
        public DbSet<Token> Tokens { get; set; }
        public DbSet<Log> Logs { get; set; }
        public DbSet<Stats> Stats { get; set; }
        public TokensContext(DbContextOptions<TokensContext> options)
     : base(options)
        { }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Token>().ToTable("Tokens");
        }
    }

    public class Token
    {
        public Token()
        {
            this.CreateDate = DateTime.Now;
        }
        public int TokenId { get; set; }
        public Guid? GUID { get; set; }
        public string TenantId { get; set; }
        public string Url { get; set; }
        public string GroupId { get; set; }
        public string Mail { get; set; }
        public DateTime? CreateDate { get; set; }
    }
    public class Stats
    {
        public Stats()
        {
            this.CreateDate = DateTime.Now;
        }
        
        public int StatsId { get; set; }
        public float ScoreConfidence { get; set; }
        public string SourceQnA { get; set; }
        public Token Token { get; set; }
        public string  Question { get; set; }
        public string Answer { get; set; }
        public DateTime? CreateDate { get; set; }
        public DateTime? BeginAskTimeQnA { get; set; }
        public DateTime? EndAskTimeQnA { get; set; }
    }
    public class Log
    {
        public Log()
        {
            this.CreateDate = DateTime.Now;
        }
        public int LogId { get; set; }
        public string Error { get; set; }
        public string ExceptionValue { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime? CreateDate { get; set; }
    }


}
