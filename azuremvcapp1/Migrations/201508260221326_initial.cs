namespace azuremvcapp1.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class initial : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.ReadUntils",
                c => new
                    {
                        Id = c.Guid(nullable: false, identity: true),
                        TwitterUserId = c.Long(nullable: false),
                        FilterName = c.String(),
                        When = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.UserConfigs",
                c => new
                    {
                        TwitterUserId = c.Long(nullable: false),
                        Configuration = c.String(),
                    })
                .PrimaryKey(t => t.TwitterUserId);
            
        }
        
        public override void Down()
        {
            DropTable("dbo.UserConfigs");
            DropTable("dbo.ReadUntils");
        }
    }
}
