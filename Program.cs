using Blockcore.Features.RPC;
using IndexerBenchmark;
using NBitcoin;

IndexerDapper indexerDapper = new();
IndexerEFcore indexerEFcore = new();
// IndexerBitcoin indexerBitcoin = new();
RPCClient client = BitcoinMethods.GetBitcoinClient();

Benchmark bmrk = new();
// bmrk.RunBenchmarks();


// indexerBitcoin.FetchBlockByHeight(100);


// indexerDapper.TestDapper();
// indexerEFcore.TestEFCore();

var count = client.GetBlockCount();

Console.WriteLine(count);
