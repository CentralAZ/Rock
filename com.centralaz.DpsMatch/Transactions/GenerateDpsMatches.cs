// <copyright>
// Copyright by the Spark Development Network
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
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web;
using Rock;
using Rock.Communication;
using Rock.Data;
using Rock.MergeTemplates;
using Rock.Model;
using LumenWorks.Framework.IO.Csv;
using Rock.Transactions;

namespace com.centralaz.DpsMatch.Transactions
{
    public class GenerateDpsMatches : ITransaction
    {
        #region Properties

        /// <summary>
        /// Gets or sets the letter template.
        /// </summary>
        /// <value>
        /// The letter template.
        /// </value>
        public int? DpsFileId { get; set; }

        /// <summary>
        /// Gets or sets the statement template.
        /// </summary>
        /// <value>
        /// The statement template.
        /// </value>
        public Guid? WorkflowTypeGuid { get; set; }

        /// <summary>
        /// Gets or sets the database timeout.
        /// </summary>
        /// <value>
        /// The database timeout.
        /// </value>
        public int? DatabaseTimeout { get; set; }

        #endregion

        #region Methods

        /// <summary>
        /// Initializes a new instance of the <see cref="GenerateDpsMatches"/> class.
        /// </summary>
        public GenerateDpsMatches()
        {
        }

        /// <summary>
        /// Executes this instance.
        /// </summary>
        public void Execute()
        {
            if ( DpsFileId != null )
            {
                using ( var rockContext = new RockContext() )
                {

                    if ( DatabaseTimeout.HasValue )
                    {
                        rockContext.Database.CommandTimeout = DatabaseTimeout.Value;
                    }

                    //Get the excel file
                    BinaryFile binaryFile = new BinaryFileService( new RockContext() ).Get( DpsFileId.Value );

                    //For each row in excel file
                    Dictionary<string, object> parameters;
                    using ( CsvReader csvReader = new CsvReader( new StreamReader( binaryFile.ContentStream ), true ) )
                    {
                        while ( csvReader.ReadNextRecord() )
                        {
                            try
                            {
                                if ( !string.IsNullOrWhiteSpace( csvReader[csvReader.GetFieldIndex( "Last_Name" )] ) )
                                {
                                    //Build new SO Object
                                    parameters = new Dictionary<string, object>();
                                    parameters.Add( "LastName", csvReader[csvReader.GetFieldIndex( "Last_Name" )] );
                                    parameters.Add( "FirstName", csvReader[csvReader.GetFieldIndex( "First_Name" )] );
                                    if ( !string.IsNullOrWhiteSpace( csvReader[csvReader.GetFieldIndex( "MI" )] ) )
                                    {
                                        String middleName = csvReader[csvReader.GetFieldIndex( "MI" )];
                                        char middleInitial = middleName.ElementAt( 0 );
                                        parameters.Add( "MiddleInitial", middleInitial );
                                    }
                                    else
                                    {
                                        parameters.Add( "MiddleInitial", DBNull.Value );
                                    }
                                    parameters.Add( "Age", csvReader[csvReader.GetFieldIndex( "Age" )].AsInteger() );
                                    parameters.Add( "Height", csvReader[csvReader.GetFieldIndex( "HT" )].AsInteger() );
                                    parameters.Add( "Weight", csvReader[csvReader.GetFieldIndex( "WT" )].AsInteger() );
                                    parameters.Add( "Race", csvReader[csvReader.GetFieldIndex( "Race" )] );
                                    parameters.Add( "Sex", csvReader[csvReader.GetFieldIndex( "Sex" )] );
                                    parameters.Add( "Hair", csvReader[csvReader.GetFieldIndex( "Hair" )] );
                                    parameters.Add( "Eyes", csvReader[csvReader.GetFieldIndex( "Eyes" )] );

                                    parameters.Add( "ResidentialAddress", csvReader[csvReader.GetFieldIndex( "Res_Add" )] );
                                    parameters.Add( "ResidentialCity", csvReader[csvReader.GetFieldIndex( "Res_City" )] );
                                    parameters.Add( "ResidentialState", csvReader[csvReader.GetFieldIndex( "Res_State" )] );
                                    parameters.Add( "ResidentialZip", csvReader[csvReader.GetFieldIndex( "Res_Zip" )].AsInteger() );

                                    if ( csvReader.GetFieldIndex( "Verification Date" ) != -1 && !string.IsNullOrWhiteSpace( csvReader[csvReader.GetFieldIndex( "Verification Date" )] ) && csvReader[csvReader.GetFieldIndex( "Verification Date" )].AsDateTime().HasValue )
                                    {
                                        parameters.Add( "VerificationDate", csvReader[csvReader.GetFieldIndex( "Verification Date" )].AsDateTime().Value );
                                    }
                                    else
                                    {
                                        parameters.Add( "VerificationDate", DBNull.Value );
                                    }
                                    parameters.Add( "Offense", csvReader[csvReader.GetFieldIndex( "Offense" )] );
                                    parameters.Add( "OffenseLevel", csvReader[csvReader.GetFieldIndex( "Level" )].AsInteger() );
                                    parameters.Add( "Absconder", csvReader[csvReader.GetFieldIndex( "Absconder" )].AsBoolean() );
                                    parameters.Add( "ConvictingJurisdiction", csvReader[csvReader.GetFieldIndex( "Conviction_State" )] );
                                    if ( csvReader.GetFieldIndex( "Verification Date" ) != -1 && !string.IsNullOrWhiteSpace( csvReader[csvReader.GetFieldIndex( "Unverified" )] ) )
                                    {
                                        parameters.Add( "Unverified", csvReader[csvReader.GetFieldIndex( "Unverified" )].AsBoolean() );
                                    }
                                    else
                                    {
                                        parameters.Add( "Unverified", DBNull.Value );
                                    }
                                    parameters.Add( "KeyString", String.Format( "{0}{1}{2}{3}{4}{5}{6}", parameters["LastName"], parameters["FirstName"], parameters["Race"], parameters["Sex"], parameters["Hair"], parameters["Eyes"], parameters["ResidentialZip"] ) );

                                    DbService.ExecuteCommand( "_com_centralaz_spDpsMatch_Offender", System.Data.CommandType.StoredProcedure, parameters, DatabaseTimeout.Value );

                                }
                            }
                            catch ( Exception e )
                            {
                                ExceptionLogService.LogException( e, null );
                            }
                        }
                    }

                    DbService.ExecuteCommand( "_com_centralaz_spDpsMatch_Match", System.Data.CommandType.StoredProcedure, null, DatabaseTimeout.Value );

                    if ( WorkflowTypeGuid != null )
                    {
                        LaunchWorkflow( rockContext, WorkflowTypeGuid.Value );
                    }
                }
            }

        }

        private void LaunchWorkflow( RockContext rockContext, Guid workflowTypeGuid )
        {
            var workflowType = Rock.Web.Cache.WorkflowTypeCache.Get( workflowTypeGuid );
            if ( workflowType != null && ( workflowType.IsActive ?? true ) )
            {

                var workflow = Rock.Model.Workflow.Activate( workflowType, String.Format( "DPS Offender Match for {0}", DateTime.Now.ToShortDateString() ) );

                List<string> workflowErrors;
                new WorkflowService( rockContext ).Process( workflow, out workflowErrors );
            }

        }

        #endregion
    }
}