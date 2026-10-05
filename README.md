# APC Car Sales: Vehicle Stock Tracker

**INSY7315 | POE Part 2**

APC Vehicle Tracker is a web application for APC Cars that tracks used-vehicle stock across locations. Staff sign in, search stock, view each vehicle's movement history, log movements between locations and view stock reports. Admins and stock controllers manage vehicles and images, and admins manage staff accounts.

**Live site:** https://apcvt-web-ckbmcze0hhbehwc6.germanywestcentral-01.azurewebsites.net/
**Repository:** https://github.com/CraftyCrustacean/INSY7315-2026-APC
**Group Presentation:** https://youtu.be/FJ4KDnzZqvc

## Team and features

| Feature | Owner | Student number |
|---|---|---|
| 1. Authentication, user admin, GitHub, and hosting | Zachary Handley | ST10435382 |
| 2. Vehicle search | Ritvik Maharaj | ST10438117 |
| 3. Log a movement | Saheel Ramone Bhugwandeen | ST10440943 |
| 4. Reports | Esethu Lushaba | ST10447469 |
| 5. Vehicle CRUD and image upload | Chase Miller | ST10433939 |

## Tech stack

- C# and .NET 10
- ASP.NET Core MVC (Razor views, Bootstrap 5)
- ASP.NET Core Web API (JWT bearer authentication)
- Entity Framework Core with PostgreSQL (hosted on Neon)
- Microsoft Entra External ID (OpenID Connect) and Microsoft Identity Web
- Microsoft Graph (creates staff sign-in accounts)
- Azure Blob Storage (vehicle images) and SixLabors ImageSharp (image processing)
- Azure App Service (hosting)
- xUnit and GitHub Actions (tests and CI)

## Solution structure

```text
INSY7315-2026-APC
├── src
│   ├── APCVehicleTracker        MVC web app (UI, controllers, API client services)
│   ├── APCVehicleTracker.API    Web API (vehicles, locations, staff, reports, images)
│   └── APCVehicleTracker.Data   EF Core context, entities, repositories, auth policies, shared contracts
└── tests
    └── APCVehicleTracker.Tests  xUnit tests
```

The MVC app never reads vehicle data from the database directly. It calls the API with the signed-in user's access token, and the API validates the token, checks the staff record and reads or writes the database.

## Roles and access

| Role | Search and details | Log movement | Reports | Vehicle CRUD | Staff admin |
|---|---|---|---|---|---|
| Admin | Yes | Yes | Yes | Yes (and reactivate) | Yes |
| Stock Controller | Yes | Yes | No | Yes | No |
| Branch Manager | Yes | Yes | Yes | No | No |
| Transport Staff | Yes | Yes | No | No | No |
| Sales Executive | Yes | No | No | No | No |

Access is enforced with policies (`CanManageUsers`, `CanEditVehicles`, `CanLogMovement`, `CanViewReports`) on both the MVC controllers and the API endpoints. Menu items are hidden for users who lack the permission.

## What we built

### 1. Authentication and user admin (Zachary Handley-ST10435382)

- Staff sign in through the Entra External ID sign-in page using OpenID Connect.
- After sign-in the app looks up the staff row. Unknown or inactive staff are refused and sent to an "Access not granted" page.
- For valid staff the app adds the role and `staff_id` claims, then issues an auth cookie (HttpOnly, secure, 30-minute sliding idle timeout). Every request is authorised from it. Sign out clears the cookie and calls the External ID sign-out.
- The API repeats the staff check on every request, so a deactivated or unknown account is rejected there too.
- **Staff admin (admins only):** list staff, create (first name, last name, email, optional phone, role), edit (deactivate/activate), and reset a password. The app generates a random 16-character temporary password, creates the account through Microsoft Graph with an email sign-in identity and forces a password change at first sign-in. The password is shown once and never stored. If saving the staff row fails after the Entra account is created, the Entra account is deleted again so the two stay in sync. Duplicate emails are refused, and an admin cannot remove their own admin role or deactivate their account.
- Anti-forgery tokens are validated on every POST form.

### 2. Vehicle search (Ritvik Maharaj-ST10438117)

- Available to all signed-in roles. Shows active vehicles only.
- Filters (all optional, combined with AND): make, model (list follows the chosen make), year range, status, location and an "include sold" checkbox (off by default).
- Results show the primary image thumbnail, make, model, year, registration, status, current location and days at the current location, 25 per page with a total count and Previous/Next paging that keeps the filters.
- An empty result shows "No matching vehicles found" with a Clear Filters button.
- Each result opens a details page with the vehicle's information and its movement history (date, from, to, moved by, notes).

### 3. Log a movement (Saheel Ramone Bhugwandeen-ST10440943)

- Available to Admin, Stock Controller, Branch Manager and Transport Staff. Reached from the vehicle's details page. Other users get a 403.
- The form shows the current location read-only, a destination list that excludes the current location, an optional new status and optional notes (maximum 500 characters).
- Who moved the vehicle and when are set by the server from the signed-in user and server time, never from the form.
- The API checks that the vehicle exists, is active and is not sold, that the destination exists and differs from the current location, that the status is valid (a movement cannot mark a vehicle Sold) and that the notes fit. The movement and any status change are saved in one transaction and rolled back on failure.
- A vehicle with no movements yet can have its first movement logged. It is saved with no "from" location.
- On success the user returns to the vehicle's details page, where the new location and history row appear.

### 4. Reports (Esethu Lushaba-ST10447469)

- Available to Admin and Branch Manager. Computed live from the vehicle and movement tables, and nothing is stored.
- Summary tiles: total in stock (Available and In Workshop), in workshop, and stuck vehicles.
- Stuck vehicles: vehicles that have been at their current location for more than a chosen number of days (default 2), filterable by location and day threshold.
- Stock aging: days since each vehicle's first recorded movement.
- Each report can be exported to CSV (`stuck-vehicles.csv` and `stock-aging.csv`).
- Reports include active vehicles that are not sold.

### 5. Vehicle CRUD and images (Chase Miller-ST10433939)

- Available to Admin and Stock Controller.
- **Add:** make, model, year, registration, optional VIN, status and a **required starting location**, which is saved as the vehicle's first movement in the same transaction. Registration and VIN must be unique, a new vehicle cannot be added as Sold, and the VIN must be 11 to 17 letters and digits.
- **Edit:** the same fields without location. Location can only change by logging a movement. Inactive vehicles cannot be edited.
- **Delete:** a soft delete behind a confirmation dialog that sets `IsActive` to false. History and images are kept. Admins can view inactive vehicles and reactivate them.
- **Images:** JPEG and PNG only, up to 5 MB each and at most 5 per vehicle. The file signature (magic bytes) is checked, not just the extension, and the image must decode as a real JPEG or PNG. The server generates the blob name, corrects rotation, strips metadata, resizes to a maximum of 1280 px and creates a 300 px thumbnail for search results. The first image becomes the primary image, and users can change the primary, reorder and delete images (deleting removes the blobs and the row). Images are served through the web app, though the storage container is public read only.

## Security

- Entra External ID sign-in, JWT bearer authentication on the API with a required access scope, and a per-request staff check.
- Role and policy based authorisation on every protected action.
- Anti-forgery validation on all POST forms, HTTPS redirection and HSTS, and security headers on the web app and API.
- Secrets are supplied through user secrets and Azure configuration, never committed.

## Hosting and deployment

- The web app and the API run as two separate Azure App Service apps on one plan, each with its own address and configuration. The web app calls the API over HTTPS with the signed-in user's access token.
- Both apps are in Germany West Central, the same city as the Neon PostgreSQL database, which keeps the app responsive on database queries.
- Vehicle images are stored in Azure Blob Storage. The API reaches it with a managed identity, so no storage key exists.
- Production secrets (database connection string and Entra client secrets) are held in App Service settings and are separate from the development secrets.
- Both apps are published self-contained, so they do not depend on the .NET runtime installed on the host.
- Every merge to `main` runs the tests and, if they pass, publishes both apps to the live site through a GitHub Actions workflow.
- The services are of the free tier, which sleeps when idle and has a daily CPU allowance, so the first request after a break takes a few seconds.

## Testing and CI

- xUnit tests cover the sign-in staff check (active staff receive their role claim, unknown and inactive staff are refused) and the movement-logging rules (server-set staff and date, status update, sold and inactive vehicles rejected, same or missing destination rejected, notes limit, missing staff claim, permission check and first movement for a new vehicle).
- GitHub Actions builds the solution in Release and runs the tests on every pull request to `main` and every push to `main`.

```bash
dotnet test
```

## Running locally

### Prerequisites

- .NET 10 SDK
- Git
- Visual Studio 2022 or later (ASP.NET and web development workload), or the .NET CLI

### Setup

1. Clone the repository:

```bash
git clone https://github.com/CraftyCrustacean/INSY7315-2026-APC.git
cd INSY7315-2026-APC
```

2. Set the secrets (get the values from the team). Without them you cannot sign in or reach the database.

```bash
dotnet user-secrets set "AzureAd:ClientSecret" "<client-secret>" --project src/APCVehicleTracker
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<connection-string>" --project src/APCVehicleTracker.API
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<connection-string>" --project src/APCVehicleTracker
dotnet user-secrets set "AzureAd:ClientSecret" "<api-client-secret>" --project src/APCVehicleTracker.API
dotnet user-secrets set "Storage:ConnectionString" "<storage-connection-string>" --project src/APCVehicleTracker.API
```

3. Start the API first. It must listen on `https://localhost:7183`:

```bash
dotnet run --project src/APCVehicleTracker.API --launch-profile https
```

4. Start the web app (it listens on `https://localhost:7123`) and sign in with a staff account.

In Visual Studio you can set both projects to start together, using the `https` profile for the API.

### Signing in

Only staff who exist in the Staff table and have an Entra account can sign in. Accounts are created by an admin on the Staff page. Test logins are listed in the submission document.

## Design notes and limitations

- A vehicle's current location is worked out from its latest movement and is not stored on the vehicle, so there is nothing to keep in sync.
- Stock aging counts from the first recorded movement, because vehicles have no purchase date field.
- Search does not have price or colour filters.
- Locations have no active flag, so only their existence is checked.

## Git workflow

The team used feature branches and pull requests into `main`, with peer review before merging. On every merge to main the deploy workflow is triggered, publishing the changes to the live site.
