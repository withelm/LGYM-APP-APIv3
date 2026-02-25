using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.DataSeeder.Seeders;

public sealed class ReportTemplateFieldSeeder : IEntitySeeder
{
    public int Order => 81;

    public async Task SeedAsync(AppDbContext context, SeedContext seedContext, CancellationToken cancellationToken)
    {
        SeedOperationConsole.Start("report template fields");
        if (seedContext.ReportTemplateFields.Count > 0)
        {
            SeedOperationConsole.Skip("report template fields");
            return;
        }

        var templates = seedContext.ReportTemplates;
        if (templates.Count == 0)
        {
            SeedOperationConsole.Skip("report template fields");
            return;
        }

        var existing = await context.ReportTemplateFields
            .AsNoTracking()
            .Select(field => new { field.TemplateId, field.Key })
            .ToListAsync(cancellationToken);

        var existingSet = new HashSet<(Guid TemplateId, string Key)>(
            existing.Select(entry => (entry.TemplateId, entry.Key)));

        var addedAny = false;
        foreach (var template in templates)
        {
            var fields = new List<ReportTemplateField>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    TemplateId = template.Id,
                    Key = "mood",
                    Label = "Mood",
                    Type = ReportFieldType.Text,
                    IsRequired = false,
                    Order = 1
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    TemplateId = template.Id,
                    Key = "energy",
                    Label = "Energy level",
                    Type = ReportFieldType.Number,
                    IsRequired = false,
                    Order = 2
                }
            };

            foreach (var field in fields)
            {
                if (!existingSet.Add((field.TemplateId, field.Key)))
                {
                    continue;
                }

                await context.ReportTemplateFields.AddAsync(field, cancellationToken);
                seedContext.ReportTemplateFields.Add(field);
                addedAny = true;
            }
        }

        if (!addedAny)
        {
            SeedOperationConsole.Skip("report template fields");
            return;
        }

        SeedOperationConsole.Done("report template fields");
    }
}
