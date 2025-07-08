using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using Resume.Entities;
namespace Resume.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<ResumeEntity> Resumes { get; set; }

        //DbSet<Resume> Resumes { get; set; }

    }
}
