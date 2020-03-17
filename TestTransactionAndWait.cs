using System;
using StackExchange.Redis;

public class TestTransactionAndWait
{
    private static int iter;
    private static bool test;
    private static string REDIS_DB_URL;
    private static string REDIS_DB_PWD;

    private static Lazy<ConfigurationOptions> txnConfigOptions
    = new Lazy<ConfigurationOptions>(() =>
    {
        var configOptions = new ConfigurationOptions();
        configOptions.EndPoints.Add(REDIS_DB_URL);
        configOptions.Password = REDIS_DB_PWD;
        configOptions.AbortOnConnectFail = false;
        configOptions.ConnectRetry = 10;
        configOptions.KeepAlive = 4;
        configOptions.SyncTimeout = 100000;
        configOptions.ClientName = "TxnRedisConnection";
        configOptions.ConnectTimeout = 100000;
        configOptions.Ssl = false;
        return configOptions;
    });
    private static Lazy<ConnectionMultiplexer> TxnlazyConnection = new Lazy<ConnectionMultiplexer>(() =>
    {
        return ConnectionMultiplexer.Connect(txnConfigOptions.Value);
    });

    public static ConnectionMultiplexer Conn_Txn
    {
        get
        {
            return TxnlazyConnection.Value;
        }
    }
    private static Lazy<ConfigurationOptions> nonTxnConfigOptions
    = new Lazy<ConfigurationOptions>(() =>
    {
        var configOptions = new ConfigurationOptions();
        configOptions.EndPoints.Add(REDIS_DB_URL);
        configOptions.Password = REDIS_DB_PWD;
        configOptions.AbortOnConnectFail = false;
        configOptions.ConnectRetry = 10;
        configOptions.KeepAlive = 4;
        configOptions.SyncTimeout = 5000;
        configOptions.ClientName = "NonTxnRedisConnection";
        configOptions.ConnectTimeout = 10000;
        configOptions.Ssl = false;
        return configOptions;
    });
    private static Lazy<ConnectionMultiplexer> nonTxnlazyConnection = new Lazy<ConnectionMultiplexer>(() =>
    {
        return ConnectionMultiplexer.Connect(nonTxnConfigOptions.Value);
    });

    public static ConnectionMultiplexer Conn_NonTxn
    {
        get
        {
            return nonTxnlazyConnection.Value;
        }
    }

    public static void Main(string[] args)
    {
        try
        {
            if ((args.Length != 2))
            {
                Console.WriteLine("Usage: <REDIS_DB_URL> <Iteration_Count>");
                Console.WriteLine("e.g. redis-12000.cluster4.virag.demo-rlec.redislabs.com:12000 1");
                return;
            }
            else if (args.Length == 2)
            {
                test = int.TryParse(args[1], out iter);
                if (!test)
                {
                    Console.WriteLine("Iteration is missing and it must be an interger value e.g. 1");
                    Console.WriteLine("e.g. redis-12000.cluster4.virag.demo-rlec.redislabs.com:12000 1");                    
                    return;
                }                
            	REDIS_DB_URL = args[0];
                REDIS_DB_PWD = "";
                iter = int.Parse(args[1]);
                Console.WriteLine($"Going to run {iter} iteration..");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"** CHECK PROGRAM ARGUMENTS**\n {e.StackTrace}");
            Console.WriteLine("Usage: <REDIS_DB_URL> <Iteration_Count>");
            Console.WriteLine("e.g. redis-12000.cluster4.virag.demo-rlec.redislabs.com:12000 1");
            return;
        }

        var hashKey = "userHashKey:{1234}";
        var watch = System.Diagnostics.Stopwatch.StartNew();

        IDatabase txn_redis = TestTransactionAndWait.Conn_Txn.GetDatabase();
        IDatabase non_txn_redis = TestTransactionAndWait.Conn_NonTxn.GetDatabase();
        watch.Stop();
        Console.WriteLine($"Connecting to RE DB, Execution Time: {watch.ElapsedMilliseconds} ms");

        // do all non transactional writes and reads on another redis connection i.e. non_txn_redis
        // Simple PING command
        if (!watch.IsRunning)
            watch.Restart(); // Reset time to 0 and start measuring

        for (int i = 0; i <= iter; i++)
        {
            Console.WriteLine("\n------ Begin Iteration {0}", i + " ------");

            string cacheCommand = "PING";
            Console.WriteLine("\nCache command  : " + cacheCommand);
            Console.WriteLine("Cache response : " + non_txn_redis.Execute(cacheCommand).ToString());

            // Simple get and put of integral data types into the cache
            cacheCommand = "GET Message";
            Console.WriteLine("\nCache command  : " + cacheCommand + " or StringGet()");
            Console.WriteLine("Cache response : " + non_txn_redis.StringGet("Message").ToString());

            cacheCommand = "SET Message \"Hello! The cache is working from a .NET console app!\"";
            Console.WriteLine("\nCache command  : " + cacheCommand + " or StringSet()");
            Console.WriteLine("Cache response : " + non_txn_redis.StringSet("Message", "Hello! The cache is working from a .NET console app!").ToString());

            // Demonstrate "SET Message" executed as expected...
            cacheCommand = "GET Message";
            Console.WriteLine("\nCache command  : " + cacheCommand + " or StringGet()");
            Console.WriteLine("Cache response : " + non_txn_redis.StringGet("Message").ToString());

            // Get the client list, useful to see if connection list is growing...
            cacheCommand = "CLIENT LIST";
            Console.WriteLine("\nCache command  : " + cacheCommand);
            Console.WriteLine("Cache response : \n" + non_txn_redis.Execute("CLIENT", "LIST").ToString().Replace("id=", "id="));

            var allHash = non_txn_redis.HashGetAll(hashKey);

            //get all the items
            foreach (var item in allHash)
            {
                Console.WriteLine(string.Format("key : {0}, value : {1}", item.Name, item.Value));
            }
            Console.WriteLine("\n------ End Iteration {0}", i + " ------");
        }
        watch.Stop();
        Console.WriteLine($"\nResponse from non-transaction in RE DB, Execution Time: {watch.ElapsedMilliseconds} ms");

        HashEntry[] redisPersonHash1 =
            {
                //new HashEntry("first_name", "Virag"),
                new HashEntry("last_name", "Tripathi"),
                new HashEntry("age", 42)
            };
        HashEntry[] redisPersonHash2 =
            {
                new HashEntry("first_name", "Virag"),
                new HashEntry("last_name", "Tripathi"),
                new HashEntry("age", 38)
            };
        if (!watch.IsRunning)
            watch.Restart(); // Reset time to 0 and start measuring
        // do all transactional writes on dedicated redis connection i.e. txn_redis
        var trans1 = txn_redis.CreateTransaction();
        txn_redis.HashSet(hashKey, redisPersonHash1);
        var exec = trans1.Execute();

        // https://redis.io/commands/time
        RedisResult timeBeforeWait = txn_redis.Execute("TIME");
        var resultBeforeWait = (RedisResult[])timeBeforeWait;
        var firstIntBeforeWait = int.Parse((string)resultBeforeWait[0]);
        var secondIntBeforeWait = int.Parse((string)resultBeforeWait[1]);

        // WAIT numreplicas timeout
        // summary: Wait for the synchronous replication of all the write commands sent in the context of the current connection
        // since: 3.0.0
        // group: generic
        // anything but 0 (WAIT forever) as timeout otherwise you'll get a timeout exception if slave is down longer than the syncTimeout
        // because of the default syncTimeout=5000ms. either increase the value of syncTimeout e.g. 
        // syncTimeout=100000 or use >0
        // see configurations https://stackexchange.github.io/StackExchange.Redis/Configuration.html
        // see the https://stackexchange.github.io/StackExchange.Redis/Timeouts for details

        RedisResult waitResult = txn_redis.Execute("WAIT", "1", "50");
        var waitResultResp = (RedisResult)waitResult;
        var numreplicas = int.Parse((string)waitResultResp);
        
        if (numreplicas != 1) {
            Console.WriteLine($"** REPLICA DOWN ==> numreplicas = {numreplicas}");
        } else {
            Console.WriteLine($"** REPLICA UP ==> numreplicas = {numreplicas}");
        }

        RedisResult timeAfterWait = txn_redis.Execute("TIME");
        watch.Stop();
        var resultAfterWait = (RedisResult[])timeAfterWait;
        var firstIntAfterWait = int.Parse((string)resultAfterWait[0]);
        var secondIntAfterWait = int.Parse((string)resultAfterWait[1]);

        var totalMicroSecBefore = firstIntBeforeWait * 1000000 + secondIntBeforeWait;
        var totalMicroSecAfter = firstIntAfterWait * 1000000 + secondIntAfterWait;

        Console.WriteLine("TIME elapsed since the WAIT : " + (totalMicroSecAfter - totalMicroSecBefore) * .001 + " ms" +
        " <== This includes client perceived latency");
        Console.WriteLine($"Response from 1st transaction in RE DB (with WAIT), Execution Time: {watch.ElapsedMilliseconds} ms");

        if (!watch.IsRunning)
            watch.Restart(); // Reset time to 0 and start measuring
        var trans2 = txn_redis.CreateTransaction();
        trans2.AddCondition(Condition.HashNotExists(hashKey, "first_name"));
        trans2.HashSetAsync(hashKey, redisPersonHash2, CommandFlags.FireAndForget);
        bool committed = trans2.Execute();
        // ^^^ if true: it was applied; if false: it was rolled back
        // add additional logic for cleanup if necessary
        watch.Stop();
        Console.WriteLine($"Response from 2nd transaction in RE DB (without WAIT), Execution Time: {watch.ElapsedMilliseconds} ms\n");

        // destroy the connections 
        TxnlazyConnection.Value.Dispose();
        nonTxnlazyConnection.Value.Dispose();
    }
}
