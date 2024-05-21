using Blockcore.Features.RPC;
using IndexerBenchmark;
using NBitcoin;

IndexerDapper indexerDapper = new();
IndexerEFcore indexerEFcore = new();
// IndexerBitcoin indexerBitcoin = new();
RPCClient client = BitcoinMethods.GetBitcoinClient();

Indexer indexer = new(Client: client);

Benchmark bmrk = new();
// bmrk.RunBenchmarks();


// indexerBitcoin.FetchBlockByHeight(100);


// indexerDapper.TestDapper();
// indexerEFcore.TestEFCore();
Console.WriteLine("Starting Indexer");
await indexer.RunIndexer();




