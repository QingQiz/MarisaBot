using System;
using System.IO;
using System.Linq;
using Marisa.Database.Entity;
using Marisa.Configuration;
using Realms;

namespace Marisa.Database;

public static class BotDbContext
{
    public static string DatabasePath => ConfigurationManager.Configuration.DatabasePath;

    public static Realm OpenRealm()
    {
        var dbDirectory = Path.GetDirectoryName(DatabasePath);

        if (!string.IsNullOrWhiteSpace(dbDirectory))
        {
            Directory.CreateDirectory(dbDirectory);
        }

        return Realm.GetInstance(new RealmConfiguration(DatabasePath)
        {
            SchemaVersion = 1
        });
    }

    public static void EnsureCreated()
    {
        using var _ = OpenRealm();
    }

    public static long NextId<T>(Realm realm) where T : IRealmObject
    {
        var idProperty = typeof(T).GetProperty("Id") ?? throw new InvalidOperationException($"{typeof(T).Name} has no Id property");

        return (realm.All<T>()
            .AsEnumerable()
            .Select(x => (long?)idProperty.GetValue(x))
            .Max() ?? 0L) + 1;
    }
}

public static class RealmExt
{
    public static T FirstOrDefaultByUid<T>(this Realm realm, long uid) where T : class, IHaveUId, IRealmObject
    {
        return realm.All<T>().AsEnumerable().FirstOrDefault(t => t.UId == uid);
    }

    public static T InsertOrUpdateByUid<T>(this Realm realm, T value) where T : class, IHaveUId, IRealmObject
    {
        var existing = realm.FirstOrDefaultByUid<T>(value.UId);

        if (existing is null)
        {
            var idProperty = typeof(T).GetProperty("Id");
            if (idProperty is not null && idProperty.GetValue(value) is 0L)
            {
                idProperty.SetValue(value, BotDbContext.NextId<T>(realm));
            }

            return realm.Add(value);
        }

        CopyWritableProperties(value, existing);
        return existing;
    }

    private static void CopyWritableProperties<T>(T source, T target)
    {
        foreach (var property in typeof(T).GetProperties().Where(x => x.CanRead && x.CanWrite && x.Name != "Id"))
        {
            property.SetValue(target, property.GetValue(source));
        }
    }
}
