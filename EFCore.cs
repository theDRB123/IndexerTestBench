using System;
using System.Linq;

using BenchmarkDotNet.Attributes;

namespace IndexerBenchmark
{
    public class IndexerEFcore
    {
        [Benchmark]
        public void TestEFCore()
        {
            Console.WriteLine("EFCore Test");
            using var db = new AppDbContext();
            Console.WriteLine("Connected EFcore");

        }
    }
}