namespace APCVehicleTracker.API.DTOs
{
    public class VehicleSearchResponseDto
    {
        public int TotalCount { get; set; }

        public int Page { get; set; }

        public int PageSize { get; set; }

        public List<VehicleSearchResultDto> Vehicles { get; set; } = new();
    }
}