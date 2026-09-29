//using APCVehicleTracker.Models;

//namespace APCVehicleTracker.Services
//{
//    public static class DummyDataService
//    {
//        public static List<Vehicle> GetVehicles()
//        {
//            return new List<Vehicle>
//            {
//                new Vehicle { Id = 1, RegistrationNumber = "ND 12 AB CD", Make = "Toyota", Model = "Hilux", Year = 2023, VinNumber = "VIN00123456", CurrentLocation = "Showroom", Status = "In Stock", LastMoved = DateTime.Now.AddDays(-2) },
//                new Vehicle { Id = 2, RegistrationNumber = "ND 45 EF GD", Make = "VW", Model = "Polo", Year = 2022, VinNumber = "VIN00789012", CurrentLocation = "Workshop", Status = "Under Repair", LastMoved = DateTime.Now.AddDays(-1) },
//                new Vehicle { Id = 3, RegistrationNumber = "ND 78 HI JD", Make = "Ford", Model = "Ranger", Year = 2024, VinNumber = "VIN00345678", CurrentLocation = "Wash Bay", Status = "In Stock", LastMoved = DateTime.Now.AddHours(-5) },
//                new Vehicle { Id = 4, RegistrationNumber = "ND 91 KL MD", Make = "BMW", Model = "3 Series", Year = 2023, VinNumber = "VIN00901234", CurrentLocation = "Detailing", Status = "In Stock", LastMoved = DateTime.Now.AddDays(-3) },
//                new Vehicle { Id = 5, RegistrationNumber = "ND 34 NO PD", Make = "Toyota", Model = "Corolla", Year = 2021, VinNumber = "VIN00567890", CurrentLocation = "Storage Yard", Status = "In Stock", LastMoved = DateTime.Now.AddDays(-7) },
//            };
//        }

//        public static List<MovementRecord> GetMovementHistory(int vehicleId)
//        {
//            var allRecords = new List<MovementRecord>
//            {
//                new MovementRecord { Id = 1, VehicleId = 1, FromLocation = "Storage Yard", ToLocation = "Wash Bay", MovedBy = "John M.", MovementDate = DateTime.Now.AddDays(-5), Notes = "Pre-showroom prep" },
//                new MovementRecord { Id = 2, VehicleId = 1, FromLocation = "Wash Bay", ToLocation = "Showroom", MovedBy = "Sipho N.", MovementDate = DateTime.Now.AddDays(-2), Notes = "Ready for display" },
//                new MovementRecord { Id = 3, VehicleId = 2, FromLocation = "Showroom", ToLocation = "Workshop", MovedBy = "Grace K.", MovementDate = DateTime.Now.AddDays(-1), Notes = "Service check requested" },
//            };

//            return allRecords.Where(r => r.VehicleId == vehicleId).ToList();
//        }
//    }
//}