using System.Net;
using APCVehicleTracker.Data.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace APCVehicleTracker.Services;

public record ApiResult<T>(T? Value, string? Error)
{
    public bool Ok => Error is null;
}

public class StaffApiService
{
    private readonly HttpClient _http;
    public StaffApiService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<StaffDto>> ListAsync()
    {
        return await _http.GetFromJsonAsync<List<StaffDto>>("api/staff") ?? new();
    }

    public async Task<StaffDto?> GetAsync(int id)
    {
        var response = await _http.GetAsync($"api/staff/{id}");
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StaffDto>();
    }

    public async Task<ApiResult<TemporaryPasswordResponse>> CreateAsync(CreateStaffRequest request)
    {
        return await ReadResult<TemporaryPasswordResponse>(await _http.PostAsJsonAsync("api/staff", request));
    }

    public async Task<ApiResult<StaffDto>> UpdateAsync(int id, UpdateStaffRequest request)
    {
        return await ReadResult<StaffDto>(await _http.PutAsJsonAsync($"api/staff/{id}", request));
    }

    public async Task<ApiResult<TemporaryPasswordResponse>> ResetPasswordAsync(int id)
    {
        return await ReadResult<TemporaryPasswordResponse>(await _http.PostAsync($"api/staff/{id}/reset-password", null));
    }

    private static async Task<ApiResult<T>> ReadResult<T>(HttpResponseMessage response)
    {
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict or HttpStatusCode.NotFound)
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
            var message = problem?.Errors.Count > 0
                ? string.Join(" ", problem.Errors.SelectMany(er => er.Value))
                : problem?.Title ?? "The request could not be completed.";
            return new ApiResult<T>(default, message);
        }

        response.EnsureSuccessStatusCode();
        return new ApiResult<T>(await response.Content.ReadFromJsonAsync<T>(), null);
    }
}
