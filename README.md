# NextStep UA

MIS 430 Team 4 — helps University of Alabama students find the right official academic resource when they're stuck on an assignment.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Clone

```bash
git clone https://github.com/acrowe8/MIS430-Final.git
cd MIS430-Final
```

## Build

```bash
dotnet build NextStep.sln
```

## Run locally

```bash
dotnet run --project api
```

Then open the URL printed in the terminal (typically `http://localhost:5249`) in desktop Chrome. The API serves the frontend from `api/wwwroot`.

No API keys or database connection strings are required for this skeleton.
