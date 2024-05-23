using Blockcore.Features.RPC;
using IndexerORM;
using Microsoft.EntityFrameworkCore;

IndexerDapper indexerDapper = new();
IndexerEFcore indexerEFcore = new();

DbContext db = new AppDbContext();
Console.WriteLine("Connected to Database");
RPCClient client = Network.GetBitcoinClient();
Console.WriteLine("Connected to Bitcoin core");



//at the moment keep all the other limits as a multiple of maxBatchSize
Indexer.IndexerConfig config = new Indexer.IndexerConfig(
maxCacheElements: 1000,
maxBatchSize: 200,
maxHashBatchSize: 100,
maxBatchCount: 5,
blockLimit: 25000
);

Indexer indexer = new(Client: client, config, db);

Console.WriteLine("Starting Indexer");
await indexer.RunIndexer(100000);



