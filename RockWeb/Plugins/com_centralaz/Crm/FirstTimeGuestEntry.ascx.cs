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
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Web.UI;
using System.Web.UI.WebControls;

using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;
using Rock.Web.UI.Controls;
using Rock.Attribute;
using System.Text;

namespace RockWeb.Plugins.com_centralaz.Crm
{
    /// <summary>
    /// A block for rapidly adding new people (First Time Guests) and adding them to a connection 
    /// request which can launch a workflow to perform additional processing.
    /// </summary>
    [DisplayName( "First Time Guest Entry" )]
    [Category( "com_centralaz > CRM" )]
    [Description( "A block for rapidly adding new people (First Time Guests) and adding them to a connection request which can launch a workflow to perform additional processing." )]

    // Connection Request Settings
    [ConnectionOpportunityField( "Connection Opportunity", "The connection opportunity that new requests will be made for.", true, "", false, "Connection Request Settings", 0 )]
    [TextField( "Interests", "A comma-delimited list of different options that can be checked.  These will be added to the Comment field of the connection request.", true, "Baptism, Following Jesus Christ (Discover Christ class), Serving, Discover Central class", "Connection Request Settings", 1 )]
    [TextField( "Entry Source", "A comma-delimited list of places where the data entry can occur. The selected item will be added to the Comment field of the connection request.", true, "Guest Central, Children's HQ", "Connection Request Settings", 2 )]

    // Person Settings
    [DefinedValueField( "2E6540EA-63F0-40FE-BE50-F2A84735E600", "Connection Status", "The connection status to use for new individuals (default: 'Web Prospect'.)", true, false, Rock.SystemGuid.DefinedValue.PERSON_CONNECTION_STATUS_VISITOR, "Person Settings", 2 )]
    [DefinedValueField( "8522BADD-2871-45A5-81DD-C76DA07E2E7E", "Record Status", "The record status to use for new individuals (default: 'Pending'.)", true, false, "283999EC-7346-42E3-B807-BCE9B2BABB49", "Person Settings", 3 )]
    [BooleanField( "Is Sms Checked By Default ", "Is the 'Enable SMS' option checked by default.", true, "Person Settings", 4, "IsSmsChecked" )]

    //Prayer Request Settings
    [BooleanField( "Is Prayer Request Enabled", "Is the Prayer Request text box visible.", true, "Prayer Request Settings", 5 )]
    [CategoryField( "Prayer Category", "The  category to use for all new prayer requests.", false, "Rock.Model.PrayerRequest", "", "", false, "4B2D88F5-6E45-4B4B-8776-11118C8E8269", "Prayer Request Settings", 6, "PrayerCategory" )]

    public partial class FirstTimeGuestEntry : Rock.Web.UI.RockBlock
    {
        #region Fields

        ConnectionOpportunity _connectionOpportunity = null;
        RockContext _rockContext = null;
        DefinedValueCache _dvcConnectionStatus = null;
        DefinedValueCache _dvcRecordStatus = null;
        DefinedValueCache _married = null;
        DefinedValueCache _homeAddressType = null;
        GroupTypeCache _familyType = null;
        GroupTypeRoleCache _adultRole = null;
        bool _isValidSettings = true;
        bool _isPrayerRequestEnabled = false;

        private const string CAMPUS_SETTING = "FirstTimeGuestEntry_SelectedCampus";
        private const string SOURCE_SETTING = "FirstTimeGuestEntry_SelectedSource";

        #endregion

        #region Base Control Methods

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Init" /> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnInit( EventArgs e )
        {
            base.OnInit( e );

            // this event gets fired after block settings are updated. it's nice to repaint the screen if these settings would alter it
            this.BlockUpdated += Block_BlockUpdated;
            this.AddConfigurationUpdateTrigger( upnlContent );
        }

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Load" /> event.
        /// </summary>
        /// <param name="e">The <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );

            if ( !CheckSettings() )
            {
                _isValidSettings = false;
                nbNotice.Visible = true;
                pnlView.Visible = false;
            }
            else
            {
                nbNotice.Visible = false;
                pnlView.Visible = true;

                if ( !Page.IsPostBack )
                {
                    ShowDetail();
                }
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// Handles the BlockUpdated event of the control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void Block_BlockUpdated( object sender, EventArgs e )
        {
            ShowDetail();
        }

        /// <summary>
        /// Handles the Click event of the btnCancel control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void btnCancel_Click( object sender, EventArgs e )
        {
            NavigateToParentPage();
        }

        /// <summary>
        /// Handles the Click event of the btnSave control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void btnSave_Click( object sender, EventArgs e )
        {
            if ( Page.IsValid & _isValidSettings )
            {
                var rockContext = new RockContext();
                var personService = new PersonService( rockContext );

                Person person = null;
                Person spouse = null;
                Group family = null;
                GroupLocation homeLocation = null;
                bool isMatch = false;

                var changes = new History.HistoryChangeList();
                var spouseChanges = new History.HistoryChangeList();
                var familyChanges = new History.HistoryChangeList();

                var addedPeopleNames = string.Empty;

                // First try to grab the person from the picker
                if ( ppGuest.PersonId != null )
                {
                    person = new PersonService( rockContext ).Get( ppGuest.PersonId.Value );
                }

                if ( pnlNewPerson.Enabled )
                {
                    if ( person == null )
                    {
                        // Try to find person by name/email 
                        var matches = personService.FindPersons( tbFirstName.Text.Trim(), tbLastName.Text.Trim(), tbEmail.Text.Trim() );
                        if ( matches.Count() == 1 )
                        {
                            person = matches.First();
                            isMatch = true;
                        }
                    }

                    // Check to see if this is a new person
                    if ( person == null )
                    {
                        // If so, create the person and family record for the new person
                        person = new Person();
                        person.FirstName = tbFirstName.Text.Trim();
                        person.LastName = tbLastName.Text.Trim();
                        person.Email = tbEmail.Text.Trim();
                        person.IsEmailActive = true;
                        person.EmailPreference = EmailPreference.EmailAllowed;
                        person.RecordTypeValueId = DefinedValueCache.Get( Rock.SystemGuid.DefinedValue.PERSON_RECORD_TYPE_PERSON.AsGuid() ).Id;
                        person.ConnectionStatusValueId = _dvcConnectionStatus.Id;
                        person.RecordStatusValueId = _dvcRecordStatus.Id;
                        person.Gender = Gender.Unknown;

                        family = PersonService.SaveNewPerson( person, rockContext, cpCampus.SelectedCampusId, false );
                    }
                }

                if ( person != null )
                {
                    addedPeopleNames = person.FullName;

                    History.EvaluateChange( changes, "Connection Status", person.ConnectionStatusValueId, _dvcConnectionStatus.Id );
                    person.ConnectionStatusValueId = _dvcConnectionStatus.Id;

                    // Get the current person's families
                    var families = person.GetFamilies( rockContext );

                    // If address can being entered, look for first family with a home location
                    foreach ( var aFamily in families )
                    {
                        homeLocation = aFamily.GroupLocations
                            .Where( l =>
                                l.GroupLocationTypeValueId == _homeAddressType.Id &&
                                l.IsMappedLocation )
                            .FirstOrDefault();
                        if ( homeLocation != null )
                        {
                            family = aFamily;
                            break;
                        }
                    }

                    // If a family wasn't found with a home location, use the person's first family
                    if ( family == null )
                    {
                        family = families.FirstOrDefault();
                    }

                    History.EvaluateChange( changes, "Campus", family.CampusId, cpCampus.SelectedCampusId );
                    family.CampusId = cpCampus.SelectedCampusId;

                    if ( pnlNewPerson.Enabled )
                    {
                        // Save the contact info
                        History.EvaluateChange( changes, "Email", person.Email, tbEmail.Text );
                        person.Email = tbEmail.Text;

                        if ( !isMatch || !string.IsNullOrWhiteSpace( pnHome.Number ) )
                        {
                            SetPhoneNumber( rockContext, person, pnHome, null, Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_HOME.AsGuid(), changes );
                        }
                        if ( !isMatch || !string.IsNullOrWhiteSpace( pnHome.Number ) )
                        {
                            SetPhoneNumber( rockContext, person, pnCell, cbSms, Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_MOBILE.AsGuid(), changes );
                        }

                        if ( !isMatch || !string.IsNullOrWhiteSpace( acAddress.Street1 ) )
                        {
                            string oldLocation = homeLocation != null ? homeLocation.Location.ToString() : string.Empty;
                            string newLocation = string.Empty;

                            var location = new LocationService( rockContext ).Get( acAddress.Street1, acAddress.Street2, acAddress.City, acAddress.State, acAddress.PostalCode, acAddress.Country );
                            if ( location != null )
                            {
                                if ( homeLocation == null )
                                {
                                    homeLocation = new GroupLocation();
                                    homeLocation.GroupLocationTypeValueId = _homeAddressType.Id;
                                    family.GroupLocations.Add( homeLocation );
                                }
                                else
                                {
                                    oldLocation = homeLocation.Location.ToString();
                                }

                                homeLocation.Location = location;
                                newLocation = location.ToString();
                            }
                            else
                            {
                                if ( homeLocation != null )
                                {
                                    homeLocation.Location = null;
                                    family.GroupLocations.Remove( homeLocation );
                                    new GroupLocationService( rockContext ).Delete( homeLocation );
                                }
                            }

                            History.EvaluateChange( familyChanges, "Home Location", oldLocation, newLocation );
                        }

                        // Check for the spouse
                        if ( !string.IsNullOrWhiteSpace( tbSpouseFirstName.Text ) )
                        {
                            spouse = person.GetSpouse( rockContext );
                            bool isSpouseMatch = true;

                            if ( spouse == null ||
                                !tbSpouseFirstName.Text.Trim().Equals( spouse.FirstName.Trim(), StringComparison.OrdinalIgnoreCase ) ||
                                !tbSpouseLastName.Text.Trim().Equals( spouse.LastName.Trim(), StringComparison.OrdinalIgnoreCase ) )
                            {
                                spouse = new Person();
                                isSpouseMatch = false;

                                spouse.FirstName = tbSpouseFirstName.Text.FixCase();
                                History.EvaluateChange( spouseChanges, "First Name", string.Empty, spouse.FirstName );

                                spouse.LastName = tbSpouseLastName.Text.FixCase();
                                if ( spouse.LastName.IsNullOrWhiteSpace() )
                                {
                                    spouse.LastName = person.LastName;
                                }
                                History.EvaluateChange( spouseChanges, "Last Name", string.Empty, spouse.LastName );

                                spouse.ConnectionStatusValueId = _dvcConnectionStatus.Id;
                                spouse.RecordStatusValueId = _dvcRecordStatus.Id;
                                spouse.Gender = Gender.Unknown;

                                spouse.IsEmailActive = true;
                                spouse.EmailPreference = EmailPreference.EmailAllowed;

                                var groupMember = new GroupMember();
                                groupMember.GroupRoleId = _adultRole.Id;
                                groupMember.Person = spouse;

                                family.Members.Add( groupMember );

                                spouse.MaritalStatusValueId = _married.Id;
                                person.MaritalStatusValueId = _married.Id;
                            }

                            History.EvaluateChange( changes, "Email", person.Email, tbEmail.Text );
                            spouse.Email = tbSpouseEmail.Text;

                            History.EvaluateChange( changes, "Connection Status", spouse.ConnectionStatusValueId, _dvcConnectionStatus.Id );
                            spouse.ConnectionStatusValueId = _dvcConnectionStatus.Id;

                            if ( !isSpouseMatch || !string.IsNullOrWhiteSpace( pnHome.Number ) )
                            {
                                SetPhoneNumber( rockContext, spouse, pnHome, null, Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_HOME.AsGuid(), spouseChanges );
                            }

                            if ( !isSpouseMatch || !string.IsNullOrWhiteSpace( pnSpouseCell.Number ) )
                            {
                                SetPhoneNumber( rockContext, spouse, pnSpouseCell, cbSpouseSms, Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_MOBILE.AsGuid(), spouseChanges );
                            }
                        }
                    }
                }

                // Save the person/spouse and change history 
                rockContext.SaveChanges();
                HistoryService.SaveChanges( rockContext, typeof( Person ),
                    Rock.SystemGuid.Category.HISTORY_PERSON_DEMOGRAPHIC_CHANGES.AsGuid(), person.Id, changes );
                HistoryService.SaveChanges( rockContext, typeof( Person ),
                    Rock.SystemGuid.Category.HISTORY_PERSON_FAMILY_CHANGES.AsGuid(), person.Id, familyChanges );
                if ( spouse != null )
                {
                    addedPeopleNames += " and " + spouse.FullName;
                    HistoryService.SaveChanges( rockContext, typeof( Person ),
                        Rock.SystemGuid.Category.HISTORY_PERSON_DEMOGRAPHIC_CHANGES.AsGuid(), spouse.Id, spouseChanges );
                    HistoryService.SaveChanges( rockContext, typeof( Person ),
                        Rock.SystemGuid.Category.HISTORY_PERSON_FAMILY_CHANGES.AsGuid(), spouse.Id, familyChanges );
                }

                // Save the Connection Requests
                CreateConnectionRequest( rockContext, person );
                CreateConnectionRequest( rockContext, spouse );

                // Save the Prayer Request
                if ( _isPrayerRequestEnabled )
                {
                    CreatePrayerRequest( rockContext, person );
                }

                // Reload page
                nbMessage.Text = string.Format( "New entry for {0} saved.", addedPeopleNames );
                nbMessage.Visible = true;
                hfShowSuccess.Value = "true";
                ClearControls();
                ShowDetail();
            }
        }

        /// <summary>
        /// Clears the controls.
        /// </summary>
        private void ClearControls()
        {
            ppGuest.PersonId = null;
            ppGuest.SetValue( null );
            tbFirstName.Text = tbLastName.Text = pnCell.Text = tbEmail.Text = pnHome.Text = string.Empty;
            tbSpouseFirstName.Text = tbSpouseLastName.Text = pnSpouseCell.Text = tbSpouseEmail.Text = string.Empty;
            tbComments.Text = tbPrayerRequests.Text = string.Empty;
            acAddress.Street1 = acAddress.Street2 = acAddress.City = acAddress.PostalCode = string.Empty;
            pnlNewPerson.Enabled = tbFirstName.Required = tbLastName.Required = true;
        }

        /// <summary>
        /// Handles the SelectPerson event of the ppGuest control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void ppGuest_SelectPerson( object sender, EventArgs e )
        {
            if ( ppGuest.PersonId.HasValue )
            {
                pnlNewPerson.Enabled = tbFirstName.Required = tbLastName.Required = tbEmail.Required = false;
            }
            else
            {
                pnlNewPerson.Enabled = tbFirstName.Required = tbLastName.Required = true;
            }
        }

        /// <summary>
        /// Handles the SelectedIndexChanged event of the rblSource control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void rblSource_SelectedIndexChanged( object sender, EventArgs e )
        {
            SetUserPreference( SOURCE_SETTING, rblSource.SelectedValue );
        }

        /// <summary>
        /// Handles the SelectedIndexChanged event of the cpCampus control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void cpCampus_SelectedIndexChanged( object sender, EventArgs e )
        {
            SetUserPreference( CAMPUS_SETTING, cpCampus.SelectedCampusId.ToString() );
        }

        #endregion

        #region Methods

        /// <summary>
        /// Shows the detail.
        /// </summary>
        private void ShowDetail()
        {
            // NOTE: Don't include Inactive Campuses for the cpCampus control
            cpCampus.Campuses = CampusCache.All( false );
            cpCampus.Items[0].Text = "";

            cpCampus.SelectedCampusId = GetUserPreference( CAMPUS_SETTING ).AsIntegerOrNull();
            if ( cpCampus.SelectedCampusId == null )
            {
                cpCampus.SelectedCampusId = CampusCache.All().First().Id;
            }

            // Set SMS Checkbox
            bool IsSmsChecked = GetAttributeValue( "IsSmsChecked" ).AsBoolean( true );
            cbSpouseSms.Checked = cbSms.Checked = IsSmsChecked;

            // Build Interests list...
            var interestList = GetAttributeValue( "Interests" ).SplitDelimitedValues( false );
            cblInterests.Items.Clear();
            foreach ( var interest in interestList )
            {
                cblInterests.Items.Add( new ListItem( interest, interest ) );
            }
            cblInterests.DataBind();

            // Build the Entry Source radio button list...
            var entrySourceList = GetAttributeValue( "EntrySource" ).SplitDelimitedValues( false );
            rblSource.Items.Clear();
            foreach ( var item in entrySourceList )
            {
                rblSource.Items.Add( new ListItem( item, item ) );
            }
            rblSource.DataBind();

            // Use the user's preference and set the Source Setting (if it is still actually still an option in the list).
            var sourceSetting = GetUserPreference( SOURCE_SETTING ).ToStringSafe();
            if ( rblSource.Items.Contains( new ListItem( sourceSetting, sourceSetting ) ) )
            {
                rblSource.SelectedValue = sourceSetting;
            }

            tbPrayerRequests.Visible = _isPrayerRequestEnabled;
        }

        /// <summary>
        /// Checks the settings.
        /// </summary>
        /// <returns></returns>
        private bool CheckSettings()
        {
            _rockContext = _rockContext ?? new RockContext();

            var connectionOpportunityService = new ConnectionOpportunityService( _rockContext );

            Guid? connectionOpportunityGuid = GetAttributeValue( "ConnectionOpportunity" ).AsGuidOrNull();
            if ( connectionOpportunityGuid.HasValue )
            {
                _connectionOpportunity = connectionOpportunityService.Get( connectionOpportunityGuid.Value );
            }

            if ( _connectionOpportunity == null )
            {
                connectionOpportunityGuid = PageParameter( "ConnectionOpportunityGuid" ).AsGuidOrNull();
                if ( connectionOpportunityGuid.HasValue )
                {
                    _connectionOpportunity = connectionOpportunityService.Get( connectionOpportunityGuid.Value );
                }
            }

            if ( _connectionOpportunity == null )
            {
                int? connectionOpportunityId = PageParameter( "ConnectionOpportunityId" ).AsIntegerOrNull();
                if ( connectionOpportunityId.HasValue )
                {
                    _connectionOpportunity = connectionOpportunityService.Get( connectionOpportunityId.Value );
                }
            }

            if ( _connectionOpportunity == null )
            {
                nbNotice.Heading = "Missing Connection Opportunity Setting";
                nbNotice.Text = "<p>Please edit the block settings. This block requires a valid Connection Opportunity setting.</p>";
                return false;
            }

            _dvcConnectionStatus = DefinedValueCache.Get( GetAttributeValue( "ConnectionStatus" ).AsGuid() );
            if ( _dvcConnectionStatus == null )
            {
                nbNotice.Heading = "Invalid Connection Status";
                nbNotice.Text = "<p>The selected Connection Status setting does not exist.</p>";
                return false;
            }

            _dvcRecordStatus = DefinedValueCache.Get( GetAttributeValue( "RecordStatus" ).AsGuid() );
            if ( _dvcRecordStatus == null )
            {
                nbNotice.Heading = "Invalid Record Status";
                nbNotice.Text = "<p>The selected Record Status setting does not exist.</p>";
                return false;
            }

            _married = DefinedValueCache.Get( Rock.SystemGuid.DefinedValue.PERSON_MARITAL_STATUS_MARRIED.AsGuid() );
            _homeAddressType = DefinedValueCache.Get( Rock.SystemGuid.DefinedValue.GROUP_LOCATION_TYPE_HOME.AsGuid() );
            _familyType = GroupTypeCache.Get( Rock.SystemGuid.GroupType.GROUPTYPE_FAMILY.AsGuid() );
            _adultRole = _familyType.Roles.FirstOrDefault( r => r.Guid.Equals( Rock.SystemGuid.GroupRole.GROUPROLE_FAMILY_MEMBER_ADULT.AsGuid() ) );

            if ( _married == null || _homeAddressType == null || _familyType == null || _adultRole == null )
            {
                nbNotice.Heading = "Missing System Value";
                nbNotice.Text = "<p>There is a missing or invalid system value. Check the settings for Marital Status of 'Married', Location Type of 'Home', Group Type of 'Family', and Family Group Role of 'Adult'.</p>";
                return false;
            }

            _isPrayerRequestEnabled = GetAttributeValue( "IsPrayerRequestEnabled" ).AsBoolean();

            return true;
        }

        /// <summary>
        /// Sets the phone number.
        /// </summary>
        /// <param name="rockContext">The rock context.</param>
        /// <param name="person">The person.</param>
        /// <param name="pnbNumber">The PNB number.</param>
        /// <param name="cbSms">The cb SMS.</param>
        /// <param name="phoneTypeGuid">The phone type unique identifier.</param>
        /// <param name="changes">The changes.</param>
        private void SetPhoneNumber( RockContext rockContext, Person person, PhoneNumberBox pnbNumber, RockCheckBox cbSms, Guid phoneTypeGuid, History.HistoryChangeList changes )
        {
            var phoneType = DefinedValueCache.Get( phoneTypeGuid );
            if ( phoneType != null )
            {
                var phoneNumber = person.PhoneNumbers.FirstOrDefault( n => n.NumberTypeValueId == phoneType.Id );
                string oldPhoneNumber = string.Empty;
                if ( phoneNumber == null )
                {
                    phoneNumber = new PhoneNumber { NumberTypeValueId = phoneType.Id };
                }
                else
                {
                    oldPhoneNumber = phoneNumber.NumberFormattedWithCountryCode;
                }

                phoneNumber.CountryCode = PhoneNumber.CleanNumber( pnbNumber.CountryCode );
                phoneNumber.Number = PhoneNumber.CleanNumber( pnbNumber.Number );

                if ( string.IsNullOrWhiteSpace( phoneNumber.Number ) )
                {
                    if ( phoneNumber.Id > 0 )
                    {
                        new PhoneNumberService( rockContext ).Delete( phoneNumber );
                        person.PhoneNumbers.Remove( phoneNumber );
                    }
                }
                else
                {
                    if ( phoneNumber.Id <= 0 )
                    {
                        person.PhoneNumbers.Add( phoneNumber );
                    }
                    if ( cbSms != null && cbSms.Checked )
                    {
                        phoneNumber.IsMessagingEnabled = true;
                        person.PhoneNumbers
                            .Where( n => n.NumberTypeValueId != phoneType.Id )
                            .ToList()
                            .ForEach( n => n.IsMessagingEnabled = false );
                    }
                }

                History.EvaluateChange( changes,
                    string.Format( "{0} Phone", phoneType.Value ),
                    oldPhoneNumber, phoneNumber.NumberFormattedWithCountryCode );
            }
        }

        /// <summary>
        /// Creates the connection request.
        /// </summary>
        /// <param name="rockContext">The rock context.</param>
        /// <param name="person">The person.</param>
        private void CreateConnectionRequest( RockContext rockContext, Person person )
        {
            if ( person != null && _connectionOpportunity != null )
            {
                int defaultStatusId = _connectionOpportunity.ConnectionType.ConnectionStatuses
                                    .Where( s => s.IsDefault )
                                    .Select( s => s.Id )
                                    .FirstOrDefault();

                ConnectionRequestService connectionRequestService = new ConnectionRequestService( rockContext );
                ConnectionRequest connectionRequest = new ConnectionRequest();
                connectionRequest.PersonAliasId = person.PrimaryAliasId.Value;
                connectionRequest.ConnectionOpportunityId = _connectionOpportunity.Id;
                connectionRequest.ConnectionState = ConnectionState.Active;
                connectionRequest.ConnectionStatusId = defaultStatusId;
                connectionRequest.CampusId = cpCampus.SelectedCampusId;

                StringBuilder sb = new StringBuilder();
                sb.AppendFormat( "* Entry Point: {0}{1}", rblSource.SelectedValue, Environment.NewLine );
                if ( cblInterests.SelectedValues.Count > 0 )
                {
                    sb.AppendFormat( "* Interested in:{0}", Environment.NewLine );
                    sb.AppendFormat( "  * {0}{1}", cblInterests.SelectedValues.AsDelimited( "\n  * ", "\n  * and " ), Environment.NewLine );
                }

                if ( !string.IsNullOrWhiteSpace( tbComments.Text ) )
                {
                    sb.AppendFormat( "{1}Additional Comments: {0}{1}", tbComments.Text, Environment.NewLine );
                }

                connectionRequest.Comments = sb.ToString();

                connectionRequestService.Add( connectionRequest );
                rockContext.SaveChanges();
            }
        }

        /// <summary>
        /// Creates the prayer request.
        /// </summary>
        /// <param name="rockContext">The rock context.</param>
        /// <param name="person">The person.</param>
        private void CreatePrayerRequest( RockContext rockContext, Person person )
        {
            if ( person != null && !tbPrayerRequests.Text.IsNullOrWhiteSpace() )
            {
                PrayerRequest prayerRequest = new PrayerRequest();
                prayerRequest.RequestedByPersonAliasId = person.PrimaryAliasId;
                prayerRequest.FirstName = person.NickName;
                prayerRequest.LastName = person.LastName;
                prayerRequest.Text = tbPrayerRequests.Text;
                prayerRequest.Email = person.Email;

                Category category;
                Guid defaultCategoryGuid = GetAttributeValue( "PrayerCategory" ).AsGuid();
                if ( !defaultCategoryGuid.IsEmpty() )
                {
                    category = new CategoryService( rockContext ).Get( defaultCategoryGuid );
                    prayerRequest.CategoryId = category.Id;
                    prayerRequest.Category = category;
                }

                prayerRequest.IsPublic = false;

                PrayerRequestService prayerRequestService = new PrayerRequestService( rockContext );
                prayerRequestService.Add( prayerRequest );
                prayerRequest.EnteredDateTime = RockDateTime.Now;
                rockContext.SaveChanges();
            }
        }
        #endregion
    }
}