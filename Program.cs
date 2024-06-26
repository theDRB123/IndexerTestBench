using Blockcore.Features.RPC;
using IndexerORM;
using Microsoft.EntityFrameworkCore;


// DbContext db = new AppDbContext();
Console.WriteLine("Connected to Database");
RPCClient client = Network.GetBitcoinClient();
Console.WriteLine("Connected to Bitcoin core");



//at the moment keep all the other limits as a multiple of maxBatchSize
Indexer.IndexerConfig config = new Indexer.IndexerConfig(
hashCacheSize: 10000,
blockCacheSize: 5000,
batchCacheSize: 10,
batchSizeLimit: 10, //in MB
blockLimit: 25000,
parallelInsertCount: 5
);

Indexer indexer = new(Client: client, config /*, db*/ );

Console.WriteLine("Starting Indexer");
await indexer.RunIndexer(1000000);



