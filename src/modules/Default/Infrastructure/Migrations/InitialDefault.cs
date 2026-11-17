using FluentMigrator;

namespace Modules.Defaults.Infrastructure.Migrations;

[Migration(20261103204529)]
internal sealed class InitialDefault : Migration
{
    public override void Up()
    {
        Create.Table("Examples").InSchema("default")
            .WithColumn("Id").AsGuid().PrimaryKey("PK_Examples")
            .WithColumn("Name").AsCustom("text").NotNullable();
    }

    public override void Down() => Delete.Table("Examples").InSchema("default");
}
