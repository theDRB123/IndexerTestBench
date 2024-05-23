//add the get block from block height functions
using System.Net;
using Blockcore.Features.RPC;
//RPC client
public static class Network
{
    public static RPCClient GetBitcoinClient()
    {
        // string rpcURL = "http://4.247.157.198:18332";
        string rpcURL = "http://172.24.198.249:18332";
        // string rpcURL = "https://go.getblock.io/6c9f9206d6a64ec6a70c659447912cbe";
        string rpcUser = "drb";
        string rpcPassword = "drb";

        Blockcore.Networks.Network network = new Blockcore.Networks.Bitcoin.BitcoinTest();

        RPCCredentialString rPCCredentialString = new()
        {
            UserPassword = new NetworkCredential(userName: rpcUser, password: rpcPassword)
        };

        return new RPCClient(rPCCredentialString.ToString(), new Uri(rpcURL), network: network);
    }
};
