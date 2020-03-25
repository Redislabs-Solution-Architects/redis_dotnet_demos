using System;
using System.Collections.Generic;
using StackExchange.Redis;

namespace redisdotnetdemo
{
    public class RedisConnection
    {
        public static string REDIS_DB_URL;
        public static string REDIS_DB_PWD;

        private static Lazy<ConfigurationOptions> txnConfigOptions
        = new Lazy<ConfigurationOptions>(() =>
        {
            var configOptions = new ConfigurationOptions();
            configOptions.EndPoints.Add(REDIS_DB_URL);
            configOptions.Password = REDIS_DB_PWD;
            configOptions.AbortOnConnectFail = false;
            configOptions.ConnectRetry = 10;
            configOptions.KeepAlive = 180;
            configOptions.SyncTimeout = 100000;
            configOptions.ClientName = "TxnRedisConnection";
            configOptions.ConnectTimeout = 1000;
            configOptions.Ssl = false;
            configOptions.CommandMap = CommandMap.Create(
                new HashSet<string>
                    { // EXCLUDE a few commands
                    "SUBSCRIBE", "UNSUBSCRIBE",
                    "INFO", "CONFIG", "ECHO",
                    }, available: false);
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
            configOptions.KeepAlive = 180;
            configOptions.SyncTimeout = 5000;
            configOptions.ClientName = "NonTxnRedisConnection";
            configOptions.ConnectTimeout = 1000;
            configOptions.Ssl = false;
            configOptions.CommandMap = CommandMap.Create(
                new HashSet<string>
                    { // EXCLUDE a few commands
                    "SUBSCRIBE", "UNSUBSCRIBE",
                    "INFO", "CONFIG", "ECHO",
                    }, available: false);
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

    }

}
