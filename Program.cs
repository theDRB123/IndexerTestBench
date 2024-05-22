using Blockcore.Features.RPC;
using IndexerBenchmark;
using NBitcoin;

IndexerDapper indexerDapper = new();
IndexerEFcore indexerEFcore = new();
// IndexerBitcoin indexerBitcoin = new();
RPCClient client = Network.GetBitcoinClient();

Indexer indexer = new(Client: client);

Benchmark bmrk = new();
// bmrk.RunBenchmarks();


Console.WriteLine("===> " + client.GetBestBlockHash());


// indexerDapper.TestDapper();
// indexerEFcore.TestEFCore();
Console.WriteLine("Starting Indexer");
await indexer.RunIndexer();




