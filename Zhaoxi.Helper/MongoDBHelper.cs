﻿using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using MongoDB.Driver;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using MongoDB.Bson;
using System.Linq.Expressions;

namespace Zhaoxi.Helper
{

    #region Connect
    public interface IConnect : IDisposable
    {
        MongoDatabaseSettings DatabaseSettings
        {
            get; set;
        }

        IMongoCollection<T> Collection<T>(string collectionName);
    }
    [Serializable]
    public class Connect : IDisposable, IConnect
    {
        private bool disposed;

        public MongoClient Client
        {
            get;
            set;
        }

        public IMongoDatabase DataBase
        {
            get;
            set;
        }

        public MongoDatabaseSettings DatabaseSettings
        {
            get; set;
        }
        public Connect(string connectionString, string databaseName)
        {
            this.Client = new MongoClient(connectionString);
            this.DataBase = this.Client.GetDatabase(databaseName, null);

        }

        public Connect(string connectionString, string databaseName, MongoDatabaseSettings databaseSettings)
        {
            this.Client = new MongoClient(connectionString);
            this.DataBase = this.Client.GetDatabase(databaseName, databaseSettings);
        }

        public IMongoCollection<T> Collection<T>(string collectionName)
        {
            return this.DataBase.GetCollection<T>(collectionName, null);
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    this.DataBase = null;
                    this.Client = null;
                }
                this.disposed = true;
            }
        }
        ~Connect()
        {
            this.Dispose(false);
        }
    }
    #endregion

    public interface IMongoDBHelper<T> where T : class, new()
    {
        Task<T> FindAsync(Expression<Func<T, bool>> Query);
        Task<IEnumerable<T>> AllAsync();
        Task<T> AddAsync(T Model);
        Task<ReplaceOneResult> EditAsync(Expression<Func<T, bool>> Where, T Model);
        Task<DeleteResult> DeleteAsync(Expression<Func<T, bool>> Where);

    }
    public class MongoDBHelper<T> : IMongoDBHelper<T> where T : class, new()
    {
        //需要导入Microsoft.AspNetCore包
        public static IConfigurationRoot configuration = new ConfigurationBuilder()
             .SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json").Build();

        /// <summary>
        /// 根据Key取Value值   appsettings.json 的值
        /// </summary>
        /// <param name="key"></param>
        public static string GetAppsettingsValue(string keyname, string keypath = "MongoDB")
        {
            try
            {
                return configuration.GetSection(keypath).GetSection(keyname).Value;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        public IConnect Connect { get; set; }
        public IMongoCollection<T> Collection { get; set; }
        public string CollectionName { get; set; }

        public string ConnectionStr = GetAppsettingsValue("ConnectionString");
        public string DefaultDataBaseName = GetAppsettingsValue("Database");

        public MongoDBHelper()
        {
            CollectionNameAttribute mongoCollectionName = (CollectionNameAttribute)typeof(T).GetTypeInfo().GetCustomAttribute(typeof(CollectionNameAttribute));
            this.CollectionName = (mongoCollectionName != null ? mongoCollectionName.Name : typeof(T).Name.ToLower());
            this.Connect = new Connect(ConnectionStr, DefaultDataBaseName);
            this.Collection = this.Connect.Collection<T>(this.CollectionName);
        }

        public async Task<IEnumerable<T>> AllAsync()
        {
            IFindFluent<T, T> findFluent = IMongoCollectionExtensions.Find<T>(this.Collection, new BsonDocument(), null);
            return await IAsyncCursorSourceExtensions.ToListAsync<T>(findFluent, new CancellationToken());
        }

        public async Task<T> FindAsync(Expression<Func<T, bool>> where)
        {
            IFindFluent<T, T> findFluent = IMongoCollectionExtensions.Find<T>(this.Collection, where, null);
            return await IFindFluentExtensions.FirstOrDefaultAsync<T, T>(findFluent, new CancellationToken());
        }

        public async Task<T> AddAsync(T model)
        {
            await this.Collection.InsertOneAsync(model, null, new CancellationToken());
            return model;
        }
        public async Task<ReplaceOneResult> EditAsync(Expression<Func<T, bool>> where, T model)
        {
            IMongoCollection<T> collection = this.Collection;
            FilterDefinition<T> filterDefinition = where;
            T t = model;
            ReplaceOptions replaceOptions = new ReplaceOptions() { IsUpsert = true };
            CancellationToken cancellationToken = new CancellationToken();
            return await collection.ReplaceOneAsync(filterDefinition, t, replaceOptions, cancellationToken);
        }

        public async Task<DeleteResult> DeleteAsync(Expression<Func<T, bool>> where)
        {
            return await this.Collection.DeleteOneAsync(where, new CancellationToken());
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class CollectionNameAttribute : Attribute
    {
        /// <summary>
        ///     Initializes a new instance of the CollectionName class attribute with the desired name.
        /// </summary>
        /// <param name="value">Name of the collection.</param>
        public CollectionNameAttribute(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Empty collection name is not allowed", nameof(value));

            Name = value;
        }

        /// <summary>
        ///     Gets the name of the collection.
        /// </summary>
        /// <value>The name of the collection.</value>
        public virtual string Name { get; }
    }
}
