﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Database;
using System.Linq;
using AElf.Kernel.Types;
using Google.Protobuf;
using Org.BouncyCastle.Asn1.X509;

namespace AElf.Kernel.Storages
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class DataStore : IDataStore
    {
        private readonly IKeyValueDatabase _keyValueDatabase;

        public DataStore(IKeyValueDatabase keyValueDatabase)
        {
            _keyValueDatabase = keyValueDatabase;
        }

        public async Task InsertAsync<T>(Hash pointerHash, T obj) where T : IMessage
        {
            try
            {
                if (pointerHash == null)
                {
                    throw new Exception("Point hash cannot be null.");
                }

                if (obj == null)
                {
                    throw new Exception("Cannot insert null value.");
                }

                var key = pointerHash.GetKeyString(typeof(T).Name);
                await _keyValueDatabase.SetAsync(key, obj.ToByteArray());
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public async Task InsertBytesAsync<T>(Hash pointerHash, byte[] obj) where T : IMessage
        {
            if (pointerHash != null && pointerHash.HashType == HashType.CanonicalHash)
            {
                Console.WriteLine($"Insert CanonicalHash of height {pointerHash.Height}: {Hash.Parser.ParseFrom(obj).ToHex()}");
            }
            try
            {
                if (pointerHash == null)
                {
                    throw new Exception("Point hash cannot be null.");
                }

                if (obj == null)
                {
                    throw new Exception("Cannot insert null value.");
                }

                var key = pointerHash.GetKeyString(typeof(T).Name);
                await _keyValueDatabase.SetAsync(key, obj);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public async Task<T> GetAsync<T>(Hash pointerHash) where T : IMessage, new()
        {
            try
            {
                if (pointerHash == null)
                {
                    throw new Exception("Pointer hash cannot be null.");
                }
                
                var key = pointerHash.GetKeyString(typeof(T).Name);
                var res = await _keyValueDatabase.GetAsync(key);
                return  res == null ? default(T): res.Deserialize<T>();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
        
        public async Task<byte[]> GetBytesAsync<T>(Hash pointerHash) where T : IMessage, new()
        {
            try
            {
                if (pointerHash == null)
                {
                    throw new Exception("Pointer hash cannot be null.");
                }
                
                var key = pointerHash.GetKeyString(typeof(T).Name);
                return await _keyValueDatabase.GetAsync(key);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public async Task<bool> PipelineSetDataAsync(Dictionary<Hash, byte[]> pipelineSet)
        {
            try
            {
                return await _keyValueDatabase.PipelineSetAsync(
                    pipelineSet.ToDictionary(kv => kv.Key.ToHex(), kv => kv.Value));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }
        }

        public async Task RemoveAsync<T>(Hash pointerHash) where T : IMessage
        {
            if (pointerHash != null && pointerHash.HashType == HashType.CanonicalHash)
            {
                Console.WriteLine($"Remove CanonicalHash of height {pointerHash.Height}");
            }
            try
            {
                if (pointerHash == null)
                {
                    throw new Exception("Pointer hash cannot be null.");
                }

                var key = pointerHash.GetKeyString(typeof(T).Name);
                await _keyValueDatabase.RemoveAsync(key);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }
}