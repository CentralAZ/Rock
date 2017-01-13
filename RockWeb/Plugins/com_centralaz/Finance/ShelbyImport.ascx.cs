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
    ///     - the Shelby NameCounter is added to a dictionary map (NameCounter -> Person.Id)
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
     * SELECT
	H.Counter 
	,H.[Amount]
      ,[BatchNu]
      ,[CheckNu]
      ,H.[CNDate]
      ,H.[Counter]
      ,H.[Memo]
      ,H.[NameCounter]
      ,[NonCash]
      ,[NonTax]
      ,[Posted]
      ,H.[WhenSetup]
      ,H.[WhenUpdated]
      ,H.[WhoSetup]
      ,H.[WhoUpdated]
      ,H.[CheckType]
      ,D.[PurCounter]
	  ,P.[Descr]
	  ,D.[Amount]
  FROM [ShelbyDB].[Shelby].[CNHst] H
  INNER JOIN [ShelbyDB].[Shelby].[CNHstDet] D ON D.HstCounter = H.Counter
  INNER JOIN [ShelbyDB].[Shelby].[CNPur] P ON P.Counter = D.PurCounter
  WHERE H.[BatchNu] = 48
     * */
    [DisplayName( "Shelby Import" )]
    [Category( "com_centralaz > Finance" )]
    [Description( "Finance block to import contribution data from a Shelby database. It will add new people if a match cannot be made, import the batches, and financial transactions." )]

    [TextField( "Batch Name", "The name that should be used for the batches created", true, "Shelby Import", order: 0 )]
    [IntegerField( "Anonymous Giver PersonAliasID", "PersonAliasId to use in case of anonymous giver", true, order: 1 )]
    [DefinedValueField( Rock.SystemGuid.DefinedType.FINANCIAL_TRANSACTION_TYPE, "TransactionType", "The means the transaction was submitted by", true, order: 2 )]
    [DefinedValueField( Rock.SystemGuid.DefinedType.FINANCIAL_SOURCE_TYPE, "Default Transaction Source", "The default transaction source to use if a match is not found (Website, Kiosk, etc.).", true, order:3 )]
    [DefinedValueField( Rock.SystemGuid.DefinedType.FINANCIAL_CURRENCY_TYPE, "Default Tender Type Value", "The default tender type if a match is not found (Cash, Credit Card, etc.).", true, order: 4 )]
    [BooleanField( "Use Negative Foreign Keys", "Indicates whether Rock uses the negative of the Shelby reference ID for the contribution record's foreign key", false, order: 5 )]
    [TextField( "Source Mappings", "Held in Shelby's ContributionSource field, these correspond to Rock's TransactionSource (DefinedType). If you don't want to rename your current transaction source types, just map them here. Delimit them with commas or semicolons, and write them in the format 'Shelby_value=Rock_value'.", false, "", "Data Mapping", 1 )]
    [TextField( "Tender Mappings", "Held in the Shelby's ContributionType field, these correspond to Rock's TenderTypes (DefinedType). If you don't want to clutter your tender types, just map them here. Delimit them with commas or semicolons, and write them in the format 'Shelby_value=Rock_value'.", false, "", "Data Mapping", 2 )]
    [TextField( "Fund Code Mapping", "Held in the Shelby's FundCode field, these correspond to Rock's Account IDs (integer). Each FundCode should be mapped to a matching AccountId otherwise Rock will just use the same value. Delimit them with commas or semicolons, and write them in the format 'Shelby_value=Rock_value'.", false, "", "Data Mapping", 3 )]    
    [LinkedPage( "Batch Detail Page", "The page used to display the contributions for a specific batch", true, "", "Linked Pages", 0 )]
    [LinkedPage( "Contribution Detail Page", "The page used to display the contribution transaction details", true, "",  "Linked Pages", 1 )]
    [EncryptedTextField("Shelby DB Password", "", true, "", "Remote Shelby DB", 0 )]

    public partial class ShelbyImport : Rock.Web.UI.RockBlock
    {
        #region Fields

        private int _anonymousPersonAliasId = 0;
        private FinancialBatch _financialBatch;
        private List<string> _errors = new List<string>();
        private List<XElement> _errorElements = new List<XElement>();

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
        }

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Load" /> event.
        /// </summary>
        /// <param name="e">The <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );

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

        private void VerifyOrSetAccountMappings()
        {
            var list = new ListItemCollection();
            try
            {
                var pass = Encryption.DecryptString( GetAttributeValue( "ShelbyDBPassword" ) );
                var connectionString = string.Format( @"Data Source=ACC02\Shelby;Initial Catalog=ShelbyDB; User Id=RockConversion; password={0};", pass );
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
            catch
            {
                nbMessage.Text = "Your database block settings are not valid or the remote database server is offline or mis-configured.";
            }
        }

        /// <summary>
        /// Processes the gift.
        /// </summary>
        /// <param name="elemGift">The elem gift.</param>
        /// <param name="tenderMappingDictionary">The tender type mapping dictionary.</param>
        /// <param name="rockContext">The rock context.</param>
        /// <exception cref="System.Exception">
        /// </exception>
        private void ProcessGift( XElement elemGift, Dictionary<String, String> tenderMappingDictionary, Dictionary<String, String> sourceMappingDictionary, Dictionary<int, int> fundCodeMappingDictionary, RockContext rockContext )
        {
            FinancialAccountService financialAccountService = new FinancialAccountService( rockContext );
            DefinedValueService definedValueService = new DefinedValueService( rockContext );
            PersonAliasService personAliasService = new PersonAliasService( rockContext );
            PersonService personService = new PersonService( rockContext );

            // ie, "Contribution"
            var transactionType = DefinedValueCache.Read( GetAttributeValue( "TransactionType" ).AsGuid() );
            var defaultTransactionSource = DefinedValueCache.Read( GetAttributeValue( "DefaultTransactionSource" ).AsGuid() );
            var tenderDefinedType = DefinedTypeCache.Read( Rock.SystemGuid.DefinedType.FINANCIAL_CURRENCY_TYPE.AsGuid() );
            var sourceTypeDefinedType = DefinedTypeCache.Read( Rock.SystemGuid.DefinedType.FINANCIAL_SOURCE_TYPE.AsGuid() );

            try
            {
                FinancialTransaction financialTransaction = new FinancialTransaction()
                {
                    TransactionTypeValueId = transactionType.Id,
                    SourceTypeValueId = defaultTransactionSource.Id
                };

                if ( elemGift.Element( "ReceivedDate" ) != null )
                {
                    financialTransaction.ProcessedDateTime = Rock.RockDateTime.Now;
                    financialTransaction.TransactionDateTime = elemGift.Element( "ReceivedDate" ).Value.AsDateTime();
                }

                // Map the Contribution Source to a Rock TransactionSource
                if ( elemGift.Element( "ContributionSource" ) != null )
                {
                    string transactionSourceElemValue = elemGift.Element( "ContributionSource" ).Value.ToString();

                    // Convert to mapped value if one exists...
                    if ( sourceMappingDictionary.ContainsKey( transactionSourceElemValue ) )
                    {
                        transactionSourceElemValue = sourceMappingDictionary[transactionSourceElemValue];
                    }

                    // Now find the matching source type...
                    // Get the source type and put in cache if we've not encountered it before.
                    if ( _transactionSourceTypeDefinedValueCache.ContainsKey( transactionSourceElemValue ) )
                    {
                        var transactionSourceDefinedValue = _transactionSourceTypeDefinedValueCache[transactionSourceElemValue];
                        financialTransaction.SourceTypeValueId = transactionSourceDefinedValue.Id;
                    }
                    else
                    {
                        DefinedValue transactionSourceDefinedValue;
                        int id;
                        transactionSourceDefinedValue = definedValueService.Queryable()
                            .Where( d => d.DefinedTypeId == sourceTypeDefinedType.Id && d.Value == transactionSourceElemValue )
                            .FirstOrDefault();
                        if ( transactionSourceDefinedValue != null )
                        {
                            _transactionSourceTypeDefinedValueCache.Add( transactionSourceElemValue, transactionSourceDefinedValue );
                            id = transactionSourceDefinedValue.Id;
                            financialTransaction.SourceTypeValueId = transactionSourceDefinedValue.Id;
                        }
                    }
                }

                // Map the Contribution Type to a Rock TenderType
                if ( elemGift.Element( "ContributionType" ) != null )
                {
                    string contributionTypeElemValue = elemGift.Element( "ContributionType" ).Value.ToString();

                    // Convert to mapped value if one exists...
                    if ( tenderMappingDictionary.ContainsKey( contributionTypeElemValue ) )
                    {
                        contributionTypeElemValue = tenderMappingDictionary[contributionTypeElemValue];
                    }

                    // set up the necessary Financial Payment Detail record
                    if ( financialTransaction.FinancialPaymentDetail == null )
                    {
                        financialTransaction.FinancialPaymentDetail = new FinancialPaymentDetail();
                    }

                    // Now find the matching tender type...
                    // Get the tender type and put in cache if we've not encountered it before.
                    if ( _tenderTypeDefinedValueCache.ContainsKey( contributionTypeElemValue ) )
                    {
                        var tenderTypeDefinedValue = _tenderTypeDefinedValueCache[contributionTypeElemValue];
                        financialTransaction.FinancialPaymentDetail.CurrencyTypeValueId = tenderTypeDefinedValue.Id;
                    }
                    else
                    {
                        DefinedValue tenderTypeDefinedValue;
                        int id;
                        tenderTypeDefinedValue = definedValueService.Queryable()
                            .Where( d => d.DefinedTypeId == tenderDefinedType.Id && d.Value == contributionTypeElemValue )
                            .FirstOrDefault();
                        if ( tenderTypeDefinedValue != null )
                        {
                            _tenderTypeDefinedValueCache.Add( contributionTypeElemValue, tenderTypeDefinedValue );
                            id = tenderTypeDefinedValue.Id;
                        }
                        else
                        {
                            // otherwise get and use the tender type default value
                            id = DefinedValueCache.Read( GetAttributeValue( "DefaultTenderTypeValue" ).AsGuid() ).Id;
                        }
                        financialTransaction.FinancialPaymentDetail.CurrencyTypeValueId = id;
                    }
                }

                if ( elemGift.Element( "TransactionID" ) != null )
                {
                    financialTransaction.TransactionCode = elemGift.Element( "TransactionID" ).Value.ToString();
                }

                if ( elemGift.Element( "IndividualID" ) != null && !elemGift.Element( "IndividualID" ).IsEmpty )
                {
                    int aliasId = elemGift.Element( "IndividualID" ).Value.AsInteger();

                    // verify that this is a real person alias by trying to fetch it.
                    var personAlias = personAliasService.GetByAliasId( aliasId );
                    if ( personAlias == null )
                    {
                        throw new Exception( string.Format( "Invalid person alias Id {0}", elemGift.Element( "IndividualID" ).Value ) );
                    }

                    financialTransaction.AuthorizedPersonAliasId = personAlias.Id;
                }
                else
                {
                    financialTransaction.AuthorizedPersonAliasId = _anonymousPersonAliasId;
                }

                string summary = string.Format( "{0} donated {1} on {2}",
                    elemGift.Element( "ContributorName" ).IsEmpty ? "Anonymous" : elemGift.Element( "ContributorName" ).Value,
                    elemGift.Element( "Amount" ).Value.AsDecimal().ToString( "C" )
                    , financialTransaction.TransactionDateTime.ToString() );
                financialTransaction.Summary = summary;

                FinancialAccount account = new FinancialAccount();

                if ( elemGift.Element( "FundCode" ) != null )
                {
                    int accountId = elemGift.Element( "FundCode" ).Value.AsInteger();

                    // Convert to mapped value if one exists...
                    if ( fundCodeMappingDictionary.ContainsKey( accountId ) )
                    {
                        accountId = fundCodeMappingDictionary[accountId];
                    }

                    // look in cache to see if we already fetched it
                    if ( !_financialAccountCache.ContainsKey( accountId ) )
                    {
                        account = financialAccountService.Queryable()
                        .Where( fa => fa.Id == accountId )
                        .FirstOrDefault();
                        if ( account != null )
                        {
                            _financialAccountCache.Add( accountId, account );
                        }
                        else
                        {
                            throw new Exception( "Fund Code (Rock Account) not found." );
                        }
                    }
                    account = _financialAccountCache[accountId];
                }

                FinancialTransactionDetail financialTransactionDetail = new FinancialTransactionDetail()
                {
                    AccountId = account.Id
                };

                if ( elemGift.Element( "Amount" ) != null )
                {
                    financialTransactionDetail.Amount = elemGift.Element( "Amount" ).Value.AsDecimal();
                }

                if ( elemGift.Element( "ReferenceNumber" ) != null )
                {
                    if ( !GetAttributeValue( "UseNegativeForeignKeys" ).AsBoolean() )
                    {
                        financialTransactionDetail.Summary = elemGift.Element( "ReferenceNumber" ).Value.ToString();
                    }
                    else
                    {
                        financialTransactionDetail.Summary = ( elemGift.Element( "ReferenceNumber" ).Value.AsInteger() * -1 ).ToString();
                    }
                }

                financialTransaction.TransactionDetails.Add( financialTransactionDetail );
                _financialBatch.Transactions.Add( financialTransaction );
            }
            catch ( Exception ex )
            {
                _errors.Add( elemGift.Element( "ReferenceNumber" ).Value.ToString() );
                elemGift.Add( new XElement( "Error", ex.Message ) );
                _errorElements.Add( elemGift );
                return;
            }
        }

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

            if ( _financialBatch != null )
            {
                var qry = financialTransactionDetailService.Queryable()
                                        .Where( ftd => ftd.Transaction.BatchId == _financialBatch.Id )
                                       .ToList();
                gContributions.DataSource = qry;
            }
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

        #endregion

        protected void rptAccountMap_ItemDataBound( object sender, RepeaterItemEventArgs e )
        {
            if ( e.Item.ItemType == ListItemType.Item || e.Item.ItemType == ListItemType.AlternatingItem )
            {
                var item = ( ListItem ) e.Item.DataItem;

                Literal litFundName = ( Literal ) e.Item.FindControl( "litFundName" );
                HiddenField hfFundId = ( HiddenField ) e.Item.FindControl( "hfFundId" );

                litFundName.Text = item.Text;
                hfFundId.Value = item.Value;

                RockDropDownList list = ( RockDropDownList ) e.Item.FindControl( "rdpAcccounts" );
                if ( list != null )
                {
                    list.DataSource = AllAccounts;
                    list.DataBind();
                }
            }
        }

        protected void rdpAcccounts_SelectedIndexChanged( object sender, EventArgs e )
        {
            Literal litFundName = ( Literal ) rptAccountMap.Items[GetControlIndex( ( ( ( RockDropDownList ) sender ).ClientID ) )].FindControl( "litFundName" );

            Literal litAccontSaveStatus = ( Literal ) rptAccountMap.Items[GetControlIndex( ( ( ( RockDropDownList ) sender ).ClientID ) )].FindControl( "litAccontSaveStatus" );
            litAccontSaveStatus.Text = string.Format( "<span class='text-success'><i class='fa fa-check'></i> {0} saved.</span>", litFundName.Text );
        }

        private int GetControlIndex( String controlID )
        {
            Regex regex = new Regex( "([0-9.*])", RegexOptions.RightToLeft );
            Match match = regex.Match( controlID );

            return Convert.ToInt32( match.Value );
        }
    }
}