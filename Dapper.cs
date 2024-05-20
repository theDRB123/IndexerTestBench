using System;
using System.Collections.Generic;
using System.Data;
using System.Runtime.CompilerServices;
using Dapper;
using Npgsql;

using BenchmarkDotNet.Attributes;

namespace IndexerBenchmark
{
    public class IndexerDapper
    {
        IDbConnection db;

        public IndexerDapper()
        {
            string connectionString = "Host=localhost;Port=5432;Database=IndexerBenchmark;Username=postgres;Password=drb";
            IDbConnection db = new NpgsqlConnection(connectionString);
            Console.WriteLine("Connected");
        }

        public void TestDapper()
        {
            //insert a transaction with multiple blocks to the database
            
        }
    }
}