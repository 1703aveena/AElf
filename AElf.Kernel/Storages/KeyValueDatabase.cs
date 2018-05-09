﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AElf.Kernel.Storages
{
    public class KeyValueDatabase : IKeyValueDatabase
    {
        private readonly Dictionary<Hash, object> _dictionary = new Dictionary<Hash, object>();
        
        public Task<object> GetAsync(Hash key, Type type)
        {
            return _dictionary.TryGetValue(key, out var value) ? Task.FromResult(value) : Task.FromResult<object>(null);
        }

        public Task SetAsync(Hash key, object bytes)
        {
            _dictionary[key] = bytes;
            return Task.CompletedTask;
        }
    }
}