## Build
dotnet build

## Run API
dotnet run --project src/TutorSphere.Api

## Run Web (Blazor)
dotnet run --project src/TutorSphere.Web

## Migrations
dotnet ef migrations add <Name> --project src/TutorSphere.Infrastructure --startup-project src/TutorSphere.Api
dotnet ef database update --project src/TutorSphere.Infrastructure --startup-project src/TutorSphere.Api

## Tests
dotnet test
