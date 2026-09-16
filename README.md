# Royal Rent — Car Rental Platform

A full-stack car rental web application built with ASP.NET Core MVC (.NET 9). Visitors can browse a fleet of cars, check real-time availability for specific dates, and complete a paid reservation through an integrated payment gateway. Registered users manage their own bookings, and an admin panel handles the car catalog, user roles, and all reservations.

## Features

**Booking & payments**
- Search cars by date range and category; unavailable cars for the selected dates are automatically excluded from results.
- Car detail page with an interactive calendar showing real booked/available dates.
- Checkout flow backed by [iyzico](https://www.iyzico.com/)'s hosted Checkout Form — card details are handled entirely by iyzico, never touching this app's server.
- A reservation is only created after the payment is verified server-to-server; if two customers race for the same dates, the later payment is automatically refunded instead of double-booking the car.
- "My Reservations" page for signed-in users, with the ability to cancel upcoming (unstarted) bookings.

**Accounts**
- Registration and login with PBKDF2 (salted, 100k-iteration) password hashing.
- Session-based authentication with hardened cookies (HttpOnly, SameSite, idle timeout).
- Sign-in/sign-up preserves the page (and any dates already picked) a visitor was on, so booking mid-flow doesn't lose progress.

**Admin panel**
- Manage the car catalog (add/edit/delete, with image upload validated by file type and size).
- Manage users and admin privileges.
- View every reservation across all customers.

**General**
- Consistent, site-wide notifications (SweetAlert2) instead of raw browser alerts or one-off inline error boxes.
- Responsive Bootstrap 5 UI.

## Tech Stack

- **Backend:** ASP.NET Core MVC, .NET 9
- **Database:** SQLite via Entity Framework Core 9
- **Payments:** iyzico Checkout Form (official `Iyzipay` .NET SDK)
- **Frontend:** Razor views, Bootstrap 5, vanilla JS, SweetAlert2
- **Auth:** Custom session-based auth with PBKDF2 password hashing

## Getting Started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- (Optional, for payments) An [iyzico sandbox account](https://sandbox-merchant.iyzipay.com) — the app runs and lets you browse/manage cars without it, but checkout requires real sandbox API keys.

### Setup

```bash
git clone https://github.com/AhmetTaha1/Rent_a_Car.git
cd Rent_a_Car
dotnet restore
dotnet ef database update   # creates store.db and seeds sample cars + an admin user
dotnet run
```

The app will be available at the URL printed in the console (e.g. `http://localhost:5157`).

### Default admin login

| Email | Password |
|---|---|
| `admin@site.com` | `Admin123!` |

### Configuring payments (iyzico)

`appsettings.json` ships with placeholder sandbox credentials, so checkout will show a clear "payment could not be started" message until real ones are added. To enable it:

1. Get sandbox API keys from your [iyzico merchant sandbox panel](https://sandbox-merchant.iyzipay.com) (Settings → API Keys).
2. Replace the values in the `Iyzico` section of `appsettings.json` (or, better, override them via `appsettings.Development.json`, user-secrets, or environment variables so real keys never end up in source control):

```json
"Iyzico": {
  "ApiKey": "sandbox-...",
  "SecretKey": "sandbox-...",
  "BaseUrl": "https://sandbox-api.iyzipay.com",
  "Currency": "TRY"
}
```

> Note: iyzico's servers need to be able to reach your app's `PaymentCallback` endpoint after a card payment completes. This works automatically once deployed to a public URL; for local testing you'll need a tunnel (e.g. [ngrok](https://ngrok.com/)) pointed at your `dotnet run` port.

## Project Structure

```
Controllers/    MVC controllers (Home, Cars, Account, Admin)
Models/         EF Core entities (Car, User, Reservation) + DbContext
Services/       PasswordHasher (PBKDF2)
Views/          Razor views, organized by controller
Migrations/     EF Core migrations
wwwroot/        Static assets (css, js, images)
```

## Known Limitations

- Payments require a real iyzico sandbox (or production) account to actually process a card — see above.
- No automated test suite yet.
- Buyer info sent to iyzico (identity number, address) uses sandbox-only placeholder values since the app doesn't collect that data from users today; a production launch would need a proper checkout form for it.
