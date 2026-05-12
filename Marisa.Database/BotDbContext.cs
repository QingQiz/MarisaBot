using System;
using System.IO;
using System.Linq;
using System.Reflection;
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
            SchemaVersion = 3
        });
    }

    public static void EnsureCreated()
    {
        using var _ = OpenRealm();
    }

    public static long NextId<T>(Realm realm) where T : class, IRealmObject, IHaveId
    {
        return (realm.All<T>()
            .OrderByDescending(t => t.Id)
            .FirstOrDefault()?.Id ?? 0L) + 1;
    }
}

public static class RealmExt
{
    public static T AddWithAutoId<T>(this Realm realm, T value) where T : class, IHaveId, IRealmObject
    {
        if (value.Id == 0)
        {
            value.Id = BotDbContext.NextId<T>(realm);
        }

        return realm.Add(value);
    }

    public static T FirstOrDefaultByUid<T>(this Realm realm, long uid) where T : class, IHaveId, IHaveUId, IRealmObject
    {
        return realm.All<T>().FirstOrDefault(t => t.UId == uid);
    }

    public static T InsertOrUpdateByUid<T>(this Realm realm, T value) where T : class, IHaveId, IHaveUId, IRealmObject
    {
        var existing = realm.FirstOrDefaultByUid<T>(value.UId);

        if (existing is null)
        {
            return realm.AddWithAutoId(value);
        }

        CopyWritableProperties(value, existing);
        return existing;
    }

    private static void CopyWritableProperties<T>(T source, T target)
    {
        foreach (var property in RealmEntityCache<T>.WritableProperties)
        {
            property.SetValue(target, property.GetValue(source));
        }
    }

    private static class RealmEntityCache<T>
    {
        public static readonly PropertyInfo[] WritableProperties = typeof(T)
            .GetProperties()
            .Where(x => x.CanRead && x.CanWrite && x.Name != nameof(IHaveId.Id))
            .ToArray();
    }
}
