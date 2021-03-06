using System;
using MarathonApp.DAL.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MarathonApp.DAL.EF
{
    public class MarathonContext : IdentityDbContext<User>
    {
        public MarathonContext(DbContextOptions<MarathonContext> options) : base(options)
        {
            Database.EnsureCreated();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionBuilder)
        {

        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<User>(u => u.Property(p => p.NewUser).HasDefaultValue(true));
        }

    }
}

