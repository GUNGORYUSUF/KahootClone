using KahootClone.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace KahootClone.Infrastructure.Data;

public static class MongoDbConfiguration
{
    public static void Configure()
    {
        try
        {
            // MongoDB v3.0.0 Sürücüsü için GUID Standartlaştırması (Zorunludur)
            // Veritabanına kaydedilecek tüm Guid tiplerinin standart (UUID) formatında olmasını garanti eder.
            BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        }
        catch { /* Daha önce kayıt edildiyse sessizce yoksay */ }

        if (!BsonClassMap.IsClassMapRegistered(typeof(Player)))
        {
            BsonClassMap.RegisterClassMap<Player>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
                cm.MapMember(p => p.Id);
                cm.MapMember(p => p.Nickname);
                cm.MapMember(p => p.Score);
                cm.MapMember(p => p.ConnectionId);
                cm.MapMember(p => p.AnsweredQuestionIds);
                cm.MapMember(p => p.AvatarUrl);
            });
        }
        if (!BsonClassMap.IsClassMapRegistered(typeof(Quiz)))
        {
            BsonClassMap.RegisterClassMap<Quiz>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
                cm.MapIdMember(q => q.Id);
                cm.MapMember(q => q.Pin);
                cm.MapMember(q => q.IsActive);
                cm.MapMember(q => q.Players);
                cm.MapMember(q => q.CurrentQuestionStartTime);
            });
        }
        if (!BsonClassMap.IsClassMapRegistered(typeof(Question))) { BsonClassMap.RegisterClassMap<Question>(cm => { cm.AutoMap(); cm.SetIgnoreExtraElements(true); }); }
        if (!BsonClassMap.IsClassMapRegistered(typeof(Option))) { BsonClassMap.RegisterClassMap<Option>(cm => { cm.AutoMap(); cm.SetIgnoreExtraElements(true); }); }
    }
}