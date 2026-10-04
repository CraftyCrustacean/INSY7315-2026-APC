namespace APCVehicleTracker.API.Options
{
    public sealed class StorageOptions
    {
        public const string SectionName = "Storage";

        // Local dev: set in user-secrets as Storage:ConnectionString.
        public string? ConnectionString { get; set; }

        // Azure: e.g. https://<account>.blob.core.windows.net  (uses managed identity, no secret).
        public string? AccountUrl { get; set; }

        public string ContainerName { get; set; } = "vehicle-images";
    }
}
