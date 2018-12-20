﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Configuration;
using AElf.Database.RedisProtocol;

namespace AElf.Database
{
    public class RedisDatabase : IKeyValueDatabase
    {
        private readonly ConcurrentDictionary<string, PooledRedisLite> _clientManagers = new ConcurrentDictionary<string, PooledRedisLite>();

        public async Task<byte[]> GetAsync(string database, string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("key is empty");
            }

            return await Task.FromResult(GetClient(database).Get(key));
        }

        public async Task SetAsync(string database, string key, byte[] bytes)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("key is empty");
            }

            await Task.FromResult(GetClient(database).Set(key, bytes));
        }

        public async Task RemoveAsync(string database, string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("key is empty");
            }
            await Task.FromResult(GetClient(database).Remove(key));
        }

        public async Task<bool> PipelineSetAsync(string database, Dictionary<string, byte[]> cache)
        {
            if (cache.Count == 0)
            {
                return true;
            }
            return await Task.Factory.StartNew(() =>
            {
                GetClient(database).SetAll(cache);
                return true;
            });
        }

        public bool IsConnected(string database = "")
        {
            if (string.IsNullOrWhiteSpace(database))
            {
                foreach (var db in DatabaseConfig.Instance.Hosts)
                {
                    if (!GetClient(db.Key).Ping())
                    {
                        return false;
                    }
                }
            }
            else
            {
                if (!GetClient(database).Ping())
                {
                    return false;
                }
            }

            return true;
        }

        private PooledRedisLite GetClient(string database)
        {
            if (string.IsNullOrWhiteSpace(database))
            {
                throw new ArgumentException("database is empty");
            }
            if (!_clientManagers.TryGetValue(database, out var client))
            {
                var databaseHost = DatabaseConfig.Instance.GetHost(database);
                client = new PooledRedisLite(databaseHost.Host, databaseHost.Port, databaseHost.Number);
                _clientManagers.TryAdd(database, client);
            }

            return client;
        }
    }
}