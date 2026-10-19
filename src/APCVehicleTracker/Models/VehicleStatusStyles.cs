namespace APCVehicleTracker.Models
{
    // Maps a vehicle status to its status-pill colour (see site.css).
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
