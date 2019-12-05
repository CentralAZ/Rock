// <copyright>
// Copyright by the Central Christian Church
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//
using Rock.Plugin;

namespace com.centralaz.RoomManagement.Migrations
{
    [MigrationNumber( 24, "1.8.2" )]
    public class EventLinking : Migration
    {
        public override void Up()
        {
            Sql( @"ALTER TABLE [_com_centralaz_RoomManagement_ReservationType] ADD [IsReservationBookedOnApproval] [bit] NOT NULL DEFAULT 0;" );
            Sql( @"UPDATE [_com_centralaz_RoomManagement_ReservationType] SET [IsReservationBookedOnApproval] = 0;
                " );
        }

        public override void Down()
        {
            Sql( @"
                ALTER TABLE [dbo].[_com_centralaz_RoomManagement_ReservationType] DROP COLUMN [IsReservationBookedOnApproval]
                " );
        }
    }
}
