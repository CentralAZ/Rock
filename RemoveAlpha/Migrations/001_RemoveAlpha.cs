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

namespace RemoveAlpha.Migrations
{
    [MigrationNumber( 1, "1.4.5" )]
    public class RemoveAlpha : Migration
    {
        public override void Up()
        {
            Sql(@"
            Delete From PageView Where Id in (
            Select Id         
            From PageView pv
            Join Page p on pv.PageId = prop.Id
            Where p.[Guid] =  'CFF84B6D-C852-4FC4-B602-9F045EDC8854'
                or p.[Guid] = 'B75A0C7E-4A15-4892-A857-BADE8B5DD4CA'
                or p.[Guid] = '455FFF96-AE2A-435A-B3E2-F6C32754E53A'
                or p.[Guid] = '15EDB2B6-BB6B-431E-A9AA-829489D87EDD'
                or p.[Guid] = '0FF1D7F4-BF6D-444A-BD71-645BD764EC40'
                or p.[Guid] = '81CC9A85-06F6-43B9-8476-9DF8A987EF55'
                or p.[Guid] = '1C58D731-F590-4AAC-8B8C-FD42B428B69A'
                or p.[Guid] = '4CBD2B96-E076-46DF-A576-356BCA5E577F'
                or p.[Guid] = '7638AF8B-E4C0-4C02-93B8-72A829ECACDB')");

            RockMigrationHelper.DeleteSecurityRoleGroup( "FBE0324F-F29A-4ACF-8EC3-5386C5562D70" );

            RockMigrationHelper.DeleteBlock( "2B864E89-27DE-41F9-A24B-8D2EA5C40D10" );
            RockMigrationHelper.DeleteBlockType( "6931E212-A76A-4DBB-9B97-86E5CDD0793A" );
            
            RockMigrationHelper.DeletePage( "CFF84B6D-C852-4FC4-B602-9F045EDC8854" ); //  Page: Reservation Configuration

            RockMigrationHelper.DeleteBlock( "89E210D9-7645-4CB8-9AE1-CB5512074D69" );
            RockMigrationHelper.DeleteBlockType( "88C8A452-6878-4938-913F-CA3EF87D50ED" );
            RockMigrationHelper.DeletePage( "B75A0C7E-4A15-4892-A857-BADE8B5DD4CA" ); //  Page: Resource Detail

            RockMigrationHelper.DeleteAttribute( "C405A507-7889-4287-8342-105B89710044" );
            RockMigrationHelper.DeleteBlock( "07FFD3C4-5E22-4026-AAE3-EABE608D316A" );
            RockMigrationHelper.DeleteBlockType( "620FC4A2-6587-409F-8972-22065919D9AC" );
            RockMigrationHelper.DeletePage( "455FFF96-AE2A-435A-B3E2-F6C32754E53A" ); //  Page: Resource Categories

            RockMigrationHelper.DeleteAttribute( "0C023434-43B7-4086-B469-B541FE47561C" );
            RockMigrationHelper.DeleteBlock( "BFFDFD88-EA8D-47D1-80F0-4B0D05523E69" );
            RockMigrationHelper.DeleteBlockType( "84F92545-49C5-4FF6-A7B1-099A9662F42D" );
            RockMigrationHelper.DeletePage( "15EDB2B6-BB6B-431E-A9AA-829489D87EDD" ); //  Page: Resources

            RockMigrationHelper.DeleteBlock( "41639D13-F7A6-45FE-BF32-2F17371A181C" );
            RockMigrationHelper.DeletePage( "0FF1D7F4-BF6D-444A-BD71-645BD764EC40" ); //  Page: Admin Tools

            RockMigrationHelper.DeleteAttribute( "85ECB608-B64E-43C0-986C-FC8FD38F9D81" );
            RockMigrationHelper.DeleteBlock( "1B4F3A33-656B-4FCB-A446-D481782DE8B4" );
            RockMigrationHelper.DeleteBlockType( "2A01E437-AB13-47B0-B3D4-96915801B693" );
            RockMigrationHelper.DeletePage( "81CC9A85-06F6-43B9-8476-9DF8A987EF55" ); //  Page: Available Resources

            RockMigrationHelper.DeleteAttribute( "3DD653FB-771D-4EE5-8C75-1BF1B6F773B8" );
            RockMigrationHelper.DeleteBlock( "4D4882F8-5ACC-4AE1-BC75-4FFDDA26F270" );
            RockMigrationHelper.DeleteBlockType( "8169F541-9544-4A41-BD90-0DC2D0144AFD" );
            RockMigrationHelper.DeletePage( "1C58D731-F590-4AAC-8B8C-FD42B428B69A" ); //  Page: Search Reservations

            RockMigrationHelper.DeleteBlock( "65091E04-77CE-411C-989F-EAD7D15778A0" );
            RockMigrationHelper.DeleteBlockType( "C938B1DE-9AB3-46D9-AB28-57BFCA362AEB" );
            RockMigrationHelper.DeletePage( "4CBD2B96-E076-46DF-A576-356BCA5E577F" ); //  Page: New Reservation

            RockMigrationHelper.DeleteAttribute( "90B5D912-8506-4C3D-89E2-8A91512BB30D" );
            RockMigrationHelper.DeleteAttribute( "52C3F839-A092-441F-B3F9-10617BE391EC" );
            RockMigrationHelper.DeleteAttribute( "6CB8FAC9-2CAC-49D2-9316-48360A8845D2" );
            RockMigrationHelper.DeleteAttribute( "5B8F6E28-588C-451F-8BEC-2A5EC4800132" );
            RockMigrationHelper.DeleteAttribute( "8BC51BF9-C4FC-4B08-A889-69F6A1C11230" );
            RockMigrationHelper.DeleteAttribute( "7FE686C0-DE82-4D0D-AB39-99D15681D248" );
            RockMigrationHelper.DeleteAttribute( "927C546B-305F-4491-A2F2-9E05C2446E4E" );
            RockMigrationHelper.DeleteAttribute( "68966FA5-6BF9-460D-AC1D-9FF7A52F5AF2" );
            RockMigrationHelper.DeleteAttribute( "C9C37A37-06E1-4EB7-A63F-9F7C51319A94" );
            RockMigrationHelper.DeleteAttribute( "DB7FBF03-B31F-4695-804C-EE93DC411621" );
            RockMigrationHelper.DeleteAttribute( "FFCF0C2C-8FEA-4851-AB0D-D72F50B375EC" );
            RockMigrationHelper.DeleteAttribute( "EE50A389-5909-44DF-84E6-F84085CD827E" );
            RockMigrationHelper.DeleteAttribute( "B4EAC33E-9DC2-495C-97D4-99C4345599EF" );
            RockMigrationHelper.DeleteBlock( "F71B7715-EBF5-4CDF-867E-B1018B2AECD5" );
            RockMigrationHelper.DeleteBlock( "AF897B42-21AA-4A56-B0D7-9E5303D4CE53" );
            RockMigrationHelper.DeleteBlockType( "D0EC5F69-5BB1-4BCA-B0F0-3FE2B9267635" );
            RockMigrationHelper.DeletePage( "7638AF8B-E4C0-4C02-93B8-72A829ECACDB" ); //  Page: Room Management

            RockMigrationHelper.DeleteEntityType( "839768A3-10D6-446C-A65B-B8F9EFD7808F" );
            RockMigrationHelper.DeleteEntityType( "07084E96-2907-4741-80DF-016AB5981D12" );
            RockMigrationHelper.DeleteEntityType( "5DFCA44E-7090-455C-8C7B-D02CF6331A0F" );
            RockMigrationHelper.DeleteEntityType( "A9A1F735-0298-4137-BCC1-A9117B6543C9" );
            RockMigrationHelper.DeleteEntityType( "5241B2B1-AEF2-4EB9-9737-55604069D93B" );
            RockMigrationHelper.DeleteEntityType( "3660E6A9-B3DA-4CCB-8FC8-B182BC1A2587" );
            RockMigrationHelper.DeleteEntityType( "CD0C935B-C3EF-465B-964E-A3AB686D8F51" );
            RockMigrationHelper.DeleteEntityType( "35584736-8FE2-48DA-9121-3AFD07A2DA8D" );

            Sql( @"
                           ALTER TABLE [dbo].[_com_centralaz_RoomManagement_ReservationWorkflow] DROP CONSTRAINT [FK__com_centralaz_RoomManagement_ReservationWorkflow_WorkflowId]
                           ALTER TABLE [dbo].[_com_centralaz_RoomManagement_ReservationWorkflow] DROP CONSTRAINT [FK__com_centralaz_RoomManagement_ReservationWorkflow_ModifiedByPersonAliasId]
                           ALTER TABLE [dbo].[_com_centralaz_RoomManagement_ReservationWorkflow] DROP CONSTRAINT [FK__com_centralaz_RoomManagement_ReservationWorkflow_CreatedByPersonAliasId]
                           ALTER TABLE [dbo].[_com_centralaz_RoomManagement_ReservationWorkflow] DROP CONSTRAINT [FK__com_centralaz_RoomManagement_ReservationWorkflow_ReservationWorkflowTriggerId]
                           ALTER TABLE [dbo].[_com_centralaz_RoomManagement_ReservationWorkflow] DROP CONSTRAINT [FK__com_centralaz_RoomManagement_ReservationWorkflow_ReservationId]
                           DROP TABLE [dbo].[_com_centralaz_RoomManagement_ReservationWorkflow]
                           ALTER TABLE [dbo].[_com_centralaz_RoomManagement_ReservationWorkflowTrigger] DROP CONSTRAINT [FK__com_centralaz_RoomManagement_ReservationWorkflowTrigger_WorkflowTypeId]
                           ALTER TABLE [dbo].[_com_centralaz_RoomManagement_ReservationWorkflowTrigger] DROP CONSTRAINT [FK__com_centralaz_RoomManagement_ReservationWorkflowTrigger_ModifiedByPersonAliasId]
                           ALTER TABLE [dbo].[_com_centralaz_RoomManagement_ReservationWorkflowTrigger] DROP CONSTRAINT [FK__com_centralaz_RoomManagement_ReservationWorkflowTrigger_CreatedByPersonAliasId]
                           DROP TABLE [dbo].[_com_centralaz_RoomManagement_ReservationWorkflowTrigger]
                           ALTER TABLE [dbo].[_com_centralaz_RoomManagement_ReservationLocation] DROP CONSTRAINT [FK__com_centralaz_RoomManagement_ReservationLocation_ModifiedByPersonAliasId]
                           ALTER TABLE [dbo].[_com_centralaz_RoomManagement_ReservationLocation] DROP CONSTRAINT [FK__com_centralaz_RoomManagement_ReservationLocation_CreatedByPersonAliasId]
                           ALTER TABLE [dbo].[_com_centralaz_RoomManagement_ReservationLocation] DROP CONSTRAINT [FK__com_centralaz_RoomManagement_ReservationLocation_Reservation]
                           ALTER TABLE [dbo].[_com_centralaz_RoomManagement_ReservationLocation] DROP CONSTRAINT [FK__com_centralaz_RoomManagement_ReservationLocation_Location]
                           DROP TABLE [dbo].[_com_centralaz_RoomManagement_ReservationLocation]
                           ALTER TABLE [dbo].[_com_centralaz_RoomManagement_ReservationResource] DROP CONSTRAINT [FK__com_centralaz_RoomManagement_ReservationResource_ModifiedByPersonAliasId]
                           ALTER TABLE [dbo].[_com_centralaz_RoomManagement_ReservationResource] DROP CONSTRAINT [FK__com_centralaz_RoomManagement_ReservationResource_CreatedByPersonAliasId]
                           ALTER TABLE [dbo].[_com_centralaz_RoomManagement_ReservationResource] DROP CONSTRAINT [FK__com_centralaz_RoomManagement_ReservationResource_Reservation]
                           ALTER TABLE [dbo].[_com_centralaz_RoomManagement_ReservationResource] DROP CONSTRAINT [FK__com_centralaz_RoomManagement_ReservationResource_Resource]
                           DROP TABLE [dbo].[_com_centralaz_RoomManagement_ReservationResource]
                           ALTER TABLE [dbo].[_com_centralaz_RoomManagement_Resource] DROP CONSTRAINT [FK__com_centralaz_RoomManagement_Resource_ModifiedByPersonAliasId]
                           ALTER TABLE [dbo].[_com_centralaz_RoomManagement_Resource] DROP CONSTRAINT [FK__com_centralaz_RoomManagement_Resource_CreatedByPersonAliasId]
                           ALTER TABLE [dbo].[_com_centralaz_RoomManagement_Resource] DROP CONSTRAINT [FK__com_centralaz_RoomManagement_Resource_Campus]
                           ALTER TABLE [dbo].[_com_centralaz_RoomManagement_Resource] DROP CONSTRAINT [FK__com_centralaz_RoomManagement_Resource_Category]
                           DROP TABLE [dbo].[_com_centralaz_RoomManagement_Resource]
                           ALTER TABLE [dbo].[_com_centralaz_RoomManagement_Reservation] DROP CONSTRAINT [FK__com_centralaz_RoomManagement_Reservation_ModifiedByPersonAliasId]
                           ALTER TABLE [dbo].[_com_centralaz_RoomManagement_Reservation] DROP CONSTRAINT [FK__com_centralaz_RoomManagement_Reservation_CreatedByPersonAliasId]
                           ALTER TABLE [dbo].[_com_centralaz_RoomManagement_Reservation] DROP CONSTRAINT [FK__com_centralaz_RoomManagement_Reservation_RequesterAliasId]
                           ALTER TABLE [dbo].[_com_centralaz_RoomManagement_Reservation] DROP CONSTRAINT [FK__com_centralaz_RoomManagement_Reservation_ApproverPersonId]
                           ALTER TABLE [dbo].[_com_centralaz_RoomManagement_Reservation] DROP CONSTRAINT [FK__com_centralaz_RoomManagement_Reservation_ReservationStatus]
                           ALTER TABLE [dbo].[_com_centralaz_RoomManagement_Reservation] DROP CONSTRAINT [FK__com_centralaz_RoomManagement_Reservation_ReservationMinistry]
                           ALTER TABLE [dbo].[_com_centralaz_RoomManagement_Reservation] DROP CONSTRAINT [FK__com_centralaz_RoomManagement_Reservation_Campus]
                           ALTER TABLE [dbo].[_com_centralaz_RoomManagement_Reservation] DROP CONSTRAINT [FK__com_centralaz_RoomManagement_Reservation_Schedule]
                           DROP TABLE [dbo].[_com_centralaz_RoomManagement_Reservation]
                           ALTER TABLE [dbo].[_com_centralaz_RoomManagement_ReservationMinistry] DROP CONSTRAINT [FK__com_centralaz_RoomManagement_ReservationMinistry_ModifiedByPersonAliasId]
                           ALTER TABLE [dbo].[_com_centralaz_RoomManagement_ReservationMinistry] DROP CONSTRAINT [FK__com_centralaz_RoomManagement_ReservationMinistry_CreatedByPersonAliasId]
                           DROP TABLE [dbo].[_com_centralaz_RoomManagement_ReservationMinistry]
                           ALTER TABLE [dbo].[_com_centralaz_RoomManagement_ReservationStatus] DROP CONSTRAINT [FK__com_centralaz_RoomManagement_ReservationStatus_ModifiedByPersonAliasId]
                           ALTER TABLE [dbo].[_com_centralaz_RoomManagement_ReservationStatus] DROP CONSTRAINT [FK__com_centralaz_RoomManagement_ReservationStatus_CreatedByPersonAliasId]
                           DROP TABLE [dbo].[_com_centralaz_RoomManagement_ReservationStatus]
            " );

            Sql( @"
            Delete 
            From Category
            Where Guid in ('ddede1a7-c02b-4322-9d5b-a73cdb9224c6', '355ac2fd-0831-4a11-9294-5568fdfa8fc3', 'd29a2afc-bd90-428b-9065-2ffd09fb6f6b', 'baf88943-64ea-4a6a-8e1e-f4efc5a6ceca', 'ae3f4a8d-46d7-4520-934c-85d80167b22c')


             Delete
            From PluginMigration
            Where PluginAssemblyName = 'com.centralaz.RoomManagement'" );
        }
        public override void Down()
        {
            
        }
    }
}
