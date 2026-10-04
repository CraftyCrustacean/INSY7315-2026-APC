using APCVehicleTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

namespace APCVehicleTracker.Controllers
{
    // <img> tags cannot send a bearer token, so the browser asks the web app and the web app
    // fetches the blob through the API using its own authenticated HttpClient.
    [Authorize]
    public class ImagesController : Controller
    {
        private static readonly Regex BlobName = new(@"^[0-9a-f]{32}\.(jpg|png)$", RegexOptions.Compiled);

        private readonly VehicleApiService _api;

        public ImagesController(VehicleApiService api)
        {
            _api = api;
        }

        [HttpGet("images/{blobName}")]
        public async Task<IActionResult> Get(string blobName, bool thumb = false)
        {
            if (!BlobName.IsMatch(blobName)) return NotFound();

            var response = await _api.GetImageAsync(blobName, thumb);

            if (!response.IsSuccessStatusCode)
            {
                response.Dispose();
                return NotFound();
            }

            Response.RegisterForDispose(response);
            Response.Headers.CacheControl = "private, max-age=86400";

            var stream = await response.Content.ReadAsStreamAsync();
            return File(stream, response.Content.Headers.ContentType?.MediaType ?? "image/jpeg");
        }
    }
}
