using System;
using StackExchange.Redis;

namespace redisdotnetdemo
{
    class Program
    {
        public static int iter;
        public static bool test;
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
                    RedisConnection.REDIS_DB_URL = args[0];
                    RedisConnection.REDIS_DB_PWD = "";
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
            TestTransactionAndWait.Run();
            //TestClustering.Run();
            // destroy the connections 
            RedisConnection.Conn_Txn.Dispose();
            RedisConnection.Conn_NonTxn.Dispose();
        }
    }
}
