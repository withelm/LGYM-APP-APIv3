using LgymApi.Domain.Entities;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.DataSeeder.Seeders;

public sealed class AddressSeeder : IEntitySeeder
{
    public int Order => 20;

    public async Task SeedAsync(AppDbContext context, SeedContext seedContext, CancellationToken cancellationToken)
    {
        SeedOperationConsole.Start("addresses");
        if (seedContext.Addresses.Count > 0)
        {
            SeedOperationConsole.Skip("addresses");
            return;
        }

        var addresses = new List<Address>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "LGYM Downtown",
                City = "Warsaw",
                Country = "Poland",
                Street = "Marszalkowska",
                StreetNumber = "10",
                PostalCode = "00-001",
                Latitude = 52.2297,
                Longitude = 21.0122
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "LGYM Riverside",
                City = "Krakow",
                Country = "Poland",
                Street = "Dietla",
                StreetNumber = "50",
                PostalCode = "30-002",
                Latitude = 50.0647,
                Longitude = 19.9450
            }
        };

        var existingNames = await context.Addresses
            .AsNoTracking()
            .Select(address => address.Name)
            .ToListAsync(cancellationToken);

        var addedAny = false;
        foreach (var address in addresses)
        {
            if (!string.IsNullOrWhiteSpace(address.Name)
                && existingNames.Contains(address.Name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            await context.Addresses.AddAsync(address, cancellationToken);
            seedContext.Addresses.Add(address);
            addedAny = true;
        }

        if (!addedAny)
        {
            SeedOperationConsole.Skip("addresses");
            return;
        }

        SeedOperationConsole.Done("addresses");
    }
}
