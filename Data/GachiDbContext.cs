using LavaLinkLouieBot.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LavaLinkLouieBot.Data
{
    public class GachiDbContext : DbContext
    {
        public GachiDbContext(DbContextOptions<GachiDbContext> options)
        : base(options)
        {

        }

        public DbSet<Song> louie_bot_playlists { get; set; } 
    }
}
