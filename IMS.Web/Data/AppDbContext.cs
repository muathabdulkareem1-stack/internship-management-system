using IMS.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace IMS.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<InternshipOpportunity> InternshipOpportunities => Set<InternshipOpportunity>();
    public DbSet<Internship> Internships => Set<Internship>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<ActivityReport> ActivityReports => Set<ActivityReport>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<Evaluation> Evaluations => Set<Evaluation>();
    public DbSet<EvaluationSkill> EvaluationSkills => Set<EvaluationSkill>();
    public DbSet<Alert> Alerts => Set<Alert>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        
        modelBuilder.Entity<EvaluationSkill>()
            .HasKey(es => new { es.EvaluationId, es.SkillId });

        
        modelBuilder.Entity<Internship>()
            .HasOne(i => i.Student)
            .WithMany(u => u.Internships)
            .HasForeignKey(i => i.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        
        modelBuilder.Entity<Internship>()
            .HasOne(i => i.Supervisor)
            .WithMany(u => u.SupervisedInternships)
            .HasForeignKey(i => i.SupervisorId)
            .OnDelete(DeleteBehavior.SetNull);

        
        modelBuilder.Entity<Evaluation>()
            .HasOne(e => e.Supervisor)
            .WithMany(u => u.Evaluations)
            .HasForeignKey(e => e.SupervisorId)
            .OnDelete(DeleteBehavior.Restrict);

        
        modelBuilder.Entity<InternshipOpportunity>()
            .HasOne(o => o.Coordinator)
            .WithMany()
            .HasForeignKey(o => o.CoordinatorId)
            .OnDelete(DeleteBehavior.NoAction);

       
        modelBuilder.Entity<Internship>()
            .HasOne(i => i.Opportunity)
            .WithMany(o => o.Applications)
            .HasForeignKey(i => i.OpportunityId)
            .OnDelete(DeleteBehavior.SetNull);

        
        modelBuilder.Entity<ActivityReport>()
            .HasOne(r => r.Internship)
            .WithMany(i => i.ActivityReports)
            .HasForeignKey(r => r.InternshipId)
            .OnDelete(DeleteBehavior.NoAction);

        
        modelBuilder.Entity<Assignment>()
            .HasOne(a => a.Internship)
            .WithMany(i => i.Assignments)
            .HasForeignKey(a => a.InternshipId)
            .OnDelete(DeleteBehavior.NoAction);

        
        modelBuilder.Entity<Assignment>()
            .HasOne(a => a.Report)
            .WithOne(r => r.Assignment)
            .HasForeignKey<ActivityReport>(r => r.AssignmentId)
            .OnDelete(DeleteBehavior.SetNull);

        
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
