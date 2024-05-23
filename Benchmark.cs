using System;
using System.Linq;
using IndexerORM;

using BenchmarkDotNet.Running;


public class Benchmark {
    public void DapperBenchmark(){
        IndexerDapper indexerDapper = new();
        
    }

    public void EFcoreBenchmark(){
        IndexerEFcore indexerEFcore = new();

    }


    public void RunBenchmarks()
    {
        Console.WriteLine("Running Benchmarks for EFcore and Dapper");
        var summaryDapper = BenchmarkRunner.Run<IndexerDapper>();


    }
}