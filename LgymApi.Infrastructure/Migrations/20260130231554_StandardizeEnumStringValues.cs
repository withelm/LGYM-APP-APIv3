using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LgymApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StandardizeEnumStringValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
UPDATE "ExerciseScores"
SET "Unit" = CASE
    WHEN lower("Unit") IN ('kg', 'kilogram', 'kilograms') THEN 'Kilograms'
    WHEN lower("Unit") IN ('lb', 'lbs', 'pound', 'pounds') THEN 'Pounds'
    WHEN "Unit" IN ('Kilograms', 'Pounds', 'Unknown') THEN "Unit"
    ELSE 'Unknown'
END;
""");

            migrationBuilder.Sql("""
UPDATE "MainRecords"
SET "Unit" = CASE
    WHEN lower("Unit") IN ('kg', 'kilogram', 'kilograms') THEN 'Kilograms'
    WHEN lower("Unit") IN ('lb', 'lbs', 'pound', 'pounds') THEN 'Pounds'
    WHEN "Unit" IN ('Kilograms', 'Pounds', 'Unknown') THEN "Unit"
    ELSE 'Unknown'
END;
""");

            migrationBuilder.Sql("""
UPDATE "Measurements"
SET "Unit" = CASE
    WHEN lower("Unit") IN ('m', 'meter', 'meters') THEN 'Meters'
    WHEN lower("Unit") IN ('cm', 'centimeter', 'centimeters') THEN 'Centimeters'
    WHEN lower("Unit") IN ('mm', 'millimeter', 'millimeters') THEN 'Millimeters'
    WHEN "Unit" IN ('Meters', 'Centimeters', 'Millimeters', 'Unknown') THEN "Unit"
    ELSE 'Unknown'
END;
""");

            migrationBuilder.Sql("""
UPDATE "Exercises"
SET "BodyPart" = CASE
    WHEN lower("BodyPart") = 'chest' THEN 'Chest'
    WHEN lower("BodyPart") = 'back' THEN 'Back'
    WHEN lower("BodyPart") = 'shoulders' THEN 'Shoulders'
    WHEN lower("BodyPart") = 'biceps' THEN 'Biceps'
    WHEN lower("BodyPart") = 'triceps' THEN 'Triceps'
    WHEN lower("BodyPart") = 'forearms' THEN 'Forearms'
    WHEN lower("BodyPart") = 'abs' THEN 'Abs'
    WHEN lower("BodyPart") = 'quads' THEN 'Quads'
    WHEN lower("BodyPart") = 'hamstrings' THEN 'Hamstrings'
    WHEN lower("BodyPart") = 'calves' THEN 'Calves'
    WHEN lower("BodyPart") = 'glutes' THEN 'Glutes'
    WHEN "BodyPart" IN ('Chest', 'Back', 'Shoulders', 'Biceps', 'Triceps', 'Forearms', 'Abs', 'Quads', 'Hamstrings', 'Calves', 'Glutes', 'Unknown') THEN "BodyPart"
    ELSE 'Unknown'
END;
""");

            migrationBuilder.Sql("""
UPDATE "Measurements"
SET "BodyPart" = CASE
    WHEN lower("BodyPart") = 'chest' THEN 'Chest'
    WHEN lower("BodyPart") = 'back' THEN 'Back'
    WHEN lower("BodyPart") = 'shoulders' THEN 'Shoulders'
    WHEN lower("BodyPart") = 'biceps' THEN 'Biceps'
    WHEN lower("BodyPart") = 'triceps' THEN 'Triceps'
    WHEN lower("BodyPart") = 'forearms' THEN 'Forearms'
    WHEN lower("BodyPart") = 'abs' THEN 'Abs'
    WHEN lower("BodyPart") = 'quads' THEN 'Quads'
    WHEN lower("BodyPart") = 'hamstrings' THEN 'Hamstrings'
    WHEN lower("BodyPart") = 'calves' THEN 'Calves'
    WHEN lower("BodyPart") = 'glutes' THEN 'Glutes'
    WHEN "BodyPart" IN ('Chest', 'Back', 'Shoulders', 'Biceps', 'Triceps', 'Forearms', 'Abs', 'Quads', 'Hamstrings', 'Calves', 'Glutes', 'Unknown') THEN "BodyPart"
    ELSE 'Unknown'
END;
""");

            migrationBuilder.Sql("""
UPDATE "AppConfigs"
SET "Platform" = CASE
    WHEN lower("Platform") = 'android' THEN 'Android'
    WHEN lower("Platform") = 'ios' THEN 'Ios'
    WHEN "Platform" IN ('Android', 'Ios', 'Unknown') THEN "Platform"
    ELSE 'Unknown'
END;
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
UPDATE "ExerciseScores"
SET "Unit" = CASE
    WHEN "Unit" = 'Kilograms' THEN 'kg'
    WHEN "Unit" = 'Pounds' THEN 'lbs'
    ELSE "Unit"
END;
""");

            migrationBuilder.Sql("""
UPDATE "MainRecords"
SET "Unit" = CASE
    WHEN "Unit" = 'Kilograms' THEN 'kg'
    WHEN "Unit" = 'Pounds' THEN 'lbs'
    ELSE "Unit"
END;
""");

            migrationBuilder.Sql("""
UPDATE "Measurements"
SET "Unit" = CASE
    WHEN "Unit" = 'Meters' THEN 'm'
    WHEN "Unit" = 'Centimeters' THEN 'cm'
    WHEN "Unit" = 'Millimeters' THEN 'mm'
    ELSE "Unit"
END;
""");

            migrationBuilder.Sql("""
UPDATE "AppConfigs"
SET "Platform" = CASE
    WHEN "Platform" = 'Android' THEN 'ANDROID'
    WHEN "Platform" = 'Ios' THEN 'IOS'
    ELSE "Platform"
END;
""");
        }
    }
}
