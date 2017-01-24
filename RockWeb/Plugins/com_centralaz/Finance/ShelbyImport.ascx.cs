// <copyright>
// Copyright by Central Christian Church
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
using System.ComponentModel;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Xml.Linq;
using Microsoft.AspNet.SignalR;
using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Security;
using Rock.Web.Cache;
using Rock.Web.UI.Controls;

namespace RockWeb.Plugins.com_centralaz.Finance
{
    /// <summary>
    /// Block to import a system with Shelby contributions data into Rock.
    /// 
    /// In a nutshell it works like this:
    ///  - User is asked to create/confirm Shelby Fund matching to Rock Accounts
    ///     - The Shelby PurCounter is added to a dictionary map (PurCounter -> FinancialAccount.Id)
    ///  - List of unique persons who contributed in Shelby is generated
    ///     - For each, a match is found or a new person/family record is created
    ///     - the Shelby NameCounter is added to a dictionary map (NameCounter -> Person.PersonAliasId)
    ///  - For each distinct batch (related to a contribution; select distinct BatchNu from [Shelby].[CNHst]) in Shelby
    ///         SELECT * FROM [Shelby].[CNBat] WHERE BatchNu IN (SELECT distinct BatchNu from [Shelby].[CNHst])
    ///     - Find matching or add FinancialBatch in Rock; if found, skip to next batch; when adding:
    ///         - Set the FinancialBatch.ForeignKey to the CNBat.BatchNu
    ///         - Set the FinancialBatch.ControlAmount to the CNBat.Total
    ///         - Set the FinancialBatch.Note to the CNBat.NuContr
    ///         - Set the FinancialBatch.CreatedByPersonAliasId to the CNBat.WhoSetup
    ///         - Set the FinancialBatch.CreatedDateTime to the CNBat.WhenSetup
    ///     - TBD the Shelby BatchNu is added to a dictionary map (BatchNu -> FinancialBatch.Id)
    ///     - For each contribution in Shelby
    ///         - If CNHst.Counter same as previous, use previous FinancialTransaction (don't create a new one)
    ///         - else, create FinancialTransaction
    ///             - Set the FinancialTransaction.TransactionTypeValueId = 53 (Contribution)
    ///             - Set the FinancialTransaction.SourceTypeValueId = (10=Website, 511=Kiosk, 512=Mobile Application, 513=On-Site Collection, 593=Bank Checks)
    ///             - Set the FinancialTransaction.Summary to the CNHst.Memo
    ///             - Set the FinancialTransactionDetail.TransactionCode to the CNHst.CheckNu
    ///         - Create FinancialTransactionDetail
    ///             - Set Amount = [CNHstDet].Amount
    ///             - Set AccountId = (lookup PurCounter AccountPurpose dictionary)
    ///             
    ///         - Create FinancialPaymentDetail 
    ///             - Set CurrencyTypeValueId =  (6=Cash, 9=Check, 156=Credit Card, 157=ACH, 1493=Non-Cash, 1554=Debit)
    ///                 - 6 if CheckNu="CASH"
    ///                 - 1493 if CheckNu="ONLINE", "GIVING*CENTER", "ONLINEGIVING", "PAYPAL"
    ///                 - 9 if CheckNu is all numbers (or "CHECK")
    ///                 
    ///         - Add FinancialPaymentDetail to FinancialTransaction.FinancialPaymentDetail
    ///         - Add FinancialTransactionDetail to FinancialTransaction.TransactionDetails
    ///         - Save
    ///     - commit batch transaction and move to next batch
    ///     
    /// </summary>
    /*
     * 
SELECT
	H.[Counter]
	,H.[Amount]
	,H.[BatchNu]
	,H.[CheckNu]
	,H.[CNDate]
	,H.[Memo]
	,H.[NameCounter]
	,H.[WhenSetup]
	,H.[WhenUpdated]
	,H.[WhoSetup]
	,H.[WhoUpdated]
	,H.[CheckType]
	,D.[PurCounter]
	,P.[Descr]
	,D.[Amount]
  FROM [ShelbyDB].[Shelby].[CNHst] H WITH(NOLOCK)
  INNER JOIN [ShelbyDB].[Shelby].[CNHstDet] D WITH(NOLOCK) ON D.[HstCounter] = H.[Counter]
  INNER JOIN [ShelbyDB].[Shelby].[CNPur] P WITH(NOLOCK) ON P.[Counter]= D.[PurCounter]
     * */
    [DisplayName( "Shelby Import" )]
    [Category( "com_centralaz > Finance" )]
    [Description( "Finance block to import contribution data from a Shelby database. It will add new people if a match cannot be made, import the batches, and financial transactions." )]

    [TextField( "Batch Name", "The name that should be used for the batches created", true, "Shelby Import", order: 0 )]
    [IntegerField( "Anonymous Giver PersonAliasID", "PersonAliasId to use in case of anonymous giver", true, order: 1 )]
    [DefinedValueField( Rock.SystemGuid.DefinedType.FINANCIAL_TRANSACTION_TYPE, "TransactionType", "The transaction type that designates a 'contribution' or donation.", true, order: 2 )]
    [DefinedValueField( Rock.SystemGuid.DefinedType.FINANCIAL_SOURCE_TYPE, "Default Transaction Source", "The default transaction source to use if a match is not found (Website, Kiosk, etc.).", true, order:3 )]
    [DefinedValueField( Rock.SystemGuid.DefinedType.FINANCIAL_CURRENCY_TYPE, "Default Tender Type Value", "The default tender type if a match is not found (Cash, Credit Card, etc.).", true, order: 4 )]
    [BooleanField( "Use Negative Foreign Keys", "Indicates whether Rock uses the negative of the Shelby reference ID for the contribution record's foreign key", false, order: 5 )]
    [TextField( "Source Mappings", "Held in Shelby's ContributionSource field, these correspond to Rock's TransactionSource (DefinedType). If you don't want to rename your current transaction source types, just map them here. Delimit them with commas or semicolons, and write them in the format 'Shelby_value=Rock_value'.", false, "", "Data Mapping", 1 )]
    [TextField( "Tender Mappings", "Held in the Shelby's ContributionType field, these correspond to Rock's TenderTypes (DefinedType). If you don't want to clutter your tender types, just map them here. Delimit them with commas or semicolons, and write them in the format 'Shelby_value=Rock_value'.", false, "", "Data Mapping", 2 )]
    [TextField( "Fund Code Mapping", "Held in the Shelby's FundCode field, these correspond to Rock's Account IDs (integer). Each FundCode should be mapped to a matching AccountId otherwise Rock will just use the same value. Delimit them with commas or semicolons, and write them in the format 'Shelby_value=Rock_value'.", false, "", "Data Mapping", 3 )]    
    [LinkedPage( "Batch Detail Page", "The page used to display the contributions for a specific batch", true, "", "Linked Pages", 0 )]
    [LinkedPage( "Contribution Detail Page", "The page used to display the contribution transaction details", true, "",  "Linked Pages", 1 )]
    [EncryptedTextField("Shelby DB Password", "", true, "", "Remote Shelby DB", 0 )]
    [TextField( "Fund Account Mappings", "The mapping between Shelby Funds and Rock Accounts. A comma delimited list of 'Shelby_value=Rock_value'.", false, "", "Data Mapping", 1 )]
    [GroupLocationTypeField( Rock.SystemGuid.GroupType.GROUPTYPE_FAMILY, "Address Type", "The location type to use for a new person's address.", false,
        Rock.SystemGuid.DefinedValue.GROUP_LOCATION_TYPE_HOME, "", 11 )]

    public partial class ShelbyImport : Rock.Web.UI.RockBlock
    {
        #region Fields
        /// <summary>
        /// This holds the reference to the RockMessageHub SignalR Hub context.
        /// </summary>
        private IHubContext _hubContext = GlobalHost.ConnectionManager.GetHubContext<RockMessageHub>();

        private static readonly string FUND_ACCOUNT_MAPPINGS = "FundAccountMappings";
        private int _anonymousPersonAliasId = 0;
        private List<string> _errors = new List<string>();
        private List<XElement> _errorElements = new List<XElement>();
        private int _personRecordTypeId = DefinedValueCache.Read( Rock.SystemGuid.DefinedValue.PERSON_RECORD_TYPE_PERSON.AsGuid() ).Id;
        private int _personStatusPending = DefinedValueCache.Read( Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_PENDING.AsGuid() ).Id;
        private int _transactionTypeIdContribution = DefinedValueCache.Read( Rock.SystemGuid.DefinedValue.TRANSACTION_TYPE_CONTRIBUTION.AsGuid() ).Id;

        private Regex reOnlyDigits = new Regex( @"^[0-9-\/\.]+$" );

        // Shelby Marital statuses: U, W, M, D, P, S
        private DefinedTypeCache _maritalStatusDefinedType = DefinedTypeCache.Read( Rock.SystemGuid.DefinedType.PERSON_MARITAL_STATUS.AsGuid() );

        // Holds the Fund Account Mappings block attribute as a dictionary (Shelby Purpose Counter -> Rock Account Id)
        private Dictionary<String, String> _fundAccountMappingDictionary = new Dictionary<string, string>();

        // Holds the Shelby NameCounter to Rock PersonAliasId map
        private Dictionary<int, int> _shelbyPersonMappingDictionary = new Dictionary<int, int>();

        // Holds the Shelby Batch to Rock Batch map
        private Dictionary<int, int> _shelbyBatchMappingDictionary = new Dictionary<int, int>();

        private Dictionary<int, FinancialAccount> _financialAccountCache = new Dictionary<int, FinancialAccount>();
        private Dictionary<string, DefinedValue> _tenderTypeDefinedValueCache = new Dictionary<string, DefinedValue>();
        private Dictionary<string, DefinedValue> _transactionSourceTypeDefinedValueCache = new Dictionary<string, DefinedValue>();

        private Dictionary<int, string> _accountNames = null;
        private Dictionary<int, string> AllAccounts
        {
            get
            {
                if ( _accountNames == null )
                {
                    _accountNames = new Dictionary<int, string>();
                    _accountNames.Add( -1, "" );
                    new FinancialAccountService( new RockContext() ).Queryable()
                        .OrderBy( a => a.Order )
                        .Select( a => new { a.Id, a.Name } )
                        .ToList()
                        .ForEach( a => _accountNames.Add( a.Id, a.Name ) );
                }
                return _accountNames;
            }
        }
        #endregion

        #region Base Control Methods

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Init" /> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnInit( EventArgs e )
        {
            base.OnInit( e );
            gContributions.GridRebind += gContributions_GridRebind;
            gContributions.RowDataBound += gContributions_RowDataBound;
            gErrors.GridRebind += gErrors_GridRebind;
            gErrors.RowDataBound += gErrors_RowDataBound;

            // this event gets fired after block settings are updated. it's nice to repaint the screen if these settings would alter it
            this.BlockUpdated += Block_BlockUpdated;
            this.AddConfigurationUpdateTrigger( upnlContent );

            ScriptManager scriptManager = ScriptManager.GetCurrent( Page );
            scriptManager.RegisterPostBackControl( lbImport );

            RockPage.AddScriptLink( "~/Scripts/jquery.signalR-2.2.0.min.js", fingerprint: false );

            _fundAccountMappingDictionary = Regex.Matches( GetAttributeValue( FUND_ACCOUNT_MAPPINGS ), @"\s*(.*?)\s*=\s*(.*?)\s*(;|,|$)" )
                .OfType<Match>()
                .ToDictionary( m => m.Groups[1].Value, m => m.Groups[2].Value );
        }

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Load" /> event.
        /// </summary>
        /// <param name="e">The <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );

            // Set timeout for up to 30 minutes (just like installer)
            Server.ScriptTimeout = 1800;
            ScriptManager.GetCurrent( Page ).AsyncPostBackTimeout = 1800;

            if ( !Page.IsPostBack )
            {
                VerifyOrSetAccountMappings();
                var id = GetAttributeValue( "AnonymousGiverPersonAliasID" ).AsIntegerOrNull();

                if ( id == null || string.IsNullOrEmpty( GetAttributeValue( "ContributionDetailPage" ) )  || string.IsNullOrEmpty( GetAttributeValue( "BatchDetailPage" ) ) )
                {
                    nbMessage.Text = "Invalid block settings.";
                    return;
                }

                tbBatchName.Text = GetAttributeValue( "BatchName" );
                BindCampusPicker();
                BindGrid();
                BindErrorGrid();
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// Handles the Click event of the lbImport control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void lbImport_Click( object sender, EventArgs e )
        {
            // clear any old errors:
            _errors = new List<string>();
            _errorElements = new List<XElement>();
            nbMessage.Text = "";
            pnlErrors.Visible = false;

            try
            {
                _hubContext.Clients.All.showLog();
                ProcessPeople();
                ProcessBatches();
                ProcessTransactions();

                BindGrid();
                pnlConfiguration.Visible = false;
            }
            catch ( Exception ex )
            {
                nbMessage.Text = "Errors found.";
                pnlErrors.Visible = true;
            }

            _shelbyBatchMappingDictionary.Clear();
            _fundAccountMappingDictionary.Clear();
            _shelbyPersonMappingDictionary.Clear();

            _shelbyBatchMappingDictionary = null;
            _fundAccountMappingDictionary = null;
            _shelbyPersonMappingDictionary = null;

            if ( _errors.Count > 0 )
            {
                nbMessage.Text = "Errors found.";
                pnlErrors.Visible = true;
                BindErrorGrid();
            }

            //if ( fuImport.HasFile )
            //{
            //    // clear any old errors:
            //    _errors = new List<string>();
            //    _errorElements = new List<XElement>();
            //    nbMessage.Text = "";
            //    pnlErrors.Visible = false;

            //    RockContext rockContext = new RockContext();
            //    FinancialBatchService financialBatchService = new FinancialBatchService( rockContext );
            //    DefinedValueService definedValueService = new DefinedValueService( rockContext );
            //    PersonAliasService personAliasService = new PersonAliasService( rockContext );

            //    // Find/verify the anonymous person alias ID
            //    var personAlias = personAliasService.GetByAliasId( GetAttributeValue( "AnonymousGiverPersonAliasID" ).AsInteger() );
            //    if ( personAlias == null )
            //    {
            //        nbMessage.Text = "Invalid AnonymousGiverPersonAliasID block setting.";
            //        return;
            //    }
            //    else
            //    {
            //        _anonymousPersonAliasId = personAlias.Id;
            //    }

            //    _financialBatch = new FinancialBatch();
            //    _financialBatch.Name = tbBatchName.Text;
            //    _financialBatch.BatchStartDateTime = Rock.RockDateTime.Now;

            //    int? campusId = cpCampus.SelectedCampusId;

            //    if ( campusId != null )
            //    {
            //        _financialBatch.CampusId = campusId;
            //    }
            //    else
            //    {
            //        var campuses = CampusCache.All();
            //        _financialBatch.CampusId = campuses.FirstOrDefault().Id;
            //    }

            //    financialBatchService.Add( _financialBatch );
            //    rockContext.SaveChanges();

            //    Dictionary<string, string> dictionaryInfo = new Dictionary<string, string>();
            //    dictionaryInfo.Add( "batchId", _financialBatch.Id.ToString() );
            //    string url = LinkedPageUrl( "BatchDetailPage", dictionaryInfo );
            //    String theString = String.Format( "Batch <a href=\"{0}\">{1}</a> was created.", url, _financialBatch.Id.ToString() );
            //    nbBatch.Text = theString;
            //    nbBatch.Visible = true;

            //    var xdoc = XDocument.Load( System.Xml.XmlReader.Create( fuImport.FileContent ) );
            //    var elemDonations = xdoc.Element( "Donation" );

            //    Dictionary<String, String> tenderMappingDictionary = Regex.Matches( GetAttributeValue( "TenderMappings" ), @"\s*(.*?)\s*=\s*(.*?)\s*(;|,|$)" )
            //        .OfType<Match>()
            //        .ToDictionary( m => m.Groups[1].Value, m => m.Groups[2].Value );

            //    Dictionary<String, String> sourceMappingDictionary = Regex.Matches( GetAttributeValue( "SourceMappings" ), @"\s*(.*?)\s*=\s*(.*?)\s*(;|,|$)" )
            //        .OfType<Match>()
            //        .ToDictionary( m => m.Groups[1].Value, m => m.Groups[2].Value );

            //    Dictionary<int, int> fundCodeMappingDictionary = Regex.Matches( GetAttributeValue( "FundCodeMapping" ), @"\s*(.*?)\s*=\s*(.*?)\s*(;|,|$)" )
            //        .OfType<Match>()
            //        .ToDictionary( m => m.Groups[1].Value.AsInteger(), m => m.Groups[2].Value.AsInteger() );

            //    foreach ( var elemGift in elemDonations.Elements( "Gift" ) )
            //    {
            //        ProcessGift( elemGift, tenderMappingDictionary, sourceMappingDictionary, fundCodeMappingDictionary, rockContext );
            //    }

            //    rockContext.SaveChanges();

            //    BindGrid();

            //    if ( _errors.Count > 0 )
            //    {
            //        nbMessage.Text = "Errors found.";
            //        BindErrorGrid();
            //    }

            //    _financialAccountCache = null;
            //    _tenderTypeDefinedValueCache = null;
            //}
        }

        /// <summary>
        /// Handles the GridRebind event of the gPledges control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void gContributions_GridRebind( object sender, EventArgs e )
        {
            BindGrid();
        }

        /// <summary>
        /// Handles the GridRebind event of the gErrors control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void gErrors_GridRebind( object sender, EventArgs e )
        {
            BindErrorGrid();
        }

        /// <summary>
        /// Handles the RowDataBound event of the gErrors control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="GridViewRowEventArgs"/> instance containing the event data.</param>
        protected void gErrors_RowDataBound( object sender, GridViewRowEventArgs e )
        {
            if ( e.Row.RowType == DataControlRowType.DataRow )
            {
                var elemError = e.Row.DataItem as XElement;
                if ( elemError != null )
                {
                    Literal lReferenceNumber = e.Row.FindControl( "lReferenceNumber" ) as Literal;
                    if ( lReferenceNumber != null && elemError.Element( "ReferenceNumber" ) != null )
                    {
                        lReferenceNumber.Text = elemError.Element( "ReferenceNumber" ).Value.ToString();
                    }

                    Literal lChurchCode = e.Row.FindControl( "lChurchCode" ) as Literal;
                    if ( lChurchCode != null && elemError.Element( "ChurchCode" ) != null )
                    {
                        lChurchCode.Text = elemError.Element( "ChurchCode" ).Value.ToString();
                    }

                    Literal lIndividualId = e.Row.FindControl( "lIndividualId" ) as Literal;
                    if ( lIndividualId != null && elemError.Element( "IndividualID" ) != null )
                    {
                        lIndividualId.Text = elemError.Element( "IndividualID" ).Value.ToString();
                    }

                    Literal lContributorName = e.Row.FindControl( "lContributorName" ) as Literal;
                    if ( lContributorName != null && elemError.Element( "ContributorName" ) != null )
                    {
                        lContributorName.Text = elemError.Element( "ContributorName" ).Value.ToString();
                    }

                    Literal lFundName = e.Row.FindControl( "lFundName" ) as Literal;
                    if ( lFundName != null && elemError.Element( "FundName" ) != null )
                    {
                        lFundName.Text = elemError.Element( "FundName" ).Value.ToString();
                    }

                    Literal lFundCode = e.Row.FindControl( "lFundCode" ) as Literal;
                    if ( lFundCode != null && elemError.Element( "FundCode" ) != null )
                    {
                        lFundCode.Text = elemError.Element( "FundCode" ).Value.ToString();
                    }

                    Literal lReceivedDate = e.Row.FindControl( "lReceivedDate" ) as Literal;
                    if ( lReceivedDate != null && elemError.Element( "ReceivedDate" ) != null )
                    {
                        DateTime receivedDate = DateTime.Parse( elemError.Element( "ReceivedDate" ).Value );
                        lReceivedDate.Text = receivedDate.ToString();
                    }

                    Literal lAmount = e.Row.FindControl( "lAmount" ) as Literal;
                    if ( lAmount != null && elemError.Element( "Amount" ) != null )
                    {
                        lAmount.Text = elemError.Element( "Amount" ).Value.ToString();
                    }

                    Literal lTransactionId = e.Row.FindControl( "lTransactionId" ) as Literal;
                    if ( lTransactionId != null && elemError.Element( "TransactionID" ) != null )
                    {
                        lTransactionId.Text = elemError.Element( "TransactionID" ).Value.ToString();
                    }

                    Literal lContributionType = e.Row.FindControl( "lContributionType" ) as Literal;
                    if ( lContributionType != null && elemError.Element( "ContributionType" ) != null )
                    {
                        lContributionType.Text = elemError.Element( "ContributionType" ).Value.ToString();
                    }

                    Literal lError = e.Row.FindControl( "lError" ) as Literal;
                    if ( lError != null && elemError.Element( "Error" ) != null )
                    {
                        lError.Text = elemError.Element( "Error" ).Value.ToString();
                    }
                }
            }
        }

        /// <summary>
        /// Handles the RowDataBound event of the gContributions control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="GridViewRowEventArgs"/> instance containing the event data.</param>
        protected void gContributions_RowDataBound( object sender, GridViewRowEventArgs e )
        {
            if ( e.Row.RowType == DataControlRowType.DataRow )
            {
                FinancialTransactionDetail financialTransactionDetail = e.Row.DataItem as FinancialTransactionDetail;
                if ( financialTransactionDetail != null )
                {
                    Literal lTransactionID = e.Row.FindControl( "lTransactionID" ) as Literal;
                    if ( lTransactionID != null )
                    {
                        Dictionary<string, string> dictionaryInfo = new Dictionary<string, string>();
                        dictionaryInfo.Add( "transactionId", financialTransactionDetail.TransactionId.ToString() );
                        string url = LinkedPageUrl( "ContributionDetailPage", dictionaryInfo );
                        String theString = String.Format( "<a href=\"{0}\">{1}</a>", url, financialTransactionDetail.TransactionId.ToString() );
                        lTransactionID.Text = theString;
                    }

                    Literal lFullName = e.Row.FindControl( "lFullName" ) as Literal;
                    if ( lFullName != null )
                    {
                        String url = ResolveUrl( string.Format( "~/Person/{0}", financialTransactionDetail.Transaction.AuthorizedPersonAlias.PersonId ) );
                        String theString = String.Format( "<a href=\"{0}\">{1}</a>", url, financialTransactionDetail.Transaction.AuthorizedPersonAlias.Person.FullName );
                        lFullName.Text = theString;
                    }
                }
            }
        }

        /// <summary>
        /// Handles the BlockUpdated event of the control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void Block_BlockUpdated( object sender, EventArgs e )
        {
            nbMessage.Text = "";
            var id = GetAttributeValue( "AnonymousGiverPersonAliasID" ).AsIntegerOrNull();
            if ( id == null || string.IsNullOrEmpty( GetAttributeValue( "ContributionDetailPage" ) ) || string.IsNullOrEmpty( GetAttributeValue( "BatchDetailPage" ) ) )
            {
                nbMessage.Text = "Invalid block settings.";
                return;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// List of unique persons who contributed in Shelby is generated
        ///     - For each, a match is found or a new person/family record is created
        ///     - the Shelby NameCounter is added to a dictionary map (NameCounter -> Person.Id)
        /// </summary>
        private void ProcessPeople()
        {
            int totalCount = 0;
            int counter = 0;
            try
            {
                RockContext rockContext = new RockContext();
                rockContext.Configuration.AutoDetectChangesEnabled = false;
                PersonService personService = new PersonService( rockContext );
                var connectionString = GetConnectionString();
                using ( SqlConnection connection = new SqlConnection( connectionString ) )
                {
                    connection.Open();
                    SqlCommand command = new SqlCommand();
                    command.Connection = connection;

                    // First count the total people
                    command.CommandText = @"SELECT COUNT(1) as 'Count'
FROM [Shelby].[NANames] N WITH(NOLOCK)
LEFT JOIN [Shelby].[NAAddresses] A WITH(NOLOCK) ON A.AddressCounter = N.MainAddress
WHERE N.NameCounter IN ( SELECT H.NameCounter FROM [Shelby].[CNHst] H WITH(NOLOCK) )";
                    using ( SqlDataReader reader = command.ExecuteReader() )
                    {
                        if ( reader.HasRows )
                        {
                            while ( reader.Read() )
                            {
                                totalCount = ( int ) reader["Count"];
                            }
                        }
                    }

                    command.CommandText = @"SELECT N.[NameCounter], N.[EMailAddress], N.[Gender], N.[Salutation], N.[FirstMiddle], N.[LastName], N.[MaritalStatus], A.[Adr1], A.[Adr2], A.[City], A.[State], A.[PostalCode]
FROM [Shelby].[NANames] N WITH(NOLOCK)
LEFT JOIN [Shelby].[NAAddresses] A WITH(NOLOCK) ON A.[AddressCounter] = N.[MainAddress]
WHERE N.[NameCounter] IN ( SELECT H.[NameCounter] FROM [Shelby].[CNHst] H WITH(NOLOCK) )";

                    using ( SqlDataReader reader = command.ExecuteReader() )
                    {
                        if ( reader.HasRows )
                        {
                            while ( reader.Read() )
                            {
                                counter++;
                                var shelbyPerson = new ShelbyPerson( reader );
                                int nameCounter = shelbyPerson.NameCounter;

                                int? personAliasId = FindOrCreateNewPerson( personService, shelbyPerson );
                                NotifyClientProcessingUsers( counter, totalCount );
                                //NotifyClient( "{0}", nameCounter );

                                if ( personAliasId != null )
                                {
                                    _shelbyPersonMappingDictionary.AddOrReplace( nameCounter, personAliasId.Value );
                                }

#if DEBUG
if (counter > 3000) break;
#endif
                            }
                        }

                        reader.Close();
                    }
                }
            }
            catch ( Exception ex )
            {
                nbMessage.Text = string.Format( "Your database block settings are not valid or the remote database server is offline or mis-configured. {0}<br/><pre>{1}</pre>", ex.Message, ex.StackTrace );
            }
        }

        /// <summary>
        /// Process the batches from the remote Shelby DB.
        /// 
        ///  - For each distinct batch (related to a contribution; select distinct BatchNu from [Shelby].[CNHst]) in Shelby
        ///         SELECT * FROM [Shelby].[CNBat] WHERE BatchNu IN (SELECT distinct BatchNu from [Shelby].[CNHst])
        ///     - Find matching or add FinancialBatch in Rock; if found, skip to next batch; when adding:
        ///         - Set the FinancialBatch.ForeignKey to the CNBat.BatchNu
        ///         - Set the FinancialBatch.ControlAmount to the CNBat.Total
        ///         - Set the FinancialBatch.Note to the CNBat.NuContr
        ///         - Set the FinancialBatch.CreatedByPersonAliasId to the CNBat.WhoSetup
        ///         - Set the FinancialBatch.CreatedDateTime to the CNBat.WhenSetup
        ///     - TBD the Shelby BatchNu is added to a dictionary map (BatchNu -> FinancialBatch.Id)
        ///     - commit batch transaction and move to next batch
        /// </summary>
        private void ProcessBatches()
        {
            int totalCount = 0;
            int counter = 0;
            try
            {
                RockContext rockContext = new RockContext();
                rockContext.Configuration.AutoDetectChangesEnabled = false;
                FinancialBatchService batchService = new FinancialBatchService( rockContext );
                var connectionString = GetConnectionString();
                using ( SqlConnection connection = new SqlConnection( connectionString ) )
                {
                    connection.Open();
                    SqlCommand command = new SqlCommand();
                    command.Connection = connection;

                    // First count the total
                    command.CommandText = @"SELECT COUNT(1) as 'Count' FROM [Shelby].[CNBat] WITH(NOLOCK) WHERE BatchNu IN (SELECT distinct BatchNu from [Shelby].[CNHst] WITH(NOLOCK))";
                    using ( SqlDataReader reader = command.ExecuteReader() )
                    {
                        if ( reader.HasRows )
                        {
                            while ( reader.Read() )
                            {
                                totalCount = ( int ) reader["Count"];
                            }
                        }
                    }

                    command.CommandText = @"SELECT [BatchNu], [NuContr], [Total], [WhenPosted], [WhenSetup], [WhoSetup]  FROM [Shelby].[CNBat] WITH(NOLOCK) WHERE BatchNu IN (SELECT distinct BatchNu from [Shelby].[CNHst] WITH(NOLOCK))";

                    using ( SqlDataReader reader = command.ExecuteReader() )
                    {
                        if ( reader.HasRows )
                        {
                            while ( reader.Read() )
                            {
                                counter++;
                                var shelbyBatch = new ShelbyBatch( reader );

                                int? rockBatchId = FindOrCreateNewBatch( batchService, shelbyBatch );
                                NotifyClientProcessingBatches( counter, totalCount );

                                if ( rockBatchId != null )
                                {
                                    _shelbyBatchMappingDictionary.AddOrReplace( shelbyBatch.BatchNu, rockBatchId.Value );
                                }
#if DEBUG
if (counter > 300) break;
#endif
                            }
                        }

                        reader.Close();
                    }
                }
            }
            catch ( Exception ex )
            {
                nbMessage.Text = string.Format( "Your database block settings are not valid or the remote database server is offline or mis-configured. {0}<br/><pre>{1}</pre>", ex.Message, ex.StackTrace );
            }
        }

        /// <summary>
        /// Finds and returns the matching batchId or creates a new one and returns the batchId: 
        ///   - Find matching or add FinancialBatch in Rock; if found, skip to next batch; when adding:
        ///   - Set the FinancialBatch.ForeignKey to the CNBat.BatchNu
        ///   - Set the FinancialBatch.ControlAmount to the CNBat.Total
        ///   - Set the FinancialBatch.Note to the CNBat.NuContr
        ///   - Set the FinancialBatch.CreatedByPersonAliasId to the CNBat.WhoSetup
        ///   - Set the FinancialBatch.CreatedDateTime to the CNBat.WhenSetup
        /// </summary>
        private int? FindOrCreateNewBatch( FinancialBatchService batchService, ShelbyBatch shelbyBatch )
        {
            string batchNu = shelbyBatch.BatchNu.ToStringSafe();
            var exactBatch = batchService.Queryable().Where( p => p.ForeignKey == batchNu ).FirstOrDefault();

            if ( exactBatch != null )
            {
                return exactBatch.Id;
            }

            var financialBatch = new FinancialBatch();
            financialBatch.Name = tbBatchName.Text;
            financialBatch.BatchStartDateTime = shelbyBatch.WhenSetup; // Confirmed by Michele A.
            financialBatch.ControlAmount = shelbyBatch.Total;
            financialBatch.ForeignKey = batchNu;
            financialBatch.Note = shelbyBatch.NuContr.ToStringSafe();
            financialBatch.CreatedDateTime = shelbyBatch.WhenSetup;

            int? campusId = cpCampus.SelectedCampusId;

            if ( campusId != null )
            {
                financialBatch.CampusId = campusId;
            }
            else
            {
                var campuses = CampusCache.All();
                financialBatch.CampusId = campuses.FirstOrDefault().Id;
            }

            batchService.Add( financialBatch );
            RockContext rockContext = (RockContext) batchService.Context;
            rockContext.ChangeTracker.DetectChanges();
            rockContext.SaveChanges( disablePrePostProcessing: true );

            return financialBatch.Id;
        }

        /// <summary>
        /// Process transactions.
        ///     - For each contribution in Shelby
        ///         - If CNHst.Counter same as previous, use previous FinancialTransaction (don't create a new one)
        ///         - else, create FinancialTransaction
        ///             - Set the FinancialTransaction.TransactionTypeValueId = 53 (Contribution)
        ///             - Set the FinancialTransaction.SourceTypeValueId = (10=Website, 511=Kiosk, 512=Mobile Application, 513=On-Site Collection, 593=Bank Checks)
        ///             - Set the FinancialTransaction.Summary to the CNHst.Memo
        ///             - Set the FinancialTransactionDetail.TransactionCode to the CNHst.CheckNu
        ///         - Create FinancialTransactionDetail
        ///             - Set Amount = [CNHstDet].Amount
        ///             - Set AccountId = (lookup PurCounter AccountPurpose dictionary)
        ///             
        ///         - Create FinancialPaymentDetail 
        ///             - Set CurrencyTypeValueId =  (6=Cash, 9=Check, 156=Credit Card, 157=ACH, 1493=Non-Cash, 1554=Debit)
        ///                 - 6 if CheckNu="CASH"
        ///                 - 1493 if CheckNu="ONLINE", "GIVING*CENTER", "ONLINEGIVING", "PAYPAL"
        ///                 - 9 if CheckNu is all numbers (or "CHECK")
        ///                 
        ///         - Add FinancialPaymentDetail to FinancialTransaction.FinancialPaymentDetail
        ///         - Add FinancialTransactionDetail to FinancialTransaction.TransactionDetails
        ///         - Save
        /// </summary>
        private void ProcessTransactions()
        {
            int totalCount = 0;
            int counter = 0;
            int previousTransactionCounter = -1;
            var shelbyContributionsSet = new Queue<ShelbyContribution>();
            try
            {
                RockContext rockContext = new RockContext();
                rockContext.Configuration.AutoDetectChangesEnabled = false;
                FinancialTransactionService transactionService = new FinancialTransactionService( rockContext );
                var connectionString = GetConnectionString();
                using ( SqlConnection connection = new SqlConnection( connectionString ) )
                {
                    connection.Open();
                    SqlCommand command = new SqlCommand();
                    command.Connection = connection;

                    // First count the total
                    command.CommandText = @"SELECT COUNT(1) as 'Count' FROM [ShelbyDB].[Shelby].[CNHst] H WITH(NOLOCK)";
                    using ( SqlDataReader reader = command.ExecuteReader() )
                    {
                        if ( reader.HasRows )
                        {
                            while ( reader.Read() )
                            {
                                totalCount = ( int ) reader["Count"];
                            }
                        }
                    }

                    command.CommandText = @"SELECT
	H.[Counter]
	,H.[Amount]
	,H.[BatchNu]
	,H.[CheckNu]
	,H.[CNDate]
	,H.[Memo]
	,H.[NameCounter]
	,H.[WhenSetup]
	,H.[WhenUpdated]
	,H.[WhoSetup]
	,H.[WhoUpdated]
	,H.[CheckType]
    ,D.[Counter] as 'DetailCounter'
	,D.[PurCounter]
	,P.[Descr]
	,D.[Amount] as 'PurAmount'
  FROM [ShelbyDB].[Shelby].[CNHst] H WITH(NOLOCK)
  INNER JOIN [ShelbyDB].[Shelby].[CNHstDet] D WITH(NOLOCK) ON D.[HstCounter] = H.[Counter]
  INNER JOIN [ShelbyDB].[Shelby].[CNPur] P WITH(NOLOCK) ON P.[Counter]= D.[PurCounter]
  ORDER BY H.[Counter]
";

                    using ( SqlDataReader reader = command.ExecuteReader() )
                    {
                        if ( reader.HasRows )
                        {
                            while ( reader.Read() )
                            {
                                counter++;
                                var shelbyContribution = new ShelbyContribution( reader );

                                // If we're on the first item, then the "previous" is this first one...
                                if ( previousTransactionCounter == -1 )
                                {
                                    previousTransactionCounter = shelbyContribution.Counter;
                                }

                                // Is the next item just another detail record for the same transaction?
                                // If so, just add it to the set.
                                if ( previousTransactionCounter == shelbyContribution.Counter )
                                {
                                    shelbyContributionsSet.Enqueue( shelbyContribution );
                                }
                                else
                                {
                                    // Otherwise we finish/write the previous set, and then clear the set and move to the next item
                                    FindOrCreateTransaction( transactionService, shelbyContributionsSet );
                                    shelbyContributionsSet.Enqueue( shelbyContribution );
                                    previousTransactionCounter = shelbyContribution.Counter;
                                }

                                NotifyClientProcessingTransactions( counter, totalCount );
#if DEBUG
if (counter > 900) break;
#endif
                            }

                            // Check the last set and finish/write it...
                           FindOrCreateTransaction( transactionService, shelbyContributionsSet );
                        }

                        reader.Close();
                    }
                }
            }
            catch ( Exception ex )
            {
                nbMessage.Text = string.Format( "Your database block settings are not valid or the remote database server is offline or mis-configured. {0}<br/><pre>{1}</pre>", ex.Message, ex.StackTrace );
            }
        }

        /// <summary>
        /// Finds and returns the matching financial transaction or creates a new one and returns it: 
        ///     - For each new transaction and detail in the set...
        ///         - If CNHst.Counter same as previous, use previous FinancialTransaction (don't create a new one)
        ///         - else, create FinancialTransaction
        ///             - Set the FinancialTransaction.TransactionTypeValueId = 53 (Contribution)
        ///             - Set the FinancialTransaction.SourceTypeValueId = (10=Website, 511=Kiosk, 512=Mobile Application, 513=On-Site Collection, 593=Bank Checks)
        ///             - Set the FinancialTransaction.Summary to the CNHst.Memo
        ///             - Set the FinancialTransaction.TransactionCode to the CNHst.CheckNu
        ///         - Create FinancialTransactionDetail
        ///             - Set Amount = [CNHstDet].Amount
        ///             - Set AccountId = (lookup PurCounter AccountPurpose dictionary)
        ///             
        ///         - Create FinancialPaymentDetail 
        ///             - Set CurrencyTypeValueId =  (6=Cash, 9=Check, 156=Credit Card, 157=ACH, 1493=Non-Cash, 1554=Debit)
        ///                 - 6 if CheckNu="CASH"
        ///                 - 1493 if CheckNu="ONLINE", "GIVING*CENTER", "ONLINEGIVING", "PAYPAL"
        ///                 - 9 if CheckNu is all numbers (or "CHECK")
        ///                 
        ///         - Add FinancialPaymentDetail to FinancialTransaction.FinancialPaymentDetail
        ///         - Add FinancialTransactionDetail to FinancialTransaction.TransactionDetails
        ///         - Save
        /// </summary>
        private void FindOrCreateTransaction( FinancialTransactionService transactionService, Queue<ShelbyContribution> shelbyContributionsSet )
        {
            var shelbyContribution = shelbyContributionsSet.Dequeue();

            try
            {
                string counter = shelbyContribution.Counter.ToStringSafe();
                FinancialTransaction financialTransaction = transactionService.Queryable().Where( p => p.ForeignKey == counter ).FirstOrDefault();

                if ( financialTransaction == null )
                {
                    financialTransaction = new FinancialTransaction();
                    //financialTransaction.TotalAmount = shelbyContribution.Amount;
                    financialTransaction.TransactionTypeValueId = _transactionTypeIdContribution;
                    financialTransaction.Summary = shelbyContribution.Memo;
                    financialTransaction.TransactionCode = shelbyContribution.CheckNu;
                    financialTransaction.ProcessedDateTime = Rock.RockDateTime.Now;
                    financialTransaction.TransactionDateTime = shelbyContribution.WhenSetup;
                    financialTransaction.ForeignKey = shelbyContribution.Counter.ToStringSafe();
                    financialTransaction.AuthorizedPersonAliasId = _shelbyPersonMappingDictionary[shelbyContribution.NameCounter];
                    financialTransaction.BatchId = _shelbyBatchMappingDictionary[shelbyContribution.BatchNu];

                    if ( shelbyContribution.CheckNu.Contains( "cash" ) )
                    {
                        financialTransaction.SourceTypeValueId = DefinedValueCache.Read( Rock.SystemGuid.DefinedValue.FINANCIAL_SOURCE_TYPE_ONSITE_COLLECTION.AsGuid() ).Id;
                    }
                    else if ( shelbyContribution.CheckNu.Contains( "kiosk" ) )
                    {
                        financialTransaction.SourceTypeValueId = DefinedValueCache.Read( Rock.SystemGuid.DefinedValue.FINANCIAL_SOURCE_TYPE_KIOSK.AsGuid() ).Id;
                    }
                    else if ( shelbyContribution.CheckNu.StartsWith( "on" ) )
                    {
                        financialTransaction.SourceTypeValueId = DefinedValueCache.Read( Rock.SystemGuid.DefinedValue.FINANCIAL_SOURCE_TYPE_WEBSITE.AsGuid() ).Id;
                    }
                    else
                    {
                        financialTransaction.SourceTypeValueId = DefinedValueCache.Read( Rock.SystemGuid.DefinedValue.FINANCIAL_SOURCE_TYPE_ONSITE_COLLECTION.AsGuid() ).Id;
                    }

                    // set up the necessary Financial Payment Detail record
                    if ( financialTransaction.FinancialPaymentDetail == null )
                    {
                        financialTransaction.FinancialPaymentDetail = new FinancialPaymentDetail();
                        financialTransaction.FinancialPaymentDetail.ForeignKey = shelbyContribution.DetailCounter.ToStringSafe();
                        financialTransaction.FinancialPaymentDetail.CreatedDateTime = shelbyContribution.WhenSetup;

                        // Now find the matching tender type...
                        // Get the tender type and put in cache if we've not encountered it before.
                        if ( shelbyContribution.CheckNu.Contains( "cash" ) )
                        {
                            financialTransaction.FinancialPaymentDetail.CurrencyTypeValueId = DefinedValueCache.Read( Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_CASH.AsGuid() ).Id;
                        }
                        else if ( reOnlyDigits.IsMatch( shelbyContribution.CheckNu ) )
                        {
                            financialTransaction.FinancialPaymentDetail.CurrencyTypeValueId = DefinedValueCache.Read( Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_CHECK.AsGuid() ).Id;
                        }
                        else
                        {
                            financialTransaction.FinancialPaymentDetail.CurrencyTypeValueId = DefinedValueCache.Read( Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_NONCASH.AsGuid() ).Id;
                        }
                    }

                    FinancialTransactionDetail transactionDetail = new FinancialTransactionDetail();
                    transactionDetail.AccountId = _fundAccountMappingDictionary[shelbyContribution.PurCounter.ToString()].AsInteger();
                    transactionDetail.ForeignKey = shelbyContribution.DetailCounter.ToStringSafe();
                    transactionDetail.Amount = shelbyContribution.PurAmount;
                    financialTransaction.TransactionDetails.Add( transactionDetail );
                }
                else
                {
                    // TODO: verify this should not happen because we're dealing with the whole set at once.
                    _errors.Add( string.Format( "Transaction already existed!?? (Shelby CNHst.Counter: {0})", shelbyContribution.Counter ) );
                }

                while ( shelbyContributionsSet.Count > 0 )
                {
                    shelbyContribution = shelbyContributionsSet.Dequeue();
                    FinancialTransactionDetail transactionDetail = new FinancialTransactionDetail();
                    transactionDetail.AccountId = _fundAccountMappingDictionary[shelbyContribution.PurCounter.ToString()].AsInteger();
                    transactionDetail.ForeignKey = shelbyContribution.DetailCounter.ToStringSafe();
                    transactionDetail.Amount = shelbyContribution.PurAmount;
                    financialTransaction.TransactionDetails.Add( transactionDetail );
                }

                // Now save/write the whole set...
                transactionService.Add( financialTransaction );
                RockContext rockContext = ( RockContext ) transactionService.Context;
                rockContext.ChangeTracker.DetectChanges();
                rockContext.SaveChanges( disablePrePostProcessing: true );
            }
            catch ( Exception ex )
            {
                _errors.Add( shelbyContribution.Counter.ToStringSafe() );
            }
        }

        /// <summary>
        /// Finds a matching person or creates a new person in the db. When new people are created:
        ///   - it will store the person's Shelby NameCounter to the Rock person's ForeignKey field
        ///   - it will use the selected campus as the person's/family campus.
        /// </summary>
        /// <param name="personService">The person service.</param>
        /// <param name="shelbyPerson">The shelby person.</param>
        /// <returns>The person's PersonAliasId</returns>
        private int? FindOrCreateNewPerson( PersonService personService, ShelbyPerson shelbyPerson )
        {
            int? personAliasId = null;
            string firstName = ( shelbyPerson.Salutation != string.Empty ) ? shelbyPerson.Salutation : shelbyPerson.FirstMiddle;
            string namecounter = shelbyPerson.NameCounter.ToStringSafe();

            var exactPerson = personService.Queryable().Where( p => p.ForeignKey == namecounter ).FirstOrDefault();

            if ( exactPerson != null )
            {
                personAliasId = exactPerson.PrimaryAliasId;
            }

            if ( personAliasId == null && firstName != string.Empty && shelbyPerson.LastName != string.Empty )
            {
                var people = personService.GetByFirstLastName( firstName, shelbyPerson.LastName, true, true );

                // find any matches?
                if ( people.Any() )
                {
                    // If there's only one match, use it...
                    if ( people.Count() == 1 )
                    {
                        personAliasId = people.FirstOrDefault().PrimaryAliasId;
                    }
                    // otherwise, do any have the same email?
                    else if ( shelbyPerson.EmailAddress != string.Empty )
                    {
                        var peopleWithEmail = people.Where( p => p.Email == ( string ) shelbyPerson.EmailAddress );
                        if ( peopleWithEmail != null && peopleWithEmail.Count() == 1 )
                        {
                            var match = peopleWithEmail.FirstOrDefault();
                            personAliasId = match.PrimaryAliasId;
                        }
                    }
                }
            }

            // If no match was found, try matching just by email address
            if ( personAliasId == null && shelbyPerson.EmailAddress != string.Empty )
            {
                var people = personService.GetByEmail( shelbyPerson.EmailAddress, true, true );
                if ( people.Any() && people.Count() == 1 )
                {
                    personAliasId = people.FirstOrDefault().PrimaryAliasId;
                }
            }

            // If no match was still found, add a new person/family
            if ( personAliasId == null )
            {
                var person = new Person();
                person.IsSystem = false;
                person.IsEmailActive = true;

                person.RecordTypeValueId = _personRecordTypeId;
                person.RecordStatusValueId = _personStatusPending;

                person.Email = shelbyPerson.EmailAddress;
                person.EmailPreference = EmailPreference.EmailAllowed;

                person.FirstName = firstName;
                person.LastName = shelbyPerson.LastName;
                switch ( shelbyPerson.Gender )
                {
                    case "M":
                        person.Gender = Gender.Male;
                        break;
                    case "F":
                        person.Gender = Gender.Female;
                        break;
                    default:
                        person.Gender = Gender.Unknown;
                        break;
                }
                person.MaritalStatusValueId = FindMatchingMaritalStatus( shelbyPerson.MaritalStatus );
                person.ForeignKey = namecounter;

                RockContext rockContext = (RockContext) personService.Context;
                Rock.Model.Group familyGroup = PersonService.SaveNewPerson( person, rockContext );
                rockContext.ChangeTracker.DetectChanges();
                rockContext.SaveChanges( disablePrePostProcessing: true );
                personAliasId = person.PrimaryAliasId;

                if ( familyGroup != null )
                {
                    familyGroup.CampusId = cpCampus.SelectedCampusId;
                    GroupService.AddNewGroupAddress(
                        rockContext,
                        familyGroup,
                        GetAttributeValue( "AddressType" ),
                        shelbyPerson.Address1, shelbyPerson.Address2, shelbyPerson.City, shelbyPerson.State, shelbyPerson.PostalCode, "US",
                        true );
                }
            }

            return personAliasId;
        }

        private int FindMatchingMaritalStatus( string theValue )
        {
            var theDefinedValue = _maritalStatusDefinedType.DefinedValues.FirstOrDefault( a => a.Value.StartsWith( theValue, StringComparison.CurrentCultureIgnoreCase ) );
            // use the unknown value if we didn't find a match.
            if ( theDefinedValue == null )
            {
                theDefinedValue = _maritalStatusDefinedType.DefinedValues.FirstOrDefault( a => String.Equals( a.Value, "Unknown", StringComparison.CurrentCultureIgnoreCase ) );
            }

            return theDefinedValue.Id;
        }

        /// <summary>
        /// Connects to the remote Shelby db and generates a list of unique Funds (Id/Name) and binds
        /// it to the AccountMap repeater.  That is then used to map Shelby Fund Ids to Rock Accounts.
        /// </summary>
        private void VerifyOrSetAccountMappings()
        {
            var list = new ListItemCollection();
            try
            {
                var connectionString = GetConnectionString();
                using ( SqlConnection connection = new SqlConnection( connectionString ) )
                {
                    connection.Open();
                    SqlCommand command = new SqlCommand();
                    command.Connection = connection;
                    command.CommandText = "SELECT [Counter], [Descr] FROM [ShelbyDB].[Shelby].[CNPur] P WHERE P.[Counter] IN (SELECT DISTINCT D.[PurCounter] FROM [ShelbyDB].[Shelby].[CNHstDet] D) ORDER BY [Counter]";

                    SqlDataReader reader = command.ExecuteReader();
                    if ( reader.HasRows )
                    {
                        while ( reader.Read() )
                        {
                            list.Add( new ListItem( reader["Descr"].ToStringSafe(), reader["Counter"].ToStringSafe() ) );
                        }
                        rptAccountMap.DataSource = list;
                        rptAccountMap.DataBind();
                    }
                    else
                    {
                    }
                    reader.Close();
                }
            }
            catch ( Exception ex )
            {
                nbMessage.Text = string.Format( "Your database block settings are not valid or the remote database server is offline or mis-configured. {0}", ex.StackTrace ) ;
            }
        }

        /// <summary>
        /// Gets the connection string.
        /// </summary>
        /// <returns></returns>
        private string GetConnectionString()
        {
            var pass = Encryption.DecryptString( GetAttributeValue( "ShelbyDBPassword" ) );
            return string.Format( @"Data Source=ACC02\Shelby;Initial Catalog=ShelbyDB; User Id=RockConversion; password={0};", pass );
        }

        /// <summary>
        /// Processes the gift.
        /// </summary>
        /// <param name="elemGift">The elem gift.</param>
        /// <param name="tenderMappingDictionary">The tender type mapping dictionary.</param>
        /// <param name="rockContext">The rock context.</param>
        /// <exception cref="System.Exception">
        /// </exception>
        //private void ProcessGift( XElement elemGift, Dictionary<String, String> tenderMappingDictionary, Dictionary<String, String> sourceMappingDictionary, Dictionary<int, int> fundCodeMappingDictionary, RockContext rockContext )
        //{
        //    FinancialAccountService financialAccountService = new FinancialAccountService( rockContext );
        //    DefinedValueService definedValueService = new DefinedValueService( rockContext );
        //    PersonAliasService personAliasService = new PersonAliasService( rockContext );
        //    PersonService personService = new PersonService( rockContext );

        //    // ie, "Contribution"
        //    var transactionType = DefinedValueCache.Read( GetAttributeValue( "TransactionType" ).AsGuid() );
        //    var defaultTransactionSource = DefinedValueCache.Read( GetAttributeValue( "DefaultTransactionSource" ).AsGuid() );
        //    var tenderDefinedType = DefinedTypeCache.Read( Rock.SystemGuid.DefinedType.FINANCIAL_CURRENCY_TYPE.AsGuid() );
        //    var sourceTypeDefinedType = DefinedTypeCache.Read( Rock.SystemGuid.DefinedType.FINANCIAL_SOURCE_TYPE.AsGuid() );

        //    try
        //    {
        //        FinancialTransaction financialTransaction = new FinancialTransaction()
        //        {
        //            TransactionTypeValueId = transactionType.Id,
        //            SourceTypeValueId = defaultTransactionSource.Id
        //        };

        //        if ( elemGift.Element( "ReceivedDate" ) != null )
        //        {
        //            financialTransaction.ProcessedDateTime = Rock.RockDateTime.Now;
        //            financialTransaction.TransactionDateTime = elemGift.Element( "ReceivedDate" ).Value.AsDateTime();
        //        }


        //        if ( elemGift.Element( "TransactionID" ) != null )
        //        {
        //            financialTransaction.TransactionCode = elemGift.Element( "TransactionID" ).Value.ToString();
        //        }


        //        string summary = string.Format( "{0} donated {1} on {2}",
        //            elemGift.Element( "ContributorName" ).IsEmpty ? "Anonymous" : elemGift.Element( "ContributorName" ).Value,
        //            elemGift.Element( "Amount" ).Value.AsDecimal().ToString( "C" )
        //            , financialTransaction.TransactionDateTime.ToString() );
        //        financialTransaction.Summary = summary;

        //        FinancialAccount account = new FinancialAccount();

        //        if ( elemGift.Element( "FundCode" ) != null )
        //        {
        //            int accountId = elemGift.Element( "FundCode" ).Value.AsInteger();

        //            // Convert to mapped value if one exists...
        //            if ( fundCodeMappingDictionary.ContainsKey( accountId ) )
        //            {
        //                accountId = fundCodeMappingDictionary[accountId];
        //            }

        //            // look in cache to see if we already fetched it
        //            if ( !_financialAccountCache.ContainsKey( accountId ) )
        //            {
        //                account = financialAccountService.Queryable()
        //                .Where( fa => fa.Id == accountId )
        //                .FirstOrDefault();
        //                if ( account != null )
        //                {
        //                    _financialAccountCache.Add( accountId, account );
        //                }
        //                else
        //                {
        //                    throw new Exception( "Fund Code (Rock Account) not found." );
        //                }
        //            }
        //            account = _financialAccountCache[accountId];
        //        }

        //        FinancialTransactionDetail financialTransactionDetail = new FinancialTransactionDetail()
        //        {
        //            AccountId = account.Id
        //        };

        //        if ( elemGift.Element( "Amount" ) != null )
        //        {
        //            financialTransactionDetail.Amount = elemGift.Element( "Amount" ).Value.AsDecimal();
        //        }

        //        if ( elemGift.Element( "ReferenceNumber" ) != null )
        //        {
        //            if ( !GetAttributeValue( "UseNegativeForeignKeys" ).AsBoolean() )
        //            {
        //                financialTransactionDetail.Summary = elemGift.Element( "ReferenceNumber" ).Value.ToString();
        //            }
        //            else
        //            {
        //                financialTransactionDetail.Summary = ( elemGift.Element( "ReferenceNumber" ).Value.AsInteger() * -1 ).ToString();
        //            }
        //        }

        //        financialTransaction.TransactionDetails.Add( financialTransactionDetail );
        //        _financialBatch.Transactions.Add( financialTransaction );
        //    }
        //    catch ( Exception ex )
        //    {
        //        _errors.Add( elemGift.Element( "ReferenceNumber" ).Value.ToString() );
        //        elemGift.Add( new XElement( "Error", ex.Message ) );
        //        _errorElements.Add( elemGift );
        //        return;
        //    }
        //}

        /// <summary>
        /// Binds the campus picker.
        /// </summary>
        private void BindCampusPicker()
        {
            // load campus dropdown
            var campuses = CampusCache.All();
            cpCampus.Campuses = campuses;
            cpCampus.Visible = campuses.Count > 1;
        }

        /// <summary>
        /// Binds the grid.
        /// </summary>
        private void BindGrid()
        {
            RockContext rockContext = new RockContext();
            FinancialTransactionDetailService financialTransactionDetailService = new FinancialTransactionDetailService( rockContext );

            var qry = financialTransactionDetailService.Queryable()
                                    .Where( ftd => ftd.Transaction.ForeignKey != null  )
                                    .ToList();
            gContributions.DataSource = qry;
            gContributions.DataBind();

            gContributions.Actions.ShowExcelExport = false;
            pnlGrid.Visible = gContributions.Rows.Count > 0;
        }

        /// <summary>
        /// Binds the error grid.
        /// </summary>
        private void BindErrorGrid()
        {
            RockContext rockContext = new RockContext();
            FinancialTransactionDetailService financialTransactionDetailService = new FinancialTransactionDetailService( rockContext );

            if ( _errorElements.Count > 0 )
            {
                gErrors.DataSource = _errorElements;
            }

            gErrors.DataBind();

            if ( gErrors.Rows.Count > 0 )
            {
                pnlErrors.Visible = true;
                gErrors.Visible = true;
            }
        }

        private void NotifyClientProcessingUsers( int count, int total )
        {
            //double percent = ( double ) count / total * 100;
            //var x = string.Format( @"Processing {2} People...
            //    <div class='progress'>
            //      <div class='progress-bar' role='progressbar' aria-valuenow='{0:0}' aria-valuemin='0' aria-valuemax='100' style='width: {0:0}%;'>{1}</div>
            //    </div>", percent,  count, total );
            //_hubContext.Clients.All.receiveNotification( "shelbyImport-processingUsers", x );
            NotifyClientProcessing( "People", "shelbyImport-processingUsers", string.Empty, count, total );
        }

        private void NotifyClientProcessingBatches( int count, int total )
        {
            //double percent = ( double ) count / total * 100;
            //var x = string.Format( @"Processing {2} Batches...
            //    <div class='progress'>
            //      <div class='progress-bar progress-bar-info' role='progressbar' aria-valuenow='{0:0}' aria-valuemin='0' aria-valuemax='100' style='width: {0:0}%;'>{1}</div>
            //    </div>", percent, count, total );
            //_hubContext.Clients.All.receiveNotification( "shelbyImport-processingBatches", x );
            NotifyClientProcessing( "Batches", "shelbyImport-processingBatches", "progress-bar-info", count, total );

        }

        private void NotifyClientProcessingTransactions( int count, int total )
        {
            //double percent = ( double ) count / total * 100;
            //var x = string.Format( @"Processing {2} Transactions...
            //    <div class='progress'>
            //      <div class='progress-bar progress-bar-success' role='progressbar' aria-valuenow='{0:0}' aria-valuemin='0' aria-valuemax='100' style='width: {0:0}%;'>{1}</div>
            //    </div>", percent, count, total );
            //_hubContext.Clients.All.receiveNotification( "shelbyImport-processingTransactions", x );
            NotifyClientProcessing( "Transactions", "shelbyImport-processingTransactions", "progress-bar-success", count, total );
        }

        private void NotifyClientProcessing( string itemTitle, string htmlId, string progressBarclass, int count, int total )
        {
            double percent = ( double ) count / total * 100;
            var x = string.Format( @"Processing {2} {3}...
                <div class='progress'>
                  <div class='progress-bar {4}' role='progressbar' aria-valuenow='{0:0}' aria-valuemin='0' aria-valuemax='100' style='width: {0:0}%;'>{1}</div>
                </div>", percent, count, total, itemTitle, progressBarclass );
            _hubContext.Clients.All.receiveNotification( htmlId, x );
        }
        #endregion

        /// <summary>
        /// Handles the ItemDataBound event of the rptAccountMap control which creates a DropDownList 
        /// with the Rock Account as the selected value which has been matched to the corresponding
        /// Shelby Fund.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RepeaterItemEventArgs"/> instance containing the event data.</param>
        protected void rptAccountMap_ItemDataBound( object sender, RepeaterItemEventArgs e )
        {
            if ( e.Item.ItemType == ListItemType.Item || e.Item.ItemType == ListItemType.AlternatingItem )
            {
                var item = ( ListItem ) e.Item.DataItem;

                Literal litFundName = ( Literal ) e.Item.FindControl( "litFundName" );
                HiddenField hfFundId = ( HiddenField ) e.Item.FindControl( "hfFundId" );

                litFundName.Text = item.Text;
                hfFundId.Value = item.Value;
                string accountId = string.Empty;

                RockDropDownList list = ( RockDropDownList ) e.Item.FindControl( "rdpAcccounts" );
                if ( list != null )
                {
                    if ( _fundAccountMappingDictionary.ContainsKey( hfFundId.Value ) )
                    {
                        accountId = _fundAccountMappingDictionary[hfFundId.Value];
                        // Make sure it's still in the list before we try to select it.
                        if ( AllAccounts.ContainsKey( accountId.AsInteger() ) )
                        {
                            list.SelectedValue = accountId;
                        }
                    }
                    else
                    {
                        list.SelectedIndex = -1;
                    }

                    list.DataSource = AllAccounts;
                    list.DataValueField = "Key";
                    list.DataTextField = "Value";
                    list.DataBind();
                }
            }
        }

        protected void rdpAcccounts_SelectedIndexChanged( object sender, EventArgs e )
        {
            // Get the selected Rock Account Id
            RockDropDownList rdpAcccounts = ( RockDropDownList ) sender;

            var clientId = rdpAcccounts.ClientID;
            int controlIndex = GetControlIndex( clientId );
            int mapIndex = controlIndex - 1;

            // Get the Shelby Fund name 
            Literal litFundName = ( Literal ) rptAccountMap.Items[mapIndex].FindControl( "litFundName" );
            
            // Get the Shelby Fund Id
            HiddenField hfFundId = ( HiddenField ) rptAccountMap.Items[mapIndex].FindControl( "hfFundId" );

            // Save the value in the Dictonary and save it to the block's attribute
            
            // Add/Update the new value in the dictionary
            _fundAccountMappingDictionary.AddOrReplace( hfFundId.Value, rdpAcccounts.SelectedValue );

            // Turn the Dictionary back to a string and store it in the block's attribute value
            var newValue = String.Join( ",", _fundAccountMappingDictionary.Select( kvp =>String.Format( "{0}={1}", kvp.Key, kvp.Value ) ) );
            SetAttributeValue( FUND_ACCOUNT_MAPPINGS, newValue );
            SaveAttributeValues();

            // Update the onscreen status to show the user the value has been saved.
            Literal litAccontSaveStatus = ( Literal ) rptAccountMap.Items[mapIndex].FindControl( "litAccontSaveStatus" );
            litAccontSaveStatus.Text = string.Format( "<span class='text-success'><i class='fa fa-check'></i> saved</span>", litFundName.Text );
        }

        private int GetControlIndex( String controlID )
        {
            Regex regex = new Regex( "([0-9]+)", RegexOptions.RightToLeft );
            Match match = regex.Match( controlID );

            return Convert.ToInt32( match.Value );
        }

        protected void tbBatchName_TextChanged( object sender, EventArgs e )
        {
            RockTextBox tbBatchName = ( RockTextBox ) sender;
            SetAttributeValue( "BatchName", tbBatchName.Text );
            SaveAttributeValues();
        }

        #region Helper Classes & Methods
        class ShelbyPerson
        {
            public int NameCounter;
            public string FirstMiddle;
            public string Salutation;
            public string LastName;
            public string Gender;
            public string MaritalStatus;
            public string Address1;
            public string Address2;
            public string City;
            public string State;
            public string PostalCode;
            public string EmailAddress;

            public ShelbyPerson( SqlDataReader reader )
            {
                NameCounter = ( int ) reader["NameCounter"];

                EmailAddress = reader["EMailAddress"].ToStringSafe();
                if ( ! EmailAddress.IsValidEmail() )
                {
                    EmailAddress = string.Empty;
                }

                Gender = reader["Gender"].ToStringSafe();
                Salutation = reader["Salutation"].ToStringSafe();
                FirstMiddle = reader["FirstMiddle"].ToStringSafe();
                LastName = reader["LastName"].ToStringSafe();
                MaritalStatus = reader["MaritalStatus"].ToStringSafe();
                Address1 =   reader["Adr1"].ToStringSafe();
                Address2 =  reader["Adr2"].ToStringSafe();
                City = reader["City"].ToStringSafe();
                State = reader["State"].ToStringSafe();
                PostalCode = reader["PostalCode"].ToStringSafe();
            }
        }


        /// <summary>
        /// Class that represents a Shelby Batch ([BatchNu], [NuContr], [Total], [WhenPosted], [WhenSetup], [WhoSetup] )
        /// </summary>
        class ShelbyBatch
        {
            public int BatchNu;
            public int NuContr;
            public Decimal Total;
            public DateTime WhenPosted;
            public DateTime WhenSetup;
            public string WhoSetup;
            public int RockBatchId;

            public ShelbyBatch( SqlDataReader reader )
            {
                BatchNu = ( int ) reader["BatchNu"];
                NuContr = ( Int16 ) reader["NuContr"];
                Total = ( Decimal ) reader["Total"];
                WhenSetup = ( DateTime ) reader["WhenSetup"];
                WhenPosted = ( DateTime ) reader["WhenPosted"];
                WhoSetup = reader["WhoSetup"].ToStringSafe();
            }

        }

        /// <summary>
        /// Represents a Shelby Contribution History record
        /// 	H.[Counter], D.[Counter] as 'DetailCounter', H.[Amount], H.[BatchNu], H.[CheckNu], H.[CNDate], H.[Memo], H.[NameCounter], 
        /// 	H.[WhenSetup], H.[WhenUpdated], H.[WhoSetup], H.[WhoUpdated], H.[CheckType], D.[PurCounter],
        /// 	P.[Descr], D.[Amount] as 'PurAmount'
        /// </summary>
        class ShelbyContribution
        {
            public int Counter;
            public int DetailCounter;
            public Decimal Amount;
            public int BatchNu;
            public string CheckNu;
            public DateTime CNDate;
            public string Memo;
            public int NameCounter;
            public DateTime WhenSetup;
            public DateTime WhenUpdated;
            public string WhoSetup;
            public string WhoUpdated;
            public string CheckType;
            public int PurCounter;
            public string Descr;
            public Decimal PurAmount;

            public ShelbyContribution( SqlDataReader reader )
            {
                Counter = ( int ) reader["Counter"];
                DetailCounter = ( int ) reader["DetailCounter"];
                Amount = ( Decimal ) reader["Amount"];
                BatchNu = ( int ) reader["BatchNu"];
                CheckNu = (( string ) reader["CheckNu"]).ToLower();
                CNDate = ( DateTime ) reader["CNDate"];
                Memo = ( string ) reader["Memo"];
                NameCounter = ( int ) reader["NameCounter"];
                WhenSetup = ( DateTime ) reader["WhenSetup"];
                WhenUpdated = ( DateTime ) reader["WhenUpdated"];
                WhoSetup = reader["WhoSetup"].ToStringSafe();
                WhoUpdated = reader["WhoUpdated"].ToStringSafe();
                CheckType = reader["CheckType"].ToStringSafe();
                PurCounter = ( int ) reader["PurCounter"];
                WhoUpdated = reader["WhoUpdated"].ToStringSafe();
                PurAmount = ( Decimal ) reader["PurAmount"];
            }
        }
        #endregion
    }
}