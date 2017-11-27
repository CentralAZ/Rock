﻿// <copyright>
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
    [MigrationNumber( 17, "1.6.0" )]
    public class AddDetailPageToReservationList : Migration
    {
        public override void Up()
        {
            RockMigrationHelper.AddBlockAttributeValue( true, "4D4882F8-5ACC-4AE1-BC75-4FFDDA26F270", "3DD653FB-771D-4EE5-8C75-1BF1B6F773B8", @"4cbd2b96-e076-46df-a576-356bca5e577f,893ff97e-57d2-42e0-bf9a-6027d673773c" ); // Detail Page
        }
        public override void Down()
        {

        }


        /// <summary>
        /// Adds the action type attribute value in the situation where the attributeGuid
        /// is not well-known.
        /// </summary>
        /// <param name="actionTypeGuid">The action type unique identifier.</param>
        /// <param name="actionEntityTypeGuid">The action entity type unique identifier.</param>
        /// <param name="fieldTypeGuid">The field type unique identifier.</param>
        /// <param name="attributeName">Name of the attribute.</param>
        /// <param name="attributeKey">The attribute key.</param>
        /// <param name="attributeDescription">The attribute description.</param>
        /// <param name="attributeOrder">The attribute order.</param>
        /// <param name="attributeDefaultValue">The attribute default value.</param>
        /// <param name="value">The value.</param>
        public void AddActionTypeAttributeValue( string actionTypeGuid, string actionEntityTypeGuid, string fieldTypeGuid, string attributeName, string attributeKey, string attributeDescription, int attributeOrder, string attributeDefaultValue, string value )
        {

            Sql( string.Format( @"

                DECLARE @ActionEntityTypeId int = (SELECT [Id] FROM [EntityType] WHERE [Guid] = '{0}')
                DECLARE @FieldTypeId int = (SELECT [Id] FROM [FieldType] WHERE [Guid] = '{1}')
                DECLARE @EntityTypeId int = (SELECT [Id] FROM [EntityType] WHERE [Name] = 'Rock.Model.WorkflowActionType')
                DECLARE @AttributeGuid uniqueidentifier = (SELECT [Guid] FROM [Attribute] WHERE [EntityTypeId] = @EntityTypeId AND [EntityTypeQualifierColumn] = 'EntityTypeId' AND [EntityTypeQualifierValue] = CAST(@ActionEntityTypeId as varchar) AND [Key] = '{2}' )
                DECLARE @AttributeId int

                -- Find or add the action type's attribute
                IF @AttributeGuid IS NOT NULL
                BEGIN
                    SET @Attributeid = (SELECT [Id] FROM [Attribute] WHERE [Guid] = @AttributeGuid)
                END
                ELSE
                BEGIN
                    INSERT INTO [Attribute] (
                        [IsSystem],[FieldTypeId],[EntityTypeId],[EntityTypeQualifierColumn],[EntityTypeQualifierValue],
                        [Key],[Name],[Description],
                        [Order],[IsGridColumn],[DefaultValue],[IsMultiValue],[IsRequired],
                        [Guid])
                    VALUES(
                        1,@FieldTypeId, @EntityTypeId,'EntityTypeId',CAST(@ActionEntityTypeId as varchar),
                        '{2}','{3}','{4}',
                        {5},0,'{6}',0,0,
                        NEWID() )
                    SET @AttributeId = SCOPE_IDENTITY()
                END

                -- Now set the action type's attribute value
                DECLARE @ActionTypeId int = (SELECT [Id] FROM [WorkflowActionType] WHERE [Guid] = '{7}')

                IF @ActionTypeId IS NOT NULL AND @AttributeId IS NOT NULL
                BEGIN

                    -- Delete existing attribute value
                    DELETE [AttributeValue]
                    WHERE [AttributeId] = @AttributeId
                    AND [EntityId] = @ActionTypeId

                    INSERT INTO [AttributeValue] (
                        [IsSystem],[AttributeId],[EntityId],
                        [Value],
                        [Guid])
                    VALUES(
                        1,@AttributeId,@ActionTypeId,
                        '{8}',
                        NEWID())

                END
",
                    actionEntityTypeGuid,
                    fieldTypeGuid,
                    attributeKey ?? attributeName.Replace( " ", string.Empty ),
                    attributeName,
                    attributeDescription.Replace( "'", "''" ),
                    attributeOrder,
                    attributeDefaultValue.Replace( "'", "''" ),
                    actionTypeGuid,
                    value.Replace( "'", "''" ) )
            );

        }
    }
}
