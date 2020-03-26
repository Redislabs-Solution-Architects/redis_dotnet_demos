using System;
using System.Collections.Generic;
using StackExchange.Redis;

namespace redisdotnetdemo
{
    public static class TestTransactionAndWait
    {
        public static void Run()
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();

            IDatabase txn_redis = RedisConnection.Conn_Txn.GetDatabase();
            IDatabase non_txn_redis = RedisConnection.Conn_NonTxn.GetDatabase();
            watch.Stop();
            Console.WriteLine($"Connecting to RE DB, Execution Time: {watch.ElapsedMilliseconds} ms");

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

            // do all non transactional writes and reads on another redis connection i.e. non_txn_redis 
            for (int i = 1; i <= Program.iter; i++)
            {
                var hashKey = "userHashKey:{" + i + "}";

                if (!watch.IsRunning)
                     watch.Restart(); // Reset time to 0 and start measuring
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
                watch.Stop();
                Console.WriteLine($"\nResponse from non-transaction in RE DB, Execution Time: {watch.ElapsedMilliseconds} ms");

                if (!watch.IsRunning)
                    watch.Restart(); // Reset time to 0 and start measuring
                // do all transactional writes on dedicated redis connection i.e. txn_redis
                var trans1 = txn_redis.CreateTransaction();
                txn_redis.HashSet(hashKey, redisPersonHash1);
                bool exec1 = trans1.Execute();
                if (exec1 != true) {
                    Console.WriteLine($"1nd transaction rolled back");
                } else {
                    Console.WriteLine($"1nd transaction applied");
                }
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

                if (numreplicas != 1)
                {
                    Console.WriteLine($"** REPLICA DOWN ==> numreplicas = {numreplicas}");
                    // RETRY Logic
                }
                else
                {
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
                txn_redis.HashSet(hashKey, redisPersonHash2);
                bool exec2 = trans2.Execute();
                if (exec2 != true) {
                    Console.WriteLine($"2nd transaction rolled back");
                } else {
                    Console.WriteLine($"2nd transaction applied");
                }
                // ^^^ if true: it was applied; if false: it was rolled back
                watch.Stop();
                Console.WriteLine($"Response from 2nd transaction in RE DB (without WAIT), Execution Time: {watch.ElapsedMilliseconds} ms\n");
                Console.WriteLine("------ End Iteration {0}", i + " ------");
            }

            // destroy the connections 
            RedisConnection.Conn_Txn.Dispose();
            RedisConnection.Conn_NonTxn.Dispose();
        }
    }
}
