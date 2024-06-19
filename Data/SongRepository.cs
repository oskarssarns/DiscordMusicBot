using LavaLinkLouieBot.Models;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LavaLinkLouieBot.Data
{
    public class SongRepository
    {
        private readonly IMongoCollection<Song> _songs;

        public SongRepository(IMongoDatabase database)
        {
            _songs = database.GetCollection<Song>("Songs");
        }

        public async Task AddSongAsync(Song song)
        {
            await _songs.InsertOneAsync(song);
        }

        public async Task<List<Song>> GetSongsByPlaylistAsync(string playlist)
        {
            var normalizedPlaylist = playlist.ToLower().Trim();
            var filter = Builders<Song>.Filter.Eq(s => s.Playlist, normalizedPlaylist);
            var songList = await _songs.Find(filter).ToListAsync();
            return songList;
        }
    }
}
