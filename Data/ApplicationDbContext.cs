using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Models;

namespace AcxiomCRM.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<FollowUp> FollowUps => Set<FollowUp>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customers");
            entity.HasKey(customer => customer.CustomerId);
            entity.HasIndex(customer => customer.CustomerCode).IsUnique();
            entity.HasIndex(customer => customer.Email);
            entity.Property(customer => customer.CreatedDate).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasOne<IdentityUser>().WithMany().HasForeignKey(customer => customer.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Lead>(entity =>
        {
            entity.ToTable("Leads");
            entity.HasKey(lead => lead.LeadId);
            entity.HasIndex(lead => lead.LeadCode).IsUnique();
            entity.HasIndex(lead => lead.Email);
            entity.Property(lead => lead.ExpectedValue).HasPrecision(18, 2);
            entity.Property(lead => lead.CreatedDate).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasOne<IdentityUser>().WithMany().HasForeignKey(lead => lead.AssignedTo).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Opportunity>(entity =>
        {
            entity.ToTable("Opportunities");
            entity.HasKey(opportunity => opportunity.OpportunityId);
            entity.Property(opportunity => opportunity.Amount).HasPrecision(18, 2);
            entity.Property(opportunity => opportunity.Probability).HasPrecision(5, 2);
            entity.HasOne(opportunity => opportunity.Customer).WithMany(customer => customer.Opportunities)
                .HasForeignKey(opportunity => opportunity.CustomerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(opportunity => opportunity.Lead).WithMany(lead => lead.Opportunities)
                .HasForeignKey(opportunity => opportunity.LeadId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<IdentityUser>().WithMany().HasForeignKey(opportunity => opportunity.AssignedTo).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FollowUp>(entity =>
        {
            entity.ToTable("FollowUps", table => table.HasCheckConstraint("CK_FollowUps_CustomerOrLead", "CustomerId IS NOT NULL OR LeadId IS NOT NULL"));
            entity.HasKey(followUp => followUp.FollowUpId);
            entity.HasOne(followUp => followUp.Customer).WithMany(customer => customer.FollowUps)
                .HasForeignKey(followUp => followUp.CustomerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(followUp => followUp.Lead).WithMany(lead => lead.FollowUps)
                .HasForeignKey(followUp => followUp.LeadId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<IdentityUser>().WithMany().HasForeignKey(followUp => followUp.AssignedTo).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Activity>(entity =>
        {
            entity.ToTable("Activities", table => table.HasCheckConstraint("CK_Activities_CustomerOrLead", "CustomerId IS NOT NULL OR LeadId IS NOT NULL"));
            entity.HasKey(activity => activity.ActivityId);
            entity.HasOne(activity => activity.Customer).WithMany(customer => customer.Activities)
                .HasForeignKey(activity => activity.CustomerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(activity => activity.Lead).WithMany(lead => lead.Activities)
                .HasForeignKey(activity => activity.LeadId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<IdentityUser>().WithMany().HasForeignKey(activity => activity.AssignedTo).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasKey(auditLog => auditLog.AuditLogId);
            entity.Property(auditLog => auditLog.CreatedDate).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(auditLog => auditLog.OldValue).HasColumnType("TEXT");
            entity.Property(auditLog => auditLog.NewValue).HasColumnType("TEXT");
            entity.HasOne<IdentityUser>().WithMany().HasForeignKey(auditLog => auditLog.UserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
