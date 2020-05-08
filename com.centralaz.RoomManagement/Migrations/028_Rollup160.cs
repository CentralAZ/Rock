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
using System;
using System.Data.Entity;
using System.Linq;
using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Plugin;
using Rock.Web.Cache;

namespace com.centralaz.RoomManagement.Migrations
{
    /// <summary>
    /// Migration for the RoomManagement system.
    /// </summary>
    /// <seealso cref="Rock.Plugin.Migration" />
    [MigrationNumber( 28, "1.9.4" )]
    public class Rollup160 : Migration
    {
        /// <summary>
        /// The commands to run to migrate plugin to the specific version
        /// </summary>
        public override void Up()
        {
           Sql( @"
               ALTER TABLE [dbo].[_com_centralaz_RoomManagement_ReservationType] ADD [OverrideApprovalGroupId] INT NULL

                ALTER TABLE [dbo].[_com_centralaz_RoomManagement_ReservationType]  WITH CHECK ADD  CONSTRAINT [FK__com_centralaz_RoomManagement_ReservationType_OverrideApprovalGroupId] FOREIGN KEY([OverrideApprovalGroupId])
                REFERENCES [dbo].[Group] ([Id])

                ALTER TABLE [dbo].[_com_centralaz_RoomManagement_ReservationType] CHECK CONSTRAINT [FK__com_centralaz_RoomManagement_ReservationType_OverrideApprovalGroupId]             
" );

            Sql( @" UPDATE _com_centralaz_RoomManagement_ReservationType
                    SET [OverrideApprovalGroupId] = [SuperAdminGroupId]" );
        }

        /// <summary>
        /// The commands to undo a migration from a specific version.
        /// </summary>
        public override void Down()
        {
        }
    }
}