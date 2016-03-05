﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace Xigadee
{
    public class RedisCacheManager<K, E>: RedisCacheManager, ICacheManager<K, E>
        where K : IEquatable<K>
    {
        #region Declarations
        private string mConnection;

        private Lazy<ConnectionMultiplexer> mLazyConnection;

        protected const string cnKeyVersion = "version";
        protected const string cnKeyEntity = "entity";
        #endregion

        #region Constructor
        public RedisCacheManager(string connection, bool readOnly = true)
        {
            mConnection = connection;
            IsReadOnly = readOnly;

            mLazyConnection = new Lazy<ConnectionMultiplexer>(() =>
             {
                 try
                 {
                     return ConnectionMultiplexer.Connect(mConnection);
                 }
                 catch (Exception ex)
                 {
                     throw;
                 }
             });
        } 
        #endregion

        #region IsActive
        /// <summary>
        /// This property specifies that the cache can be used.
        /// </summary>
        public bool IsActive
        {
            get
            {
                return true;
            }
        }
        #endregion
        #region IsReadOnly
        /// <summary>
        /// This property specifies whether the cache should support Delete, Write and VersionWrite.
        /// </summary>
        public bool IsReadOnly
        {
            get;private set;
        }
        #endregion

        protected virtual RedisKey RedisKeyGet(EntityTransformHolder<K, E> transform, K key)
        {
            return $"entity.{transform.EntityName}.{transform.IdMaker(key)}";
        }

        protected virtual RedisKey RedisReferenceGet(EntityTransformHolder<K, E> transform, string refType)
        {
            //entityreference.{entitytype}.{keytype i.e., EMAIL, ID etc.}
            return $"entityreference.{transform.EntityName}.{refType.ToLowerInvariant()}";
        }

        public async Task<IResponseHolder<E>> Read(EntityTransformHolder<K, E> transform, K key)
        {
            try
            {
                IDatabase rDb = mLazyConnection.Value.GetDatabase();
                RedisKey hashkey = RedisKeyGet(transform, key);

                //Entity
                RedisValue result = await rDb.HashGetAsync(hashkey, cnKeyEntity);

                if (result.HasValue)
                    return new PersistenceResponseHolder<E>() { StatusCode = 200, Content = result, IsSuccess = true, Entity = transform.Deserialize(result)};
                else
                    return new PersistenceResponseHolder<E>() { StatusCode = 404, IsSuccess = false };
            }
            catch (Exception ex)
            {
                return new PersistenceResponseHolder<E>() { StatusCode = 500, IsSuccess = false };
            }
        }

        public async Task<IResponseHolder<E>> Read(EntityTransformHolder<K, E> transform, Tuple<string, string> reference)
        {
            try
            {
                IDatabase rDb = mLazyConnection.Value.GetDatabase();
                RedisKey hashkey = RedisReferenceGet(transform, reference.Item1);

                //Entity
                RedisValue result = await rDb.HashGetAsync(hashkey, cnKeyEntity);

                if (result.HasValue)
                    return new PersistenceResponseHolder<E>() { StatusCode = 200, Content = result, IsSuccess = true, Entity = transform.Deserialize(result) };
                else
                    return new PersistenceResponseHolder<E>() { StatusCode = 404, IsSuccess = false };
            }
            catch (Exception ex)
            {
                return new PersistenceResponseHolder<E>() { StatusCode = 500, IsSuccess = false };
            }
        }

        public async Task<bool> Delete(EntityTransformHolder<K, E> transform, K key)
        {
            if (IsReadOnly)
                return false;

            try
            {
                IDatabase rDb = mLazyConnection.Value.GetDatabase();
                IBatch batch = rDb.CreateBatch();

                var tasks = new List<Task>();
                RedisKey hashkey = RedisKeyGet(transform, key);

                //Entity
                tasks.Add(batch.HashDeleteAsync(hashkey, cnKeyEntity));
                //Version
                tasks.Add(batch.HashDeleteAsync(hashkey, cnKeyVersion));

                batch.Execute();

                await Task.WhenAll(tasks);

                return true;
            }
            catch (Exception ex)
            {

            }

            return false;
        }

        public async Task<bool> Write(EntityTransformHolder<K, E> transform, E entity)
        {
            try
            {
                K key = transform.KeyMaker(entity);

                IDatabase rDb = mLazyConnection.Value.GetDatabase();
                IBatch batch = rDb.CreateBatch();

                var tasks = new List<Task>();
                RedisKey hashkey = RedisKeyGet(transform, key);

                //Entity
                tasks.Add(batch.HashSetAsync(hashkey, cnKeyEntity, transform.Serialize(entity), when: When.Always));
                //Version
                tasks.Add(batch.HashSetAsync(hashkey, cnKeyVersion, transform.Version.EntityVersionAsString(entity), when: When.Always));

                var references = transform.ReferenceMaker(entity);
                if (references != null)
                    references.ForEach((r) => tasks.Add(WriteReference(batch, transform, r, key)));

                batch.Execute();

                await Task.WhenAll(tasks);

                return true;
            }
            catch (Exception ex)
            {

            }

            return false;
        }

        protected virtual Task WriteReference(IBatch batch, EntityTransformHolder<K, E> transform, Tuple<string,string> reference, K key)
        {
            //entityreference.{entitytype}.{keytype i.e., EMAIL, ID etc.}
            RedisKey hashkey = RedisReferenceGet(transform, reference.Item1);
            //Entity
            return batch.HashSetAsync(hashkey, reference.Item2.ToLowerInvariant(), transform.IdMaker(key), when: When.Always);
        }

        public async Task<IResponseHolder> VersionRead(EntityTransformHolder<K, E> transform, K key)
        {
            try
            {
                IDatabase rDb = mLazyConnection.Value.GetDatabase();
                RedisKey hashkey = RedisKeyGet(transform, key);

                //Entity
                RedisValue result = await rDb.HashGetAsync(hashkey, cnKeyVersion);

                if (result.HasValue)
                    return new PersistenceResponseHolder<E>() { StatusCode = 200, Content = result, IsSuccess = true, Entity = transform.Deserialize(result) };
                else
                    return new PersistenceResponseHolder<E>() { StatusCode = 404, IsSuccess = false };
            }
            catch (Exception ex)
            {
                return new PersistenceResponseHolder<E>() { StatusCode = 500, IsSuccess = false };
            }
        }

        public async Task<IResponseHolder> VersionRead(EntityTransformHolder<K, E> transform, Tuple<string, string> reference)
        {
            try
            {
                IDatabase rDb = mLazyConnection.Value.GetDatabase();
                RedisKey hashkey = RedisReferenceGet(transform, reference.Item1);

                //Entity
                RedisValue result = await rDb.HashGetAsync(hashkey, cnKeyEntity);

                if (result.HasValue)
                    return new PersistenceResponseHolder<E>() { StatusCode = 200, Content = result, IsSuccess = true, Entity = transform.Deserialize(result) };
                else
                    return new PersistenceResponseHolder<E>() { StatusCode = 404, IsSuccess = false };
            }
            catch (Exception ex)
            {
                return new PersistenceResponseHolder<E>() { StatusCode = 500, IsSuccess = false };
            }
        }
    }

    /// <summary>
    /// This is the root cache.
    /// </summary>
    public class RedisCacheManager
    {
        /// <summary>
        /// This is the static helper.
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="E"></typeparam>
        /// <param name="connection"></param>
        /// <returns></returns>
        public static RedisCacheManager<K, E> Default<K, E>(string connection)
            where K : IEquatable<K>
        {
            return new RedisCacheManager<K, E>(connection);
        }
    }
}