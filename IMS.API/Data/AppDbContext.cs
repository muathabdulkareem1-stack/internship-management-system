using IMS.API.Models;
using Microsoft.EntityFrameworkCore;

namespace IMS.API.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Internship> Internships => Set<Internship>();
    public DbSet<ActivityReport> ActivityReports => Set<ActivityReport>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<Evaluation> Evaluations => Set<Evaluation>();
    public DbSet<EvaluationSkill> EvaluationSkills => Set<EvaluationSkill>();
    public DbSet<Alert> Alerts => Set<Alert>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Composite PK for EvaluationSkill join table
        modelBuilder.Entity<EvaluationSkill>()
            .HasKey(es => new { es.EvaluationId, es.SkillId });

        // User → Internship (Student side, one-to-one)
        modelBuilder.Entity<Internship>()
            .HasOne(i => i.Student)
            .WithOne(u => u.Internship)
            .HasForeignKey<Internship>(i => i.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        // User → SupervisedInternships (Supervisor side, one-to-many)
        modelBuilder.Entity<Internship>()
            .HasOne(i => i.Supervisor)
            .WithMany(u => u.SupervisedInternships)
            .HasForeignKey(i => i.SupervisorId)
            .OnDelete(DeleteBehavior.SetNull);

        // Evaluation → Supervisor
        modelBuilder.Entity<Evaluation>()
            .HasOne(e => e.Supervisor)
            .WithMany(u => u.Evaluations)
            .HasForeignKey(e => e.SupervisorId)
            .OnDelete(DeleteBehavior.Restrict);

        // Seed default skills
        modelBuilder.Entity<Skill>().HasData(
            new Skill { Id = 1, Name = "Communication", Category = "Soft Skills" },
            new Skill { Id = 2, Name = "Teamwork", Category = "Soft Skills" },
            new Skill { Id = 3, Name = "Problem Solving", Category = "Soft Skills" },
            new Skill { Id = 4, Name = "Technical Proficiency", Category = "Technical" },
            new Skill { Id = 5, Name = "Time Management", Category = "Soft Skills" },
            new Skill { Id = 6, Name = "Initiative", Category = "Professional" },
            new Skill { Id = 7, Name = "Adaptability", Category = "Professional" }
        );
    }
}
