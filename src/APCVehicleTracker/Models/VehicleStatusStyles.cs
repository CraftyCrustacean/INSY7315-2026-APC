namespace APCVehicleTracker.Models
{
    public static class VehicleStatusStyles
    {
        public static string PillCss(string? status) => status switch
        {
            "Available" => "status-pill--active",
            "Reserved" => "status-pill--pending",
            "In Transit" => "status-pill--transit",
            "In Workshop" => "status-pill--workshop",
            _ => "status-pill--inactive"
        };
    }
}
