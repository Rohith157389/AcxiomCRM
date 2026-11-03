# AcxiomCRM

AcxiomCRM is a role-based Customer Relationship Management system built with ASP.NET Core MVC, C#, Entity Framework Core, SQLite, ASP.NET Core Identity, Bootstrap 5, Chart.js, and the ASP.NET Core Web API stack.

## Overview

The application provides a secure CRM workspace for managing customer relationships, sales leads, opportunities, follow-ups, activities, audit history, users, dashboards, reports, and JSON API access. Data access is role- and ownership-scoped on the server.

## Features

- ASP.NET Core Identity login, logout, and public registration
- Idempotent development role seeding for `Admin`, `Manager`, and `SalesExecutive`
- Customer, Lead, Opportunity, Follow-up, and Activity management
- Qualified lead conversion into a Customer and Opportunity
- Server-side validation, ownership checks, and audit logging
- Admin-only user management and read-only audit log management
- Dashboard KPIs, Chart.js charts, date filters, and weighted pipeline values
- Customer, lead, follow-up, opportunity, pipeline, conversion, activity, and audit reports
- CSV exports for supported reports
- Cookie-authenticated REST APIs for customers, leads, opportunities, and follow-ups
- Development-only OpenAPI JSON document

## Technology stack

- .NET 10 / ASP.NET Core MVC
- C# with nullable reference types enabled
- Entity Framework Core 10
- SQLite
- ASP.NET Core Identity
- Bootstrap 5 and Chart.js
- ASP.NET Core OpenAPI

## Architecture

The application is organized into:

- `Controllers/` - MVC controllers and API controllers
- `Models/` - EF Core entities
- `ViewModels/` - MVC form, list, dashboard, report, and user-management models
- `Data/` - `ApplicationDbContext` and EF Core migrations
- `Services/` - API services, Identity seeding, and application services
- `Security/` - role names and authorization policies
- `Views/` -Bootstrap-based MVC views
- `Areas/Identity/` - Identity login, registration, and access-denied pages
- `wwwroot/` - CSS, JavaScript, Bootstrap, jQuery, validation libraries, and Chart.js assets

## Prerequisites

- .NET 10 SDK
- Git
- Optional: the EF Core CLI tool, version `10.0.14`

Check the SDK with:

```bash
dotnet --version
```

Install the matching EF CLI tool if it is not already installed:

```bash
dotnet tool install --global dotnet-ef --version 10.0.14
```

## Local setup

Clone the repository and enter the project directory:

```bash
git clone https://github.com/Rohith157389/AcxiomCRM.git
cd AcxiomCRM
```

Restore dependencies:

```bash
dotnet restore
```

The repository intentionally excludes local SQLite files, users, passwords, and demonstration data. Create a local database from the committed migrations:

```bash
dotnet ef database update
```

This creates `app.db` in the application working directory. Do not commit that file.

## Development Admin account

The development seeder always ensures the three roles exist. It creates or updates the configured development Admin assignment only when both `SeedAdmin:Email` and `SeedAdmin:Password` are supplied. The password must be stored outside source control using user secrets:

```bash
dotnet user-secrets set "SeedAdmin:Email" "admin@example.local"
dotnet user-secrets set "SeedAdmin:Password" "Use-a-local-development-password-1!"
```

Never put the password in `appsettings.json`, `appsettings.Development.json`, a commit, or a public issue. Public registration assigns only the `SalesExecutive` role; it cannot assign `Admin` or `Manager`.

## Run the application

```bash
dotnet run
```

For the committed HTTP launch profile:

```bash
dotnet run --launch-profile http
```

The default development profiles use `http://localhost:5004` and `https://localhost:7265`. Ports may vary by local launch configuration. Open `/Identity/Account/Register` to create a normal SalesExecutive account, or `/Identity/Account/Login` to sign in.

## Roles and authorization

- `Admin` can access all CRM data, user management, audit logs, and administrative reports.
- `Manager` can access team-wide CRM data and management reports, but not user management or audit-log administration.
- `SalesExecutive` can access only records assigned to or owned by the authenticated user.

Authorization is enforced on the server. Hiding navigation links is not used as the security boundary.

## Dashboard and reports

The dashboard contains customer, lead, and opportunity KPIs; lead-status and opportunity-stage charts; monthly won-sales charts; weighted pipeline values; and date filters for today, recent periods, month, year, and custom ranges.

Reports are available through the CRM navigation and include customer, lead, follow-up, opportunity, pipeline, lead-conversion, user-activity, and audit-log reports. Report filters, pagination, and CSV exports use the same server-side authorization scope as the pages.

## REST API

All API endpoints require an authenticated Identity session and return JSON. Cookie authentication is reused; JWT is not required.

Customers:

- `GET /api/customers?search=&status=&page=1&pageSize=20`
- `GET /api/customers/{id}`
- `POST /api/customers`
- `PUT /api/customers/{id}`
- `DELETE /api/customers/{id}`

Leads:

- `GET /api/leads?search=&status=&page=1&pageSize=20`
- `GET /api/leads/{id}`
- `POST /api/leads`
- `PUT /api/leads/{id}`
- `DELETE /api/leads/{id}`

Opportunities:

- `GET /api/opportunities?search=&stage=&status=&page=1&pageSize=20`
- `GET /api/opportunities/{id}`
- `POST /api/opportunities`
- `PUT /api/opportunities/{id}`
- `DELETE /api/opportunities/{id}`

Follow-ups:

- `GET /api/follow-ups?status=&page=1&pageSize=20`
- `GET /api/follow-ups/{id}`
- `POST /api/follow-ups`
- `PUT /api/follow-ups/{id}`
- `DELETE /api/follow-ups/{id}`

Anonymous API requests receive `401 Unauthorized`; authenticated users outside the required role or ownership scope receive `403` or `404` according to the endpoint and record scope. Invalid data returns `400`, duplicates/conflicts return `409`, and successful creation returns `201 Created`.

## OpenAPI documentation

When running in the Development environment, the OpenAPI JSON document is available at:

```text
/openapi/v1.json
```

The project exposes the document through `AddOpenApi()` and `MapOpenApi()`. A browser or an HTTP client can retrieve it while the application is running. No production OpenAPI endpoint is enabled by default.

## Troubleshooting

### `dotnet ef` is not recognized

Install the matching tool and reopen the terminal:

```bash
dotnet tool install --global dotnet-ef --version 10.0.14
```

### Database errors after a schema change

Check the migration state before changing anything:

```bash
dotnet ef migrations list
dotnet ef migrations has-pending-model-changes
```

Apply committed migrations with:

```bash
dotnet ef database update
```

Do not delete or recreate a shared database. For a clean personal checkout, remove only your own uncommitted local `app.db` and recreate it from migrations.

### The Admin role is missing

Start the application once with valid `SeedAdmin:Email` and `SeedAdmin:Password` user secrets in the Development environment. Role and Admin assignment seeding is idempotent.

### The dashboard is empty

Confirm that you are signed in with a role and that a SalesExecutive owns or is assigned CRM records. The dashboard applies date filters and server-side ownership scope.

### Build validation

```bash
dotnet build --no-restore
```

