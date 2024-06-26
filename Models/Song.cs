﻿using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace LavaLinkLouieBot.Models
{
    public class Song
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public ObjectId Id { get; set; }
        public string? Name { get; set; }
        public string? Link { get; set; }
        public string? Playlist { get; set; }
        public string? UserAdded { get; set; }
        public DateTime? Created { get; set; }
    }
}
