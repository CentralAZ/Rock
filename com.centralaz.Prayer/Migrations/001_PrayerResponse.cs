﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rock;
using Rock.Plugin;
using Rock.Web.Cache;
namespace com.centralaz.Accountability.Migrations
{
    [MigrationNumber( 1, "1.0.14" )]
    public class PrayerResponse : Migration
    {
        //
        public override void Up()
        {
            // Page: PrayerResponse
            RockMigrationHelper.AddPage( "7019736A-8F30-4402-8A48-CE5308218618", "325B7BFD-8B80-44FD-A951-4E4763DA6C0D", "Prayer Response", "", "A2A3D942-D1C3-4451-A71C-CB550F156910", "" ); // Site:External Website
            Sql( @"
              UPDATE [Page]
	                SET [DisplayInNavWhen] = 2
	                WHERE [Guid] = 'A2A3D942-D1C3-4451-A71C-CB550F156910'
            " );
            
            RockMigrationHelper.UpdateBlockType( "Prayer Response", "Block for people who have requested prayer to submit an answer to said prayer.", "~/Plugins/com_centralaz/Prayer/PrayerResponse.ascx", "com_centralaz > Prayer", "7DD1FCB7-D820-486A-885B-5D260A17F349" );
            RockMigrationHelper.AddBlock( "A2A3D942-D1C3-4451-A71C-CB550F156910", "", "7DD1FCB7-D820-486A-885B-5D260A17F349", "Prayer Response", "Main", "", "", 0, "5EA2F2F9-E091-4FAF-A8A9-B883B12DF583" );

            RockMigrationHelper.AddBlock( "A2A3D942-D1C3-4451-A71C-CB550F156910", "", "CACB9D1A-A820-4587-986A-D66A69EE9948", "Page Menu", "Sidebar1", "", "", 0, "24DE4F04-7F7C-402C-B32E-084A826D0C64" );

            RockMigrationHelper.AddBlockTypeAttribute( "7DD1FCB7-D820-486A-885B-5D260A17F349", "1D0D3794-C210-48A8-8C68-3FBEC08A6BA5", "Answer Submitted Message", "AnswerSubmittedMessage", "", "Lava template to use to display a success message.", 2, @"<div>
Thank you for sharing God's answer to your prayer with us.
</div>", "A4E91C3B-0B41-4335-8924-0BFC72E337D7" );

            RockMigrationHelper.AddBlockTypeAttribute( "7DD1FCB7-D820-486A-885B-5D260A17F349", "1D0D3794-C210-48A8-8C68-3FBEC08A6BA5", "Description", "Description", "", "Lava template to use to display information about the block", 2, @"<div>
  We are encouraged to be able to pray for you. If you feel as though your prayer has been answered, feel welcome to fill out just how it has been answered. Otherwise, click the 'Extend Request' button for the prayer request to be extended another week.
  </br>
  </br>
  First Name: {{PrayerRequest.FirstName}}
  </br>
  Last Name: {{PrayerRequest.LastName}}
  </br>
  Email Address: {{PrayerRequest.Email}}
  </br>
  Prayer Category: {{PrayerRequest.Category.Name}}
  </br>
  </br>
  Request: {{PrayerRequest.Text}}
  </br>
  </br>

</div>
", "AD076FE9-F13F-4BB3-B814-DF930769100B" );

            RockMigrationHelper.AddBlockTypeAttribute( "7DD1FCB7-D820-486A-885B-5D260A17F349", "1D0D3794-C210-48A8-8C68-3FBEC08A6BA5", "Date Extended Message", "DateExtendedMessage", "", "Lava template to use to display a success message.", 2, @"<div>
Our team will continue praying for your request.
</div>", "CE30BF14-C3C6-4B14-86FF-9967C24C07B2" );

            RockMigrationHelper.AddBlockTypeAttribute( "7DD1FCB7-D820-486A-885B-5D260A17F349", "1D0D3794-C210-48A8-8C68-3FBEC08A6BA5", "Error", "Error", "", "Lava template to use to display an error message.", 2, @"<div>
<h3>Error</h3>
Either the prayer you are looking to update has expired, or you do not have the right credentials for it. If you would like us to continue praying for you, we welcome you to fill out another prayer request.
</div>", "558416C0-6F58-429C-859A-FC687B6AE50D" );

            RockMigrationHelper.AddBlockTypeAttribute( "7DD1FCB7-D820-486A-885B-5D260A17F349", "A75DFC58-7A1B-4799-BF31-451B2BBE38FF", "Expires After (Days)", "ExpireDays", "", "Number of days until the request will expire (only applies when auto-approved is enabled).", 4, @"14", "5722EA2F-1CBE-4BBD-8E17-9834A5B7E72E" );



            RockMigrationHelper.AddBlockAttributeValue( "24DE4F04-7F7C-402C-B32E-084A826D0C64", "7A2010F0-0C0C-4CC5-A29B-9CBAE4DE3A22", @"" ); // CSS File

            RockMigrationHelper.AddBlockAttributeValue( "24DE4F04-7F7C-402C-B32E-084A826D0C64", "EEE71DDE-C6BC-489B-BAA5-1753E322F183", @"False" ); // Include Current Parameters

            RockMigrationHelper.AddBlockAttributeValue( "24DE4F04-7F7C-402C-B32E-084A826D0C64", "1322186A-862A-4CF1-B349-28ECB67229BA", @"{% include '~~/Assets/Lava/PageSubNav.lava' %}" ); // Template

            RockMigrationHelper.AddBlockAttributeValue( "24DE4F04-7F7C-402C-B32E-084A826D0C64", "41F1C42E-2395-4063-BD4F-031DF8D5B231", @"7625a63e-6650-4886-b605-53c2234fa5e1" ); // Root Page

            RockMigrationHelper.AddBlockAttributeValue( "24DE4F04-7F7C-402C-B32E-084A826D0C64", "6C952052-BC79-41BA-8B88-AB8EA3E99648", @"1" ); // Number of Levels

            RockMigrationHelper.AddBlockAttributeValue( "24DE4F04-7F7C-402C-B32E-084A826D0C64", "E4CF237D-1D12-4C93-AFD7-78EB296C4B69", @"False" ); // Include Current QueryString

            RockMigrationHelper.AddBlockAttributeValue( "24DE4F04-7F7C-402C-B32E-084A826D0C64", "2EF904CD-976E-4489-8C18-9BA43885ACD9", @"False" ); // Enable Debug

            RockMigrationHelper.AddBlockAttributeValue( "24DE4F04-7F7C-402C-B32E-084A826D0C64", "C80209A8-D9E0-4877-A8E3-1F7DBF64D4C2", @"False" ); // Is Secondary Block

            RockMigrationHelper.AddBlockAttributeValue( "24DE4F04-7F7C-402C-B32E-084A826D0C64", "0A49DABE-42EE-40E5-9E06-0E6530944865", @"" ); // Include Page List

            RockMigrationHelper.UpdateSystemEmail( "Groups", "Prayer Request Update", "PrayerTeam@centralaz.com", "", "", "", "", "Prayer Request Update", @"{{PrayerRequest.FirstName}},

We've had a great time praying for you this week. One of the things that really motivates a prayer team is frequent updates and answered prayer. Have there been any changes? Please take a moment to update us on any changes that will help us pray more effectively or strategically.

If you wish for us to continue praying for this request or send an update, please click the 'Edit Request' link below.

<a href='{{MagicUrl}}'>Edit Request</a>

Blessings,
Paul Covert
Prayer Pastor, Central Christian Church

Request:
{{PrayerRequest.Text}}", "4ADFA0F5-E6E1-4208-A6E1-BF69FA29ADF0" );

            Sql( @"
            INSERT INTO [dbo].[ServiceJob]
                       ([IsSystem]
                       ,[IsActive]
                       ,[Name]
                       ,[Description]
                       ,[Class]
                       ,[CronExpression]
                       ,[NotificationStatus]
                       ,[Guid])
                 VALUES
                       (0
                       ,0
                       ,'Send Prayer Update Email'
                       ,'Sends an email to all people with prayer requests that expire today, asking for an update on the prayer request.'
                       ,'com.centralaz.Prayer.Jobs.SendRequestUpdateEmail'
                       ,'0 0 9 1/1 * ? *'
                       ,1
                       ,'92B866DB-A941-4238-A34D-31B68C3B5965')
          " );

            RockMigrationHelper.AddEntityAttribute( "Rock.Model.ServiceJob", "08F3003B-F3E2-41EC-BDF1-A2B7AC2908CF", "Class", "com.centralaz.Prayer.Jobs.SendRequestUpdateEmail", "Request Expired Email", "", "The system email to send.", 0, "4ADFA0F5-E6E1-4208-A6E1-BF69FA29ADF0", "3D55298A-C3DE-405C-A423-1CFDDC927C96" );
            RockMigrationHelper.AddEntityAttribute( "Rock.Model.ServiceJob", "309460EF-0CC5-41C6-9161-B3837BA3D374", "Class", "com.centralaz.Prayer.Jobs.SendRequestUpdateEmail", "Category", "", "The category of prayer request the email will be sent out to.", 2, "4B2D88F5-6E45-4B4B-8776-11118C8E8269", "C5A429C5-FE02-4D19-A7D5-0066798FD70B" );
            RockMigrationHelper.AddEntityAttribute( "Rock.Model.ServiceJob", "BD53F9C9-EBA9-4D3F-82EA-DE5DD34A8108", "Class", "com.centralaz.Prayer.Jobs.SendRequestUpdateEmail", "Request Update Page", "", "The page that the link directs the user to.", 0, "A2A3D942-D1C3-4451-A71C-CB550F156910", "6CC4788A-A47E-4101-A787-9DA7873890F0" );
        }

        public override void Down()
        {
            RockMigrationHelper.DeleteAttribute( "3D55298A-C3DE-405C-A423-1CFDDC927C96" );    // Rock.Model.ServiceJob: Request Expired Email
            RockMigrationHelper.DeleteAttribute( "C5A429C5-FE02-4D19-A7D5-0066798FD70B" );    // Rock.Model.ServiceJob: Category
            RockMigrationHelper.DeleteAttribute( "6CC4788A-A47E-4101-A787-9DA7873890F0" );    // Rock.Model.ServiceJob: Request Update Page

            RockMigrationHelper.DeleteAttribute( "558416C0-6F58-429C-859A-FC687B6AE50D" );
            RockMigrationHelper.DeleteAttribute( "5722EA2F-1CBE-4BBD-8E17-9834A5B7E72E" );
            RockMigrationHelper.DeleteAttribute( "CE30BF14-C3C6-4B14-86FF-9967C24C07B2" );
            RockMigrationHelper.DeleteAttribute( "AD076FE9-F13F-4BB3-B814-DF930769100B" );
            RockMigrationHelper.DeleteAttribute( "A4E91C3B-0B41-4335-8924-0BFC72E337D7" );
            RockMigrationHelper.DeleteBlock( "24DE4F04-7F7C-402C-B32E-084A826D0C64" );
            RockMigrationHelper.DeleteBlock( "5EA2F2F9-E091-4FAF-A8A9-B883B12DF583" );
            RockMigrationHelper.DeleteBlockType( "7DD1FCB7-D820-486A-885B-5D260A17F349" );
            RockMigrationHelper.DeletePage( "A2A3D942-D1C3-4451-A71C-CB550F156910" ); //  Page: PrayerResponse
        }
    }
}