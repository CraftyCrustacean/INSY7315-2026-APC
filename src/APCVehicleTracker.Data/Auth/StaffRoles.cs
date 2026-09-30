using System.Text.RegularExpressions;

namespace APCVehicleTracker.Data.Auth;

public class StaffRoles
{
    public const string Admin = "Admin";
    public const string StockController = "StockController";
    public const string SalesExecutive = "SalesExecutive";
    public const string BranchManager = "BranchManager";
    public const string TransportStaff = "TransportStaff";

    public static readonly string[] All = { Admin, StockController, SalesExecutive, BranchManager, TransportStaff };

    public static string DisplayName(string role)
    {
        return Regex.Replace(role, "(?<=[a-z])(?=[A-Z])", " ");
    }
}
