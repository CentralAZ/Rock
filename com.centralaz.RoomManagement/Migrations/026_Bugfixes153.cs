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
    [MigrationNumber( 26, "1.9.4" )]
    public class Bugfixes153 : Migration
    {
        /// <summary>
        /// The commands to run to migrate plugin to the specific version
        /// </summary>
        public override void Up()
        {
            RockMigrationHelper.AddActionTypeAttributeValue( "7AA18EA5-62D2-49A3-B410-B2D90F2A2EBF", "5D9B13B6-CD96-4C7C-86FA-4512B9D28386", @"Changes Needed: {{Workflow | Attribute:'Reservation'}}" ); // Room Reservation Approval Notification:Notify Requester that the Reservation Requires Changes:Send Email:Subject
            RockMigrationHelper.AddActionTypeAttributeValue( "52520D50-9D35-4E0D-AF04-1A222B50EA91", "5D9B13B6-CD96-4C7C-86FA-4512B9D28386", @"Reservation Approved: {{Workflow | Attribute:'Reservation'}}" ); // Room Reservation Approval Notification:Notify Requester that the Reservation has been Approved:Send Email:Subject
            RockMigrationHelper.AddActionTypeAttributeValue( "78761E92-CD94-438A-A3F2-FB683C8D8054", "5D9B13B6-CD96-4C7C-86FA-4512B9D28386", @"Reservation Denied: {{Workflow | Attribute:'Reservation'}}" ); // Room Reservation Approval Notification:Notify Requester that the Reservation has been Denied:Send Email:Subject
            RockMigrationHelper.AddActionTypeAttributeValue( "FE7B413C-0DF4-4F9C-935C-8B39DA87742D", "5D9B13B6-CD96-4C7C-86FA-4512B9D28386", @"Approval Needed: {{Workflow | Attribute:'Reservation'}}" ); // Room Reservation Approval Notification:Notify Approval group that the Reservation is Pending Review:Send Email:Subject
        }

        /// <summary>
        /// The commands to undo a migration from a specific version.
        /// </summary>
        public override void Down()
        {
        }
    }
}