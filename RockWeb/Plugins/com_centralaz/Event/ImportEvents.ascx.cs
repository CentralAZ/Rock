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
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.UI;
using System.Web.UI.WebControls;

using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;
using Rock.Web.UI.Controls;
using Rock.Attribute;

using CsvHelper;
using DDay.iCal;
using DDay.iCal.Serialization.iCalendar;
using CsvHelper.Configuration;

namespace RockWeb.Plugins.com_centralaz.Event
{
    /// <summary>
    /// Allows you to import a csv containing a list of events.
    /// 
    /// For each imported row, it will create:
    ///     * a registration instance with the Name of the imported item
    ///     * a group (of the configured type; under the configured parent group) with the Name of teh imported item
    ///     * an event item occurrence linking the registration to the occurrence and group
    ///     
    /// Also, the group-type needs to have two group attribute called:
    ///     * RegistrationInstanceId (int)
    ///     * EventOccurrenceId (int)
    /// </summary>
    [DisplayName( "Import Events" )]
    [Category( "com_centralaz > Event" )]
    [Description( "Allows you to import a csv containing a list of events (name, address, max size, date)" )]
    public partial class ImportEvents : Rock.Web.UI.RockBlock
    {
        #region Fields
        Dictionary<string, object> mergeFields = Rock.Lava.LavaHelper.GetCommonMergeFields( null );
        private static string SESSIONKEY_TO_PROCESS = "CentralAZImportEvents";
        private RockContext _rockContext = new RockContext();

        private readonly string _USER_PREF_EVENTITEMID = "CentralAZImportEvents:EventItemId";
        private readonly string _USER_PREF_REGTEMPLATEID = "CentralAZImportEvents:RegistrationTemplateId";
        private readonly string _USER_PREF_REGISTRATIONINSTANCESOURCEID = "CentralAZImportEvents:RegistrationInstanceSourceId";
        private readonly string _USER_PREF_GROUPTYPEID = "CentralAZImportEvents:GroupTypeId";
        private readonly string _USER_PREF_PARENTGROUPID = "CentralAZImportEvents:ParentGroupId";
        private readonly string _USER_PREF_SCHEDULE = "CentralAZImportEvents:iCalSchedule";

        private readonly string _GROUP_ATTRIB_REGISTRATIONINSTANCEID = "RegistrationInstanceId";
        private readonly string _GROUP_ATTRIB_EVENTOCCURRENCEID = "EventOccurrenceId";
        #endregion

        #region Properties

        // used for public / protected properties

        #endregion

        #region Base Control Methods

        //  overrides of the base RockBlock methods (i.e. OnInit, OnLoad)

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Init" /> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnInit( EventArgs e )
        {
            base.OnInit( e );
            // Display the version number.  If the plugin has an assembly you can do it like this:
            // lVersionText.Text = com.centralaz.PLUGINASSEMBLY.VersionInfo.GetPluginProductVersionNumber();
            lVersionText.Text = "1.2.1";

            // this event gets fired after block settings are updated. it's nice to repaint the screen if these settings would alter it
            this.BlockUpdated += Block_BlockUpdated;
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
                BindData();
            }
        }

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.PreRender" /> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnPreRender( EventArgs e )
        {
            base.OnPreRender( e );
        }

        #endregion

        #region Events

        // handlers called by the controls on your block

        /// <summary>
        /// Handles the BlockUpdated event of the control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void Block_BlockUpdated( object sender, EventArgs e )
        {

        }

        /// <summary>
        /// Handles the NextButtonClick event of the Wizard1 control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="WizardNavigationEventArgs"/> instance containing the event data.</param>
        protected void Wizard1_NextButtonClick( object sender, WizardNavigationEventArgs e )
        {
            WizardStepType nextStep = Wizard1.WizardSteps[e.NextStepIndex].StepType;

            if ( e.CurrentStepIndex == 0 )
            {
                if ( !ValidateFileUpload() || Session[SESSIONKEY_TO_PROCESS] == null )
                {
                    e.Cancel = true;
                    return;
                }
                else
                {
                    divErrors.Visible = false;
                }

                InitializeWizardStep2();
            }
            else if ( nextStep == WizardStepType.Finish )
            {
                // Prepare the summary and UI of the "Finish" step
                DisplaySummary();
            }
            else
            {
                Wizard1.StepPreviousButtonText = "Previous";
            }
        }

        /// <summary>
        /// Handles the FinishButtonClick event of the Wizard1 control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="WizardNavigationEventArgs"/> instance containing the event data.</param>
        protected void Wizard1_FinishButtonClick( object sender, WizardNavigationEventArgs e )
        {
            lblComplete.CssClass = "";
            int newEvents = 0;
            int updatedEvents = 0;
            int miscProblems = 0;
            int recordNumber = 0;

            List<string> probItems = new List<string>();
            var list = ( Queue<ImportEventGroup> ) Session[SESSIONKEY_TO_PROCESS];
            string name = string.Empty;

            try
            {
                int eventItemId = ddlEventItems.SelectedValueAsInt() ?? -1;
                int registrationTemplateId = ddlTemplates.SelectedValueAsInt() ?? -1;
                int registrationInstanceSourceId = ddlRegistrationInstanceSource.SelectedValueAsInt() ?? -1;
                int parentGroupId = gpParentGroup.SelectedValueAsInt() ?? -1;
                int groupTypeId = gtpGroupType.SelectedValueAsInt() ?? -1;

                var riService = new RegistrationInstanceService( _rockContext );
                var registrationInstanceSource = riService.Queryable().AsNoTracking()
                    .Where( i => i.Id == registrationInstanceSourceId )
                    .FirstOrDefault();

               var eventItemService = new EventItemService( _rockContext );
                var eventItem = eventItemService.Queryable()
                    .Where( i => i.Id == eventItemId )
                    .FirstOrDefault();

                var groupService = new GroupService( _rockContext );
                var parentGroup = groupService.Get( parentGroupId );

                // now process through each record we're importing...
                do
                {
                    recordNumber++;
                    ImportEventGroup item = list.Dequeue();
                    name = item.Name;
                    if ( AddOrUpdateEventRegistration( item, riService, registrationTemplateId, registrationInstanceSource, eventItemService, eventItem, parentGroup, groupTypeId ) )
                    {
                        newEvents++;
                    }
                    else
                    {
                        updatedEvents++;
                    }

                }
                while ( list.Count != 0 );

                _rockContext.SaveChanges();

            }
            catch ( System.Exception ex )
            {
                probItems.Add( name );
                lblComplete.Text += string.Format( "An error occurred while handling record {0} which prevented the events from being imported.<br/><br/>{1}<br/>", recordNumber, ex.Message, ex.StackTrace );
                lblComplete.CssClass = "errorText";
            }
            finally
            {
                Session[SESSIONKEY_TO_PROCESS] = null;
            }

            if ( newEvents > 0 )
            {
                lblCompleteMsg.Text += newEvents + " newly added events and groups.<br />";
            }

            if ( updatedEvents > 0 )
            {
                lblCompleteMsg.Text += updatedEvents + " successfully updated.<br />";
            }

            if ( miscProblems > 0 )
            {
                lblCompleteMsg.Text += miscProblems + " of the provided items were unable to be added or updated:<br /><br /> " +
                    string.Join( "<br/> ", probItems.ToArray() ) + "<br /><br />";
            }

            lblCompleteMsg.Text += string.Format( "<p>&nbsp;</p><p class='text-muted'><i>The import detected the file's encoding was {0}</i></p>", hfFileEncoding.Value );
        }

        /// <summary>
        /// Handles the SelectedIndexChanged event of the GroupType picker control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void gtpGroupType_SelectedIndexChanged( object sender, EventArgs e )
        {
            int? groupTypeId = gtpGroupType.SelectedValue.AsIntegerOrNull();

            if ( groupTypeId != null )
            {
                gpParentGroup.IncludedGroupTypeIds = new List<int>() { gtpGroupType.SelectedGroupTypeId ?? -1 };
                gpParentGroup.Enabled = true;
                gpParentGroup.SetValue( null );

                // save as user preference
                SetUserPreference( _USER_PREF_GROUPTYPEID, groupTypeId.Value.ToStringSafe() );
            }
        }

        /// <summary>
        /// Handles the SelectedIndexChanged event of the ddlRegistrationInstanceSource control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void ddlRegistrationInstanceSource_SelectedIndexChanged( object sender, EventArgs e )
        {
            SetUserPreference( _USER_PREF_REGISTRATIONINSTANCESOURCEID, ddlRegistrationInstanceSource.SelectedValue );
        }

        /// <summary>
        /// Handles the SelectedIndexChanged event of the ddlEventItems control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void ddlEventItems_SelectedIndexChanged( object sender, EventArgs e )
        {
            SetUserPreference( _USER_PREF_EVENTITEMID, ddlEventItems.SelectedValue );
        }

        /// <summary>
        /// Handles the DataBound event of the gtpGroupType control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void gtpGroupType_DataBound( object sender, EventArgs e )
        {
            BindGroupTypes();

            int? groupTypeId = gtpGroupType.SelectedValue.AsIntegerOrNull();

            if ( groupTypeId != null )
            {
                gpParentGroup.IncludedGroupTypeIds = new List<int>() { gtpGroupType.SelectedGroupTypeId ?? -1 };
                gpParentGroup.Enabled = true;
            }
        }

        /// <summary>
        /// Handles the SelectedIndexChanged event of the ddlTemplates control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void ddlTemplates_SelectedIndexChanged( object sender, EventArgs e )
        {
            int? registrationTemplateId = ddlTemplates.SelectedValue.AsIntegerOrNull();
            if ( registrationTemplateId != null && registrationTemplateId != 0 )
            {
                ddlRegistrationInstanceSource.Enabled = true;
                SetUserPreference( _USER_PREF_REGTEMPLATEID, registrationTemplateId.Value.ToStringSafe() );

                BindRegistrationInstances( registrationTemplateId.Value );
            }
            else
            {
                SetUserPreference( _USER_PREF_REGTEMPLATEID, string.Empty );
                ddlRegistrationInstanceSource.ClearSelection();
                ddlRegistrationInstanceSource.Items.Clear();
                ddlRegistrationInstanceSource.DataBind();
            }
        }

        /// <summary>
        /// Handles the Click event of the ClearSettings button clearing all the selections and resetting the saved user preferences.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void btnClearSettings_Click( object sender, EventArgs e )
        {
            ddlTemplates.ClearSelection();
            ddlTemplates.SetValue( string.Empty );

            ddlEventItems.ClearSelection();
            ddlEventItems.SetValue( string.Empty );

            ddlRegistrationInstanceSource.ClearSelection();
            ddlRegistrationInstanceSource.SetValue( string.Empty );

            gtpGroupType.ClearSelection();
            gtpGroupType.SetValue( string.Empty );

            gpParentGroup.SetValue( null );

            sbSchedule.iCalendarContent = "";

            SetUserPreference( _USER_PREF_EVENTITEMID, string.Empty );
            SetUserPreference( _USER_PREF_REGTEMPLATEID, string.Empty );
            SetUserPreference( _USER_PREF_REGISTRATIONINSTANCESOURCEID, string.Empty );
            SetUserPreference( _USER_PREF_GROUPTYPEID, string.Empty );
            SetUserPreference( _USER_PREF_PARENTGROUPID, string.Empty );
            SetUserPreference( _USER_PREF_SCHEDULE, string.Empty );
        }

        #endregion

        #region Methods

        /// <summary>
        /// Binds the data.
        /// </summary>
        private void BindData()
        {
            BindEventItems();
            BindTemplates();
            BindGroupTypes();
        }

        /// <summary>
        /// Binds the registration instances.
        /// </summary>
        /// <param name="registrationTemplateId">The registration template identifier.</param>
        private void BindRegistrationInstances( int registrationTemplateId )
        {
            ddlRegistrationInstanceSource.SelectedValue = null;
            ddlRegistrationInstanceSource.Items.Clear();

            var list = new Rock.Model.RegistrationInstanceService( _rockContext )
                .Queryable().AsNoTracking()
                .Where( r => r.RegistrationTemplateId == registrationTemplateId )
                .OrderBy( a => a.Name )
                .Select( a => new
                {
                    a.Id,
                    a.Name
                } ).ToList();

            if ( list.Count != 0 )
            {
                ddlRegistrationInstanceSource.Items.Add( new ListItem() );
                ddlRegistrationInstanceSource.Items.AddRange( list.Select( a => new ListItem( a.Name, a.Id.ToString() ) ).ToArray() );
            }
            else
            {
                ddlRegistrationInstanceSource.Items.Clear();
            }

            // set using user preference
            var registrationInstanceSourceId = GetUserPreference( _USER_PREF_REGISTRATIONINSTANCESOURCEID ).AsIntegerOrNull();
            if ( registrationInstanceSourceId != null )
            {
                ddlRegistrationInstanceSource.SetValue( registrationInstanceSourceId );
            }

            ddlRegistrationInstanceSource.DataBind();
            ddlRegistrationInstanceSource.ShowErrorMessage( Rock.Constants.WarningMessage.CannotBeBlank( GroupType.FriendlyTypeName ) );
        }

        /// <summary>
        /// Binds the templates.
        /// </summary>
        private void BindTemplates()
        {
            var list = new RegistrationTemplateService( _rockContext )
                .Queryable()
                .AsNoTracking()
                .Where( t => t.IsActive )
                .OrderBy( t => t.Name )
                .Select( a => new
                {
                    a.Id,
                    a.Name
                } ).ToList();

            ddlTemplates.Items.Add( new ListItem() );
            ddlTemplates.Items.AddRange( list.Select( a => new ListItem( a.Name, a.Id.ToString() ) ).ToArray() );
            ddlTemplates.DataBind();

            // Set the gorup type picker by user preference if a value was saved.
            var registrationTemplateId = GetUserPreference( _USER_PREF_REGTEMPLATEID ).AsIntegerOrNull();
            if ( registrationTemplateId != null )
            {
                ddlTemplates.SetValue( registrationTemplateId );
                ddlRegistrationInstanceSource.Enabled = true;
                BindRegistrationInstances( registrationTemplateId.Value );
            }
        }

        /// <summary>
        /// Binds the event items.
        /// </summary>
        private void BindEventItems()
        {
            var list = new EventItemService( _rockContext ).Queryable().Where( a => a.IsActive ).OrderBy( a => a.Name ).ToList();
            ddlEventItems.Items.Add( new ListItem() );
            ddlEventItems.Items.AddRange( list.Select( a => new ListItem( a.Name, a.Id.ToString() ) ).ToArray() );
            ddlEventItems.DataBind();

            // Set the gorup type picker by user preference if a value was saved.
            var eventItemId = GetUserPreference( _USER_PREF_EVENTITEMID ).AsIntegerOrNull();
            if ( eventItemId != null )
            {
                ddlEventItems.SetValue( eventItemId );
            }
        }

        /// <summary>
        /// Binds the group types.
        /// </summary>
        private void BindGroupTypes()
        {
            var allGroupTypes = new GroupTypeService( _rockContext ).Queryable().OrderBy( a => a.Name ).ToList();
            gtpGroupType.GroupTypes = allGroupTypes;

            // Set the gorup type picker by user preference if a value was saved.
            var groupTypeId = GetUserPreference( _USER_PREF_GROUPTYPEID ).AsIntegerOrNull();
            if ( groupTypeId != null )
            {
                gtpGroupType.SetValue( groupTypeId );
                gtpGroupType.SelectedGroupTypeId = groupTypeId;
                gpParentGroup.Enabled = true;
            }
        }

        /// <summary>
        /// Adds the or update event.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns></returns>
        private bool AddOrUpdateEventRegistration( ImportEventGroup item, RegistrationInstanceService riService, int registrationTemplateId, RegistrationInstance sourceInstance, EventItemService eiService, EventItem eventItem, Rock.Model.Group parentGroup, int groupTypeId )
        {
            bool isNew = false;

            var registrationInstance = riService.Queryable()
                .Where( i => i.RegistrationTemplateId == registrationTemplateId && i.Name == item.Name )
                .FirstOrDefault();

            // If it didn't already exist, create a new registration instance
            if ( registrationInstance == null )
            {
                isNew = true;
                registrationInstance = new RegistrationInstance();
                registrationInstance.Guid = Guid.NewGuid();

                registrationInstance = new RegistrationInstance()
                {
                    Guid = Guid.NewGuid(),
                    Name = item.Name,
                    IsActive = true,
                    RegistrationTemplateId = registrationTemplateId,
                    CreatedDateTime = RockDateTime.Now,
                    CreatedByPersonAliasId = CurrentPersonAliasId,
                };

                riService.Add( registrationInstance );
            }

            registrationInstance.MaxAttendees = item.MaxRegistrants;
            registrationInstance.StartDateTime = sourceInstance.StartDateTime;
            registrationInstance.EndDateTime = sourceInstance.EndDateTime;
            registrationInstance.SendReminderDateTime = sourceInstance.SendReminderDateTime;
            //registrationInstance.ContactPersonAliasId = sourceInstance.ContactPersonAliasId;
            //registrationInstance.ContactPhone = sourceInstance.ContactPhone;
            //registrationInstance.ContactEmail = sourceInstance.ContactEmail;
            registrationInstance.AccountId = sourceInstance.AccountId;

            // Render some lava to replace keywords in the source registration instance
            mergeFields.AddOrReplace( "LocationName", item.Name );
            mergeFields.AddOrReplace( "Address", string.Format( "{0}, {1}, {2} {3}", item.StreetAddress, item.City, item.State, item.Zip ) );

            registrationInstance.AdditionalReminderDetails = sourceInstance.AdditionalReminderDetails.ResolveMergeFields( mergeFields );
            registrationInstance.AdditionalConfirmationDetails = sourceInstance.AdditionalConfirmationDetails.ResolveMergeFields( mergeFields );

            registrationInstance.ModifiedDateTime = RockDateTime.Now;
            registrationInstance.ModifiedByPersonAliasId = CurrentPersonAliasId;

            _rockContext.SaveChanges();

            Rock.Model.Group group = AddOrUpdateGroup( item, parentGroup, groupTypeId, registrationInstance.Id );

            int eventItemOccurrenceId = AddOrUpdateEventItemOccurrence( item, registrationInstance, eventItem, group );
            _rockContext.SaveChanges();

            // Now that we have the event item occurrence Id we can save it to the Group's attribute:
            UpdateGroupEventItemOccurrenceId( group, eventItemOccurrenceId );

            return isNew;
        }

        /// <summary>
        /// Adds a new or updates a matching (by Name) event item occurrence.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="registrationInstance">The registration instance.</param>
        /// <param name="eventItem">The event item.</param>
        /// <param name="group">The group.</param>
        /// <returns></returns>
        private int AddOrUpdateEventItemOccurrence( ImportEventGroup item, RegistrationInstance registrationInstance, EventItem eventItem, Rock.Model.Group group )
        {
            var eventItemOccurrence = eventItem.EventItemOccurrences.Where( o => o.Location == item.Name ).FirstOrDefault();

            if ( eventItemOccurrence == null )
            {
                eventItemOccurrence = new EventItemOccurrence()
                {
                    EventItemId = eventItem.Id,
                    Guid = Guid.NewGuid(),
                    Location = item.Name,
                    Schedule = new Schedule(),
                    CampusId = item.CampusId,
                    CreatedDateTime = RockDateTime.Now,
                    CreatedByPersonAliasId = CurrentPersonAliasId,
                };

                // Tie it back to the event registration via the Linkages
                var linkage = new EventItemOccurrenceGroupMap();
                linkage.RegistrationInstanceId = registrationInstance.Id;
                linkage.PublicName = item.Name;
                linkage.GroupId = group.Id;
                

                eventItemOccurrence.Linkages.Add( linkage );
                eventItem.EventItemOccurrences.Add( eventItemOccurrence );
            }

            eventItemOccurrence.ModifiedDateTime = RockDateTime.Now;
            eventItemOccurrence.ModifiedByPersonAliasId = CurrentPersonAliasId;

            // Set the schedule
            eventItemOccurrence.Schedule = new Schedule();
            eventItemOccurrence.Schedule.iCalendarContent = sbSchedule.iCalendarContent;

            // Save to get the eventItemOccurenceId
            _rockContext.SaveChanges();

            // Set the linkage UrlSlug
            var linkage2 = eventItemOccurrence.Linkages.FirstOrDefault();
            linkage2.UrlSlug = string.Format( "{0}-{1}", registrationInstance.Id, eventItemOccurrence.Id );
            _rockContext.SaveChanges();

            return eventItemOccurrence.Id;
        }

        /// <summary>
        /// Handles the SaveSchedule event of the sbSchedule control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void sbSchedule_SaveSchedule( object sender, EventArgs e )
        {
            var schedule = new Schedule { iCalendarContent = sbSchedule.iCalendarContent };
            lScheduleText.Text = schedule.FriendlyScheduleText;
        }

        /// <summary>
        /// Adds a new group or updates the existing matching (by Name) group found under the parentGroup.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="parentGroup">The parent group.</param>
        /// <param name="groupTypeId">The group type identifier.</param>
        /// <param name="registrationInstanceid">The registration instanceid.</param>
        /// <returns>the new or updated Group</returns>
        private Rock.Model.Group AddOrUpdateGroup( ImportEventGroup item, Rock.Model.Group parentGroup, int groupTypeId, int registrationInstanceid )
        {
            var group = parentGroup.Groups.Where( g => g.Name == item.Name ).FirstOrDefault();

            var description = RemoveSpecialCharacters( item.Description );

            if ( group == null )
            {
                group = new Rock.Model.Group()
                {
                    ParentGroupId = parentGroup.Id,
                    GroupTypeId = groupTypeId,
                    Guid = Guid.NewGuid(),
                    Name = item.Name,
                    CreatedDateTime = RockDateTime.Now,
                    Description = description,
                };

                // Tie it back a Location
                var location = new Location()
                {
                    Guid = Guid.NewGuid(),
                    Street1 = item.StreetAddress,
                    City = item.City,
                    State = item.State,
                    PostalCode = item.Zip,
                    CreatedDateTime = RockDateTime.Now,
                    CreatedByPersonAliasId = CurrentPersonAliasId,
                };

                var groupLocation = new GroupLocation()
                {
                    Guid = Guid.NewGuid(),
                    Location = location,
                    CreatedDateTime = RockDateTime.Now,
                    ModifiedDateTime = RockDateTime.Now,
                    CreatedByPersonAliasId = CurrentPersonAliasId,
                };

                parentGroup.Groups.Add( group );

                group.GroupLocations.Add( groupLocation );
                _rockContext.SaveChanges();
            }

            // Save the attribute values.
            group.LoadAttributes();
            group.SetAttributeValue( _GROUP_ATTRIB_REGISTRATIONINSTANCEID, registrationInstanceid.ToString() );
            group.SaveAttributeValues( _rockContext );

            // Set the group's description if one came in the import.
            if ( ! string.IsNullOrWhiteSpace( description ) )
            {
                group.Description = description;
            }

            group.CampusId = item.CampusId; 
            group.GroupCapacity = item.MaxRegistrants;
            group.ModifiedDateTime = RockDateTime.Now;
            group.ModifiedByPersonAliasId = CurrentPersonAliasId;

            _rockContext.SaveChanges();

            return group;
        }

        /// <summary>
        /// Updates the group's event item occurrence identifier attribute.
        /// </summary>
        /// <param name="group">The group.</param>
        /// <param name="eventItemOccurrenceId">The event item occurrence identifier.</param>
        private void UpdateGroupEventItemOccurrenceId(Rock.Model.Group group, int eventItemOccurrenceId )
        {
            group.SetAttributeValue( _GROUP_ATTRIB_EVENTOCCURRENCEID, eventItemOccurrenceId.ToString() );
            group.SaveAttributeValues( _rockContext );
        }

        /// <summary>
        /// Initializes the wizard step2.
        /// </summary>
        private void InitializeWizardStep2()
        {
            var allGroupTypes = new GroupTypeService( _rockContext ).Queryable().OrderBy( a => a.Name ).ToList();
            gtpGroupType.GroupTypes = allGroupTypes;

            LoadUserPreferences();
        }

        /// <summary>
        /// Loads the user preferences.
        /// </summary>
        private void LoadUserPreferences()
        {
            var registrationTemplateId = GetUserPreference( _USER_PREF_REGTEMPLATEID ).AsIntegerOrNull();
            if ( registrationTemplateId != null )
            {
                ddlTemplates.SetValue( registrationTemplateId );
            }

            var registrationInstanceSourceId = GetUserPreference( _USER_PREF_REGISTRATIONINSTANCESOURCEID ).AsIntegerOrNull();
            if ( registrationInstanceSourceId != null )
            {
                ddlRegistrationInstanceSource.SetValue( registrationInstanceSourceId );
            }

            var groupTypeId = GetUserPreference( _USER_PREF_GROUPTYPEID ).AsIntegerOrNull();
            if ( groupTypeId != null )
            {
                gtpGroupType.SetValue( groupTypeId );
                gtpGroupType.SelectedGroupTypeId = groupTypeId;
            }

            var groupId = GetUserPreference( _USER_PREF_PARENTGROUPID ).AsIntegerOrNull();
            if ( groupId != null )
            {
                gpParentGroup.SetValue( groupId );
            }

            var iCal = GetUserPreference( _USER_PREF_SCHEDULE );
            if ( iCal != null )
            {
                sbSchedule.iCalendarContent = iCal;
                var schedule = new Schedule { iCalendarContent = sbSchedule.iCalendarContent };
                lScheduleText.Text = schedule.FriendlyScheduleText;
            }
        }

        /// <summary>
        /// Handles the FileUploaded event of the fupContentFile control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void fupContentFile_FileUploaded( object sender, EventArgs e )
        {
            //string physicalFileName = this.Request.MapPath( fuprExampleContentFile.UploadedContentFilePath );
            //lblPhysicalFileName.Text = "Uploaded File: " + physicalFileName;
        }


        /// <summary>
        /// Validates the file upload.
        /// </summary>
        /// <returns></returns>
        private bool ValidateFileUpload()
        {
            bool isValid = false;
            bool errors = false;
            lblErrors.Text = string.Empty;
            StringBuilder sbErrors = new StringBuilder();
            int i = 0;
            string lastSuccessfulItemName = string.Empty;

            var campuses = CampusCache.All();

            try
            {
                var list = new Queue<ImportEventGroup>();

                string physicalFileName = this.Request.MapPath( fuprExampleContentFile.UploadedContentFilePath );

                //char ch;
                //int Tchar = 0;
                //StreamReader reader = new StreamReader( physicalFileName, Encoding.Unicode );
                //do
                //{
                //    ch = ( char ) reader.Read();
                //    Console.Write( ch );
                //    if ( Convert.ToInt32( ch ) == 34 )
                //    {
                //        Console.Write( @";" );
                //    }
                //    Tchar++;
                //} while ( !reader.EndOfStream );
                //reader.Close();
                //reader.Dispose();

                using ( StreamReader sr = new StreamReader( physicalFileName) )
                {
                    //CvsConfiguration 
                    CsvReader csvReader = new CsvReader( sr );
                    csvReader.Configuration.WillThrowOnMissingField = false;
                    csvReader.Configuration.IgnoreHeaderWhiteSpace = true;

                    try
                    { 
                        var records = csvReader.GetRecords<ImportEventGroup>().ToList();
                        foreach ( var r in records )
                        {
                            i++;
                            var campusId = campuses.Where( c => c.Name.ToLower() == r.Campus.Trim().ToLower() ).Select( c => c.Id ).FirstOrDefault();
                            if ( campusId == 0 )
                            {
                                throw new Exception( string.Format( "Record number {0} does not have a valid campus. ", i ) );
                            }
                            r.CampusId = campusId;

                            lastSuccessfulItemName = r.Name;
                            list.Enqueue( r );
                        }

                        Session[SESSIONKEY_TO_PROCESS] = list;
                        isValid = true;
                    }
                    catch ( Exception ex2 )
                    {
                        errors = true;
                        sbErrors.AppendFormat( "Record number {0} is not valid. ", i );
                        if ( i > 1 )
                        {
                            sbErrors.AppendFormat( "(That's the one that comes after '<b>{0}</b>'. ) ", lastSuccessfulItemName );
                        }

                        sbErrors.AppendFormat( "<small class='text-info'>{0}</small>", ex2.Message );
                    }

                    hfFileEncoding.Value = sr.CurrentEncoding.EncodingName;
                }
            }
            catch ( Exception ex )
            {
                errors = true;
                sbErrors.Append( "No file was found. " + ex.Message );
            }

            if ( errors )
            {
                lblErrors.Text = sbErrors.ToString();
                divErrors.Visible = true;
            }
            else
            {
                isValid = true;
            }

            return isValid;
        }

        /// <summary>
        /// Displays the summary.
        /// </summary>
        private void DisplaySummary()
        {
            Queue<ImportEventGroup> records = null;

            if ( Session[SESSIONKEY_TO_PROCESS] != null )
            {
                records = ( Queue<ImportEventGroup> ) Session[SESSIONKEY_TO_PROCESS];
            }

            if ( records == null )
            {
                throw new Exception( "Error: Session reset.  Please start over." );
            }

            int? eventItemId = ddlEventItems.SelectedValueAsInt();
            Rock.Model.EventItem eventItem = null;
            if ( eventItemId.HasValue )
            {
                eventItem = new EventItemService( _rockContext ).Queryable().AsNoTracking()
                    .Where( e => e.Id == eventItemId.Value )
                    .FirstOrDefault();
            }

            int? parentGroupId = gpParentGroup.SelectedValueAsInt();
            Rock.Model.Group parentGroup = null;
            if ( parentGroupId.HasValue )
            {
                parentGroup = new GroupService( _rockContext ).Queryable().AsNoTracking()
                    .Where( g => g.Id == parentGroupId.Value )
                    .FirstOrDefault();
            }

            int? registrationTemplateId = ddlTemplates.SelectedValueAsInt();
            RegistrationTemplate registrationTemplate = null;
            if ( registrationTemplateId.HasValue )
            {
                registrationTemplate = new RegistrationTemplateService( _rockContext ).Queryable().AsNoTracking()
                    .Where( t => t.Id == registrationTemplateId.Value )
                    .FirstOrDefault();
            }

            int? registrationInstanceSourceId = ddlRegistrationInstanceSource.SelectedValueAsInt();
            RegistrationInstance registrationInstanceSource = null;
            if ( registrationInstanceSourceId.HasValue )
            {
                registrationInstanceSource = new RegistrationInstanceService( _rockContext ).Queryable().AsNoTracking()
                    .Where( t => t.Id == registrationInstanceSourceId.Value )
                    .FirstOrDefault();
            }

            int? groupTypeId = gtpGroupType.SelectedGroupTypeId;
            GroupType groupType = null;
            if ( groupTypeId.HasValue )
            {
                groupType = new GroupTypeService( _rockContext ).Get( groupTypeId.Value );
            }

            var schedule = new Schedule { iCalendarContent = sbSchedule.iCalendarContent };
            var calendarEvent = schedule.GetCalenderEvent();
            int hours = ( calendarEvent.Duration.Days * 24 ) + calendarEvent.Duration.Hours;
            string minutes = calendarEvent.Duration.Minutes.ToString();

            if ( registrationInstanceSource != null && registrationTemplate != null && parentGroup != null )
            {
                lblSummary.Text = String.Format( @"
            <dl class='dl-vertical'>
                <dt>Events to import:</dt>
                <dd>{0}</dd>         
                <dt>Using <i>registration template</i>:</dt>
                <dd>{1}</dd>
                <dt>Using source registration instance:</dt>
                <dd>{2}</dd>
                <dt>With occurrences under <i>event</i> item:</dt>
                <dd>{3}</dd>    
                <dt>With new groups to be put under the <i>parent</i> group:</dt>
                <dd>{4}</dd>
                <dt>Using <i>Group Type</i>:</dt>
                <dd>{5}</dd>
                <dt>And the event occurrences taking place:</dt>
                <dd>{6} for {7} hours {8} minutes</dd>

            </dl>", records.Count(), registrationTemplate.Name, registrationInstanceSource.Name, eventItem.Name, parentGroup.Name, groupType.Name, schedule.FriendlyScheduleText, hours, minutes  );
            }

            SetUserPreference( _USER_PREF_EVENTITEMID, eventItemId.HasValue ? eventItemId.Value.ToStringSafe() : string.Empty );
            SetUserPreference( _USER_PREF_REGTEMPLATEID, registrationTemplateId.HasValue ? registrationTemplateId.Value.ToStringSafe() : string.Empty );
            SetUserPreference( _USER_PREF_REGISTRATIONINSTANCESOURCEID, registrationInstanceSourceId.HasValue ? registrationInstanceSourceId.Value.ToStringSafe() : string.Empty );
            SetUserPreference( _USER_PREF_GROUPTYPEID, gtpGroupType.SelectedValue );
            SetUserPreference( _USER_PREF_PARENTGROUPID, parentGroupId.HasValue ? parentGroupId.Value.ToStringSafe() : string.Empty );
            SetUserPreference( _USER_PREF_SCHEDULE, sbSchedule.iCalendarContent != null ? sbSchedule.iCalendarContent : string.Empty );
        }

        #endregion

        public string RemoveSpecialCharacters( string str )
        {
            // Convert MS special quotes to regular quotes.
            if ( str.IndexOf( '\u2032' ) > -1 )
            {
                str = str.Replace( '\u2032', '\'' );
            }
            if ( str.IndexOf( '\u2033' ) > -1 )
            {
                str = str.Replace( '\u2033', '\"' );
            }
            if ( str.IndexOf( '\u2026' ) > -1 )
            {
                str = str.Replace( "\u2026", "..." );
            }
            if ( str.IndexOf( '\u201d' ) > -1 )
            {
                str = str.Replace( '\u201d', '\'' );
            }
            if ( str.IndexOf( '\u0092' ) > -1 )
            {
                str = str.Replace( '\u0092', '\'' );
            }
            str = str.Replace( ( char ) 0x2019, '\'' );
            str = str.Replace( ( char ) 0x92, '\'' );
            str = str.Replace( ( char ) 0xA0, ' ' );

            StringBuilder sb = new StringBuilder();
            foreach ( char c in str )
            {
                if ( ( c >= '0' && c <= '9' ) || ( c >= 'A' && c <= 'Z' ) || ( c >= 'a' && c <= 'z' ) 
                    || c == '.' || c == ',' || c == '_' || c == '\'' || c == ' ' || c == '-' || c == '"' || c == '!' || c == '?' )
                {
                    sb.Append( c );
                }
            }
            return sb.ToString();
        }

    }

    public sealed class ImportEventGroupClassMap : CsvClassMap<ImportEventGroup>
    {
        public ImportEventGroupClassMap()
        {
           // Map( m => m.CampusId ).Ignore;
            //Map( m => m.FirstName ).Index( 1 ).Name( "First Name" );
            //Map( m => m.LastName ).Index( 2 ).Name( "Last Name" );
        }
    }

    /// <summary>
    /// A helper class to hold a single record from the imported data.
    /// </summary>
    public class ImportEventGroup
    {
        public string Name { get; set; }
        public string CrossStreets { get; set; }
        public string StreetAddress { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Zip { get; set; }
        public string Campus { get; set; }
        public int CampusId { get; set; }
        public int MaxRegistrants { get; set; }
        public string Description { get; set; }

    }

    public class ImportSettings
    {
        public int RegistrationTemplateId { get; set; }
        public int RegistrationInstanceIdSource { get; set; }
        public int GroupTypeId { get; set; }
        public int ParentGroupId { get; set; }
    }
}