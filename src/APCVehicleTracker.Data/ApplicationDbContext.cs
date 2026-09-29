using APCVehicleTracker.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace APCVehicleTracker.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Vehicle> Vehicles { get; set; }

        public DbSet<Location> Locations { get; set; }

        public DbSet<Movement> Movements { get; set; }

        public DbSet<Staff> Staff { get; set; }

        public DbSet<VehicleImage> VehicleImages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Vehicle>(entity =>
            {
                entity.ToTable("vehicle");

                entity.HasKey(v => v.VehicleId);

                entity.Property(v => v.VehicleId)
                    .HasColumnName("vehicle_id");

                entity.Property(v => v.Make)
                    .HasColumnName("make");

                entity.Property(v => v.Model)
                    .HasColumnName("model");

                entity.Property(v => v.Year)
                    .HasColumnName("year");

                entity.Property(v => v.Registration)
                    .HasColumnName("registration");

                entity.Property(v => v.Vin)
                    .HasColumnName("vin");

                entity.Property(v => v.Status)
                    .HasColumnName("status");
            });

            modelBuilder.Entity<Location>(entity =>
            {
                entity.ToTable("location");

                entity.HasKey(l => l.LocationId);

                entity.Property(l => l.LocationId)
                    .HasColumnName("location_id");

                entity.Property(l => l.LocationName)
                    .HasColumnName("location_name");

                entity.Property(l => l.LocationType)
                    .HasColumnName("location_type");

                entity.Property(l => l.Address)
                    .HasColumnName("address");
            });
            modelBuilder.Entity<Movement>(entity =>
            {
                entity.ToTable("movement");

                entity.HasKey(m => m.MovementId);

                entity.Property(m => m.MovementId)
                    .HasColumnName("movement_id");

                entity.Property(m => m.VehicleId)
                    .HasColumnName("vehicle_id");

                entity.Property(m => m.FromLocationId)
                    .HasColumnName("from_location_id");

                entity.Property(m => m.ToLocationId)
                    .HasColumnName("to_location_id");

                entity.Property(m => m.StaffId)
                    .HasColumnName("staff_id");

                entity.Property(m => m.MovementDateTime)
                    .HasColumnName("movement_date_time");

                entity.Property(m => m.Notes)
                    .HasColumnName("notes");
            });
            modelBuilder.Entity<Staff>(entity =>
            {
                entity.ToTable("staff");
                entity.HasKey(s => s.StaffId);
                entity.Property(s => s.StaffId).HasColumnName("staff_id");
                entity.Property(s => s.FirstName).HasColumnName("first_name");
                entity.Property(s => s.LastName).HasColumnName("last_name");
                entity.Property(s => s.Email).HasColumnName("email");
                entity.Property(s => s.Phone).HasColumnName("phone");
                entity.Property(s => s.Role).HasColumnName("role");
                entity.Property(s => s.EntraObjectId).HasColumnName("entra_object_id");
                entity.Property(s => s.IsActive).HasColumnName("is_active");
            });
            modelBuilder.Entity<VehicleImage>(entity =>
            {
                entity.ToTable("vehicle_image");

                entity.HasKey(v => v.VehicleImageId);

                entity.Property(v => v.VehicleImageId)
                    .HasColumnName("vehicle_image_id");

                entity.Property(v => v.VehicleId)
                    .HasColumnName("vehicle_id");

                entity.Property(v => v.ImageUrl)
                    .HasColumnName("image_url");

                entity.Property(v => v.UploadedDate)
                    .HasColumnName("uploaded_date");
            });
        }
    }
}