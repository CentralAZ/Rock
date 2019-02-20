﻿// <copyright>
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
using System.Data.Entity;
using System.Web.Security;
using Rock.Security;
using System.Text;

namespace RockWeb.Plugins.com_centralaz.ChurchMetrics
{
    /// <summary>
    /// Block for easily adding/editing metric values for any metric that has partitions of campus and service time.
    /// </summary>
    [DisplayName( "Servant Minister Metrics Entry" )]
    [Category( "com_centralaz > ChurchMetrics" )]
    [Description( "Block for easily adding/editing metric values for any metric that has partitions of campus and service time." )]

    // Metric Categories
    [MetricCategoriesField( "Metric Categories", "Select the metric categories to display (note: only metrics in those categories with a campus and schedule partition will displayed).", true, "", "Metric Categories", 0 )]

    // Permission Settings
    [GroupField( "Group", "The group that dictates who can add metrics", true, "", "Permission Settings", 1 )]
    [TextField( "Authorized Campuses Attribute Key", "The key to the groupmember attribute that dictates which campuses the person can enter metrics for.", true, "Campuses", "Permission Settings", 2 )]
    [TextField( "Notes Visible Attribute Key", "The key to the groupmember attribute that dictates whether the person can see the notes field.", true, "CanSeeNotes", "Permission Settings", 3 )]
    [IntegerField( "Number of Months until Notification displayed again.", "", true, 3, "Permission Settings", 4, "Months" )]
    [KeyValueListField( "Metric Entry Blacklist", "A key/value list of metrics that can't be saved together.  This prevents users from saving values of two metrics when they should only be able to update one or the other.", false, "", "Metric Id", "Metric Id", "", "", "Permission Settings", 5 )]
    [CodeEditorField( "Blacklist Custom Message", "A custom message to be displayed instead of the default blacklist message.", CodeEditorMode.Html, CodeEditorTheme.Rock, 200, false, "", "Permission Settings", 6 )]

    // Schedule Categories
    [CategoryField( "Holiday Schedule Category", "The schedule category to use for list of holiday service times. If this category has child categories, Rock will use those too.", false, "Rock.Model.Schedule", "", "", false, "", "Schedule Categories", 5 )]
    [CategoryField( "Weekend Schedule Category", "The schedule category to use for list of service times. If this category has child categories, Rock will search for one that contains the name of the currently selected campus. Otherwise, Rock will use this one.", false, "Rock.Model.Schedule", "", "", false, "", "Schedule Categories", 6 )]
    [CategoryField( "Event Schedule Category", "The schedule category to use for list of event times. If this category has child categories, Rock will search for one that contains the name of the currently selected campus. Otherwise, Rock will use this one.", false, "Rock.Model.Schedule", "", "", false, "", "Schedule Categories", 7 )]

    public partial class ServantMinisterMetricsEntry : Rock.Web.UI.RockBlock
    {
        #region Fields

        private int? _selectedCampusId { get; set; }
        private DateTime? _selectedWeekend { get; set; }
        private int? _selectedServiceId { get; set; }

        #endregion

        #region Base Control Methods

        /// <summary>
        /// Restores the view-state information from a previous user control request that was saved by the <see cref="M:System.Web.UI.UserControl.SaveViewState" /> method.
        /// </summary>
        /// <param name="savedState">An <see cref="T:System.Object" /> that represents the user control state to be restored.</param>
        protected override void LoadViewState( object savedState )
        {
            base.LoadViewState( savedState );
            _selectedCampusId = ViewState["SelectedCampusId"] as int?;
            _selectedWeekend = ViewState["SelectedWeekend"] as DateTime?;
            _selectedServiceId = ViewState["SelectedServiceId"] as int?;
        }

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

            nbMetricsSaved.Visible = false;

            if ( !Page.IsPostBack )
            {
                _selectedCampusId = GetBlockUserPreference( "CampusId" ).AsIntegerOrNull();
                _selectedServiceId = GetBlockUserPreference( "ScheduleId" ).AsIntegerOrNull();

                if ( CheckSelection() )
                {
                    DisplayLeadTeamMessage();
                    DisplayAdminMessage();
                    LoadDropDowns();
                    BindMetrics();
                }
            }
        }

        /// <summary>
        /// Saves any user control view-state changes that have occurred since the last page postback.
        /// </summary>
        /// <returns>
        /// Returns the user control's current view state. If there is no view state associated with the control, it returns null.
        /// </returns>
        protected override object SaveViewState()
        {
            ViewState["SelectedCampusId"] = _selectedCampusId;
            ViewState["SelectedWeekend"] = _selectedWeekend;
            ViewState["SelectedServiceId"] = _selectedServiceId;
            return base.SaveViewState();
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
            BindMetrics();
        }

        /// <summary>
        /// Handles the ItemCommand event of the rptrSelection control.
        /// </summary>
        /// <param name="source">The source of the event.</param>
        /// <param name="e">The <see cref="RepeaterCommandEventArgs"/> instance containing the event data.</param>
        protected void rptrSelection_ItemCommand( object source, RepeaterCommandEventArgs e )
        {
            switch ( e.CommandName )
            {
                case "Campus":
                    _selectedCampusId = e.CommandArgument.ToString().AsIntegerOrNull();
                    break;
                case "Weekend":
                    _selectedWeekend = e.CommandArgument.ToString().AsDateTime();
                    break;
                case "Service":
                    _selectedServiceId = e.CommandArgument.ToString().AsIntegerOrNull();
                    break;
            }

            if ( CheckSelection() )
            {
                LoadDropDowns();
                BindMetrics();
            }
        }

        /// <summary>
        /// Handles the ItemDataBound event of the rptrMetric control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RepeaterItemEventArgs"/> instance containing the event data.</param>
        protected void rptrMetric_ItemDataBound( object sender, RepeaterItemEventArgs e )
        {
            if ( e.Item.ItemType == ListItemType.Item )
            {
                var nbMetricValue = e.Item.FindControl( "nbMetricValue" ) as NumberBox;
                if ( nbMetricValue != null )
                {
                    nbMetricValue.ValidationGroup = BlockValidationGroup;
                }
            }
        }

        /// <summary>
        /// Handles the Click event of the btnSave control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void btnSave_Click( object sender, EventArgs e )
        {
            nbMetricErrors.Visible = false;
            nbMetricErrors.Text = "";

            if ( !MetricValuesValid() )
            {
                nbMetricErrors.Visible = true;
            }
            else
            {
                int campusEntityTypeId = EntityTypeCache.Get( typeof( Rock.Model.Campus ) ).Id;
                int scheduleEntityTypeId = EntityTypeCache.Get( typeof( Rock.Model.Schedule ) ).Id;

                int? campusId = bddlCampus.SelectedValueAsInt();
                int? scheduleId = bddlService.SelectedValueAsInt();
                DateTime? weekend = bddlWeekend.SelectedValue.AsDateTime();

                StringBuilder sb = new StringBuilder();
                if ( campusId.HasValue && scheduleId.HasValue && weekend.HasValue )
                {
                    using ( var rockContext = new RockContext() )
                    {
                        var metricService = new MetricService( rockContext );
                        var metricValueService = new MetricValueService( rockContext );

                        foreach ( RepeaterItem item in rptrMetric.Items )
                        {
                            var hfMetricIId = item.FindControl( "hfMetricId" ) as HiddenField;
                            var hfModifiedDateTime = item.FindControl( "hfModifiedDateTime" ) as HiddenField;
                            var nbMetricValue = item.FindControl( "nbMetricValue" ) as NumberBox;

                            if ( hfMetricIId != null && nbMetricValue != null )
                            {
                                int metricId = hfMetricIId.ValueAsInt();
                                DateTime? modifiedDateTime = hfModifiedDateTime.Value.AsDateTime();
                                var metric = new MetricService( rockContext ).Get( metricId );

                                if ( metric != null )
                                {
                                    int campusPartitionId = metric.MetricPartitions.Where( p => p.EntityTypeId.HasValue && p.EntityTypeId.Value == campusEntityTypeId ).Select( p => p.Id ).FirstOrDefault();
                                    int schedulePartitionId = metric.MetricPartitions.Where( p => p.EntityTypeId.HasValue && p.EntityTypeId.Value == scheduleEntityTypeId ).Select( p => p.Id ).FirstOrDefault();

                                    var metricValue = metricValueService
                                        .Queryable()
                                        .Where( v =>
                                            v.MetricId == metric.Id &&
                                            v.MetricValueDateTime.HasValue && v.MetricValueDateTime.Value == weekend.Value &&
                                            v.MetricValuePartitions.Count == 2 &&
                                            v.MetricValuePartitions.Any( p => p.MetricPartitionId == campusPartitionId && p.EntityId.HasValue && p.EntityId.Value == campusId.Value ) &&
                                            v.MetricValuePartitions.Any( p => p.MetricPartitionId == schedulePartitionId && p.EntityId.HasValue && p.EntityId.Value == scheduleId.Value ) )
                                        .FirstOrDefault();

                                    if ( metricValue == null )
                                    {
                                        metricValue = new MetricValue();
                                        metricValue.MetricValueType = MetricValueType.Measure;
                                        metricValue.MetricId = metric.Id;
                                        metricValue.MetricValueDateTime = weekend.Value;
                                        metricValueService.Add( metricValue );

                                        var campusValuePartition = new MetricValuePartition();
                                        campusValuePartition.MetricPartitionId = campusPartitionId;
                                        campusValuePartition.EntityId = campusId.Value;
                                        metricValue.MetricValuePartitions.Add( campusValuePartition );

                                        var scheduleValuePartition = new MetricValuePartition();
                                        scheduleValuePartition.MetricPartitionId = schedulePartitionId;
                                        scheduleValuePartition.EntityId = scheduleId.Value;
                                        metricValue.MetricValuePartitions.Add( scheduleValuePartition );

                                        metricValue.YValue = nbMetricValue.Text.AsDecimalOrNull();
                                        metricValue.Note = tbNote.Text;
                                    }
                                    else
                                    {
                                        if ( nbMetricValue.Text.AsDecimalOrNull() != metricValue.YValue )
                                        {
                                            if ( modifiedDateTime.ToString() == metricValue.ModifiedDateTime.ToString() || metricValue.YValue == null )
                                            {
                                                metricValue.YValue = nbMetricValue.Text.AsDecimalOrNull();
                                                metricValue.Note = tbNote.Text;
                                            }
                                            else
                                            {
                                                sb.AppendFormat( "<li>{0}, last updated by {1}</li>", metric.Title, metricValue.ModifiedByPersonName );
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        rockContext.SaveChanges();
                    }

                    nbMetricsSaved.Text = string.Format( "Your metrics for the {0} service on {1} at the {2} Campus have been saved.",
                        bddlService.SelectedItem.Text, bddlWeekend.SelectedItem.Text, bddlCampus.SelectedItem.Text );
                    nbMetricsSaved.Visible = true;

                    if ( sb.ToString().IsNotNullOrWhiteSpace() )
                    {
                        nbMetricsSkipped.Text = string.Format( "The following metrics were not saved, due to another user saving a more recent version:</br><ul>{0}</ul>", sb.ToString() );
                        nbMetricsSkipped.Visible = true;
                    }

                    BindMetrics();

                }
            }
        }

        /// <summary>
        /// Handles the SelectionChanged event of the filter controls.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void bddl_SelectionChanged( object sender, EventArgs e )
        {
            BindMetrics();
        }

        /// <summary>
        /// Handles the SelectedIndexChanged event of the bddlCampus control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void bddlCampus_SelectionChanged( object sender, EventArgs e )
        {
            _selectedCampusId = bddlCampus.SelectedValueAsInt();
            bddlService.Items.Clear();

            // Load service times
            var serviceList = GetServices();
            if ( serviceList.Any() )
            {
                foreach ( var service in serviceList )
                {
                    bddlService.Items.Add( new ListItem( service.Name, service.Id.ToString() ) );
                }

                if ( _selectedServiceId.HasValue )
                {
                    bddlService.SetValue( _selectedServiceId.Value );
                }
            }
            else
            {
                bddlService.Items.Add( new ListItem( "N/A" ) );
                bddlService.SetValue( "N/A" );
            }

            BindMetrics();
        }

        /// <summary>
        /// Handles the Click event of the btnLogout control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void btnLogout_Click( object sender, EventArgs e )
        {
            var transaction = new Rock.Transactions.UserLastActivityTransaction();
            transaction.UserId = CurrentUser.Id;
            transaction.LastActivityDate = RockDateTime.Now;
            transaction.IsOnLine = false;
            Rock.Transactions.RockQueue.TransactionQueue.Enqueue( transaction );


            FormsAuthentication.SignOut();

            // After logging out check to see if an anonymous user is allowed to view the current page.  If so
            // redirect back to the current page, otherwise redirect to the site's default page
            var currentPage = PageCache.Get( RockPage.PageId );
            if ( currentPage != null && currentPage.IsAuthorized( Authorization.VIEW, null ) )
            {
                Response.Redirect( CurrentPageReference.BuildUrl() );
                Context.ApplicationInstance.CompleteRequest();
            }
            else
            {
                RockPage.Layout.Site.RedirectToDefaultPage();
            }
        }

        /// <summary>
        /// Handles the Click event of the lbCloseMessage control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void lbCloseMessage_Click( object sender, EventArgs e )
        {
            var now = RockDateTime.Now;
            SetUserPreference( "MessageViewedDate", now.ToString() );
            divLeadTeamMessage.Visible = false;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Displays instructions for recording past data if:
        /// 1) The user has never exited out of the message
        /// 2) The user is due to have the message appear again
        /// </summary>
        private void DisplayLeadTeamMessage()
        {
            bool displayMessage = true;
            DateTime? messageViewedDate = GetUserPreference( "MessageViewedDate" ).AsDateTime();
            if ( messageViewedDate != null )
            {
                int? monthsUntilMessageDisplayed = GetAttributeValue( "Months" ).AsIntegerOrNull();
                if ( monthsUntilMessageDisplayed == null )
                {
                    monthsUntilMessageDisplayed = 3;
                }

                DateTime dateToDisplayMessage = messageViewedDate.Value.AddMonths( monthsUntilMessageDisplayed.Value );
                if ( RockDateTime.Now < dateToDisplayMessage )
                {
                    displayMessage = false;
                }
            }

            divLeadTeamMessage.Visible = displayMessage;
        }

        /// <summary>
        /// Displays a message to admins if there are future holiday schedules configured for the week.
        /// </summary>
        private void DisplayAdminMessage()
        {
            if ( IsUserAuthorized( "Administrate" ) )
            {
                var schedules = new List<Schedule>();
                using ( var rockContext = new RockContext() )
                {
                    var scheduleService = new ScheduleService( rockContext );

                    // check the holiday schedule categories for any schedules that will be active for the current week.
                    var holidayScheduleCategory = CategoryCache.Get( GetAttributeValue( "HolidayScheduleCategory" ).AsGuid() );
                    if ( holidayScheduleCategory != null )
                    {
                        var holidayCategoryIds = new List<int>();
                        holidayCategoryIds.Add( holidayScheduleCategory.Id );
                        if ( holidayScheduleCategory.Categories.Any() )
                        {
                            holidayCategoryIds.AddRange( holidayScheduleCategory.Categories.Select( c => c.Id ).ToList() );
                        }

                        foreach ( var schedule in scheduleService.Queryable().AsNoTracking()
                            .Where( s =>
                                s.CategoryId.HasValue &&
                                s.IsActive &&
                                holidayCategoryIds.Contains( s.CategoryId.Value ) )
                            .ToList() )
                        {
                            // Here we grab schedules if their EffectiveStartDate( First time they occur ) will occur any day
                            //   for the current week.  We only check for non-reoccuring schedules since holidays only use one
                            //   off schedules.
                            if (  
                                schedule.EffectiveStartDate.HasValue &&
                                schedule.EffectiveStartDate.Value.Date >= RockDateTime.Now.StartOfWeek( RockDateTime.FirstDayOfWeek ) &&
                                schedule.EffectiveStartDate.Value.Date <= RockDateTime.Now.EndOfWeek( RockDateTime.FirstDayOfWeek )
                                )
                            {
                                schedules.Add( schedule );
                            }
                        }
                    }

                    if ( schedules.Any() )
                    {
                        StringBuilder sb = new StringBuilder();

                        sb.AppendLine( "Admins, the following Holiday schedules will be available for entry this week:<br/>" );
                        sb.AppendLine( "<ul>" );

                        foreach ( var schedule in schedules )
                        {
                            sb.AppendLine( "<li>" + schedule.Name + "</li>" );
                        }

                        sb.AppendLine( "</ul>" );
                        nbFutureMetrics.Text = sb.ToString();
                        nbFutureMetrics.Visible = true; 
                    }
                }
            }
        }

        /// <summary>
        /// Checks the selection.
        /// </summary>
        /// <returns></returns>
        private bool CheckSelection()
        {
            // If campus and schedule have been selected before, assume current weekend
            if ( _selectedCampusId.HasValue && _selectedServiceId.HasValue && !_selectedWeekend.HasValue )
            {
                _selectedWeekend = RockDateTime.Today.SundayDate();
            }

            var options = new List<ServiceMetricSelectItem>();

            if ( !_selectedCampusId.HasValue )
            {
                var campuses = GetCampuses();
                if ( campuses.Count == 0 )
                {
                    pnlSelection.Visible = false;
                    pnlMetrics.Visible = false;
                    pnlUnauthorized.Visible = true;

                    return false;
                }
                else
                {
                    if ( campuses.Count == 1 )
                    {
                        _selectedCampusId = campuses.First().Id;
                    }
                    else
                    {
                        lSelection.Text = "Select Location:";
                        foreach ( var campus in GetCampuses() )
                        {
                            options.Add( new ServiceMetricSelectItem( "Campus", campus.Id.ToString(), campus.Name ) );
                        }
                    }
                }
            }

            if ( !options.Any() && !_selectedServiceId.HasValue )
            {
                lSelection.Text = "Select Service Time:";
                foreach ( var service in GetServices() )
                {
                    options.Add( new ServiceMetricSelectItem( "Service", service.Id.ToString(), service.Name ) );
                }
            }

            if ( options.Any() )
            {
                rptrSelection.DataSource = options;
                rptrSelection.DataBind();

                pnlSelection.Visible = true;
                pnlMetrics.Visible = false;
                pnlUnauthorized.Visible = false;

                return false;
            }
            else
            {
                pnlUnauthorized.Visible = false;
                pnlSelection.Visible = false;
                pnlMetrics.Visible = true;

                return true;
            }
        }

        /// <summary>
        /// Loads the drop downs.
        /// </summary>
        private void LoadDropDowns()
        {
            bddlCampus.Items.Clear();
            bddlWeekend.Items.Clear();
            bddlService.Items.Clear();

            // Load Campuses
            foreach ( var campus in GetCampuses() )
            {
                bddlCampus.Items.Add( new ListItem( campus.Name, campus.Id.ToString() ) );
            }
            bddlCampus.SetValue( _selectedCampusId.Value );

            // Load Weeks   
            var date = RockDateTime.Today.SundayDate();
            bddlWeekend.Items.Add( new ListItem( "Sunday " + date.ToShortDateString(), date.ToString( "o" ) ) );
            bddlWeekend.SetValue( date.ToString( "o" ) );
            lWeekend.Text = String.Format( "Week of {0} - {1}", date.AddDays( -6 ).ToShortDateString(), date.ToShortDateString() );

            var serviceList = GetServices();
            // Load service times
            if ( serviceList.Any() )
            {
                foreach ( var service in serviceList )
                {
                    bddlService.Items.Add( new ListItem( service.Name, service.Id.ToString() ) );
                }

                if ( _selectedServiceId.HasValue )
                {
                    bddlService.SetValue( _selectedServiceId.Value );
                }
            }
            else
            {
                bddlService.Items.Add( new ListItem( "N/A" ) );
                bddlService.SetValue( "N/A" );
            }
        }

        /// <summary>
        /// Gets the campuses.
        /// </summary>
        /// <returns></returns>
        private List<CampusCache> GetCampuses()
        {
            var campuses = new List<CampusCache>();

            if ( UserCanEdit )
            {
                foreach ( var campus in CampusCache.All()
                            .Where( c => c.IsActive.HasValue &&
                                c.IsActive.Value )
                            .OrderBy( c => c.Name ) )
                {
                    campuses.Add( campus );
                }
            }
            else
            {
                var groupGuid = GetAttributeValue( "Group" ).AsGuidOrNull();
                if ( groupGuid != null )
                {
                    var group = new GroupService( new RockContext() ).Get( groupGuid.Value );
                    if ( group != null )
                    {
                        var groupMember = new GroupMemberService( new RockContext() ).Queryable().Where( gm => gm.GroupId == group.Id && gm.PersonId == CurrentPersonId ).FirstOrDefault();
                        if ( groupMember != null )
                        {
                            groupMember.LoadAttributes();
                            var authorizedCampusIds = groupMember.GetAttributeValue( GetAttributeValue( "AuthorizedCampusesAttributeKey" ) ).SplitDelimitedValues().AsGuidList();
                            if ( authorizedCampusIds != null )
                            {
                                foreach ( var campus in CampusCache.All()
                                  .Where( c => c.IsActive.HasValue &&
                                      c.IsActive.Value &&
                                  authorizedCampusIds.Contains( c.Guid ) )
                                  .OrderBy( c => c.Name ) )
                                {
                                    campuses.Add( campus );
                                }
                            }
                        }
                    }
                }
            }

            return campuses;
        }

        /// <summary>
        /// Gets the weekend dates.
        /// </summary>
        /// <returns></returns>
        private List<DateTime> GetWeekendDates( int weeksBack, int weeksAhead )
        {
            var dates = new List<DateTime>();

            // Load Weeks
            var sundayDate = RockDateTime.Today.SundayDate();
            var daysBack = weeksBack * 7;
            var daysAhead = weeksAhead * 7;
            var startDate = sundayDate.AddDays( 0 - daysBack );
            var date = sundayDate.AddDays( daysAhead );
            while ( date >= startDate )
            {
                dates.Add( date );
                date = date.AddDays( -7 );
            }

            return dates;
        }

        /// <summary>
        /// Gets the services.
        /// </summary>
        /// <returns></returns>
        private List<Schedule> GetServices()
        {
            var services = new List<Schedule>();

            if ( _selectedCampusId.HasValue )
            {
                var campus = CampusCache.Get( _selectedCampusId.Value );
                if ( campus != null )
                {
                    using ( var rockContext = new RockContext() )
                    {
                        var scheduleService = new ScheduleService( rockContext );

                        // First check for any holiday schedules. If there are any, only display the holiday schedules.
                        var holidayScheduleCategory = CategoryCache.Get( GetAttributeValue( "HolidayScheduleCategory" ).AsGuid() );
                        if ( holidayScheduleCategory != null )
                        {
                            var holidayCategoryIds = new List<int>();
                            holidayCategoryIds.Add( holidayScheduleCategory.Id );
                            if ( holidayScheduleCategory.Categories.Any() )
                            {
                                holidayCategoryIds.AddRange( holidayScheduleCategory.Categories.Select( c => c.Id ).ToList() );
                            }

                            foreach ( var schedule in GetSchedulesInCategoriesOccurringToday( scheduleService, holidayCategoryIds ) )
                            {
                                services.Add( schedule );
                            }
                        }

                        // If there are no holiday schedules today, then populate schedule list with any event and weekend schedules that occur today
                        if ( !services.Any() )
                        {
                            var categoryIds = new List<int>();

                            // Grab the weekend schedule categories
                            var weekendScheduleCategory = CategoryCache.Get( GetAttributeValue( "WeekendScheduleCategory" ).AsGuid() );
                            if ( weekendScheduleCategory != null )
                            {
                                //If there is a campus-specific schedule category underneath this one, use that instead
                                if ( weekendScheduleCategory.Categories.Where( c => c.Name.Contains( campus.Name ) ).Any() )
                                {
                                    weekendScheduleCategory = weekendScheduleCategory.Categories.Where( c => c.Name.Contains( campus.Name ) ).FirstOrDefault();
                                }

                                categoryIds.Add( weekendScheduleCategory.Id );
                            }

                            // grab any event schedule categories
                            var eventScheduleCategory = CategoryCache.Get( GetAttributeValue( "EventScheduleCategory" ).AsGuid() );
                            if ( eventScheduleCategory != null )
                            {
                                //If there is a campus-specific schedule category underneath this one, use that instead
                                if ( eventScheduleCategory.Categories.Where( c => c.Name.Contains( campus.Name ) ).Any() )
                                {
                                    eventScheduleCategory = eventScheduleCategory.Categories.Where( c => c.Name.Contains( campus.Name ) ).FirstOrDefault();
                                }

                                categoryIds.Add( eventScheduleCategory.Id );
                            }

                            // Grab any schedules occurring today that are in the provided categories
                            foreach ( var schedule in GetSchedulesInCategoriesOccurringToday( scheduleService, categoryIds ) )
                            {
                                services.Add( schedule );
                            }
                        }
                    }

                    // Sort the services by the raw iCal start time
                    services = services.Distinct().OrderBy( s => s.GetCalenderEvent().DTStart.TimeOfDay ).ToList();
                }
            }

            return services;
        }

        /// <summary>
        /// Gets the schedules in categories occurring today.
        /// </summary>
        /// <param name="scheduleService">The schedule service.</param>
        /// <param name="categoryIds">The category ids.</param>
        /// <returns></returns>
        private static List<Schedule> GetSchedulesInCategoriesOccurringToday( ScheduleService scheduleService, List<int> categoryIds )
        {
            var schedules = new List<Schedule>();

            foreach ( var schedule in scheduleService.Queryable().AsNoTracking()
                .Where( s =>
                    s.CategoryId.HasValue &&
                    s.IsActive &&
                    categoryIds.Contains( s.CategoryId.Value ) )
                .ToList() ) // We ToList() this query so that we can use the NextStartDate property 
            {
                var nextStartDate = schedule.GetNextStartDateTime( RockDateTime.Now );
                if (
                    /* 
                        * Here we grab schedules if
                        *  1) Their EffectiveStartDate (First time they occur) is the same as today's date. This
                        *      is used for non-reccurring schedules such as holiday schedules or one-off events.
                        *  2) Their NextStartDateTime DayOfWeek matches today's day of week. This is used for reccurring
                        *      schedules such as weekend schedules or Trek
                        */
                    ( schedule.EffectiveStartDate.HasValue && schedule.EffectiveStartDate.Value.Date == RockDateTime.Now.Date ) ||
                    ( nextStartDate.HasValue && nextStartDate.Value.DayOfWeek == RockDateTime.Now.DayOfWeek ) )
                {
                    schedules.Add( schedule );
                }
            }

            return schedules;
        }

        /// <summary>
        /// Binds the metrics.
        /// </summary>
        private void BindMetrics()
        {
            var serviceMetricValues = new List<ServiceMetric>();

            int campusEntityTypeId = EntityTypeCache.Get( typeof( Rock.Model.Campus ) ).Id;
            int scheduleEntityTypeId = EntityTypeCache.Get( typeof( Rock.Model.Schedule ) ).Id;

            int? campusId = bddlCampus.SelectedValueAsInt();
            int? scheduleId = bddlService.SelectedValueAsInt();
            DateTime? weekend = bddlWeekend.SelectedValue.AsDateTime();

            var notes = new List<string>();

            if ( campusId.HasValue && scheduleId.HasValue && weekend.HasValue )
            {

                SetBlockUserPreference( "CampusId", campusId.HasValue ? campusId.Value.ToString() : "" );
                SetBlockUserPreference( "ScheduleId", scheduleId.HasValue ? scheduleId.Value.ToString() : "" );

                var metricCategories = MetricCategoriesFieldAttribute.GetValueAsGuidPairs( GetAttributeValue( "MetricCategories" ) );
                var metricGuids = metricCategories.Select( a => a.MetricGuid ).ToList();
                using ( var rockContext = new RockContext() )
                {
                    var metricValueService = new MetricValueService( rockContext );
                    foreach ( var metric in new MetricService( rockContext )
                        .GetByGuids( metricGuids )
                        .Where( m =>
                            m.MetricPartitions.Count == 2 &&
                            m.MetricPartitions.Any( p => p.EntityTypeId.HasValue && p.EntityTypeId.Value == campusEntityTypeId ) &&
                            m.MetricPartitions.Any( p => p.EntityTypeId.HasValue && p.EntityTypeId.Value == scheduleEntityTypeId ) )
                        .OrderBy( m => m.Title )
                        .Select( m => new
                        {
                            m.Id,
                            m.Title,
                            CampusPartitionId = m.MetricPartitions.Where( p => p.EntityTypeId.HasValue && p.EntityTypeId.Value == campusEntityTypeId ).Select( p => p.Id ).FirstOrDefault(),
                            SchedulePartitionId = m.MetricPartitions.Where( p => p.EntityTypeId.HasValue && p.EntityTypeId.Value == scheduleEntityTypeId ).Select( p => p.Id ).FirstOrDefault(),
                        } ) )
                    {
                        var serviceMetric = new ServiceMetric( metric.Id, metric.Title );

                        if ( campusId.HasValue && weekend.HasValue && scheduleId.HasValue )
                        {
                            var metricValue = metricValueService
                                .Queryable().AsNoTracking()
                                .Where( v =>
                                    v.MetricId == metric.Id &&
                                    v.MetricValueDateTime.HasValue && v.MetricValueDateTime.Value == weekend.Value &&
                                    v.MetricValuePartitions.Count == 2 &&
                                    v.MetricValuePartitions.Any( p => p.MetricPartitionId == metric.CampusPartitionId && p.EntityId.HasValue && p.EntityId.Value == campusId.Value ) &&
                                    v.MetricValuePartitions.Any( p => p.MetricPartitionId == metric.SchedulePartitionId && p.EntityId.HasValue && p.EntityId.Value == scheduleId.Value ) )
                                .FirstOrDefault();

                            if ( metricValue != null )
                            {
                                serviceMetric.Value = ( int? ) metricValue.YValue;
                                serviceMetric.ModifiedDateTime = metricValue.ModifiedDateTime;

                                if ( !string.IsNullOrWhiteSpace( metricValue.Note ) &&
                                    !notes.Contains( metricValue.Note ) )
                                {
                                    notes.Add( metricValue.Note );
                                }

                            }
                        }

                        serviceMetricValues.Add( serviceMetric );
                    }
                }
            }

            rptrMetric.DataSource = serviceMetricValues;
            rptrMetric.DataBind();

            tbNote.Text = notes.AsDelimited( Environment.NewLine + Environment.NewLine );

            if ( UserCanEdit )
            {
                tbNote.Visible = true;
            }
            else
            {
                var groupGuid = GetAttributeValue( "Group" ).AsGuidOrNull();
                if ( groupGuid != null )
                {
                    var group = new GroupService( new RockContext() ).Get( groupGuid.Value );
                    if ( group != null )
                    {
                        var groupMember = new GroupMemberService( new RockContext() ).Queryable().Where( gm => gm.GroupId == group.Id && gm.PersonId == CurrentPersonId ).FirstOrDefault();
                        if ( groupMember != null )
                        {
                            groupMember.LoadAttributes();
                            bool canSeeNotes = groupMember.GetAttributeValue( GetAttributeValue( "NotesVisibleAttributeKey" ) ).AsBoolean();
                            tbNote.Visible = canSeeNotes;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Verifies that the metric values can be saved based on the metric entry blacklist.
        /// </summary>
        private bool MetricValuesValid()
        {
            bool noConflicts = true;

            var metricEntryBlacklist = GetAttributeValue( "MetricEntryBlacklist" ).AsDictionaryOrNull();
            if ( metricEntryBlacklist != null )
            {
                List<ServiceMetricItem> metricItems = new List<ServiceMetricItem>();

                // get a list of entered metric values
                foreach ( RepeaterItem item in rptrMetric.Items )
                {
                    var hfMetricIId = item.FindControl( "hfMetricId" ) as HiddenField;
                    var lMetricTitle = item.FindControl( "lMetricTitle" ) as Label;
                    var nbMetricValue = item.FindControl( "nbMetricValue" ) as NumberBox;

                    if ( nbMetricValue.Text.AsDecimalOrNull().HasValue && hfMetricIId.ValueAsInt() > 0 )
                    {
                        var metricItem = new ServiceMetricItem();
                        metricItem.MetricId = hfMetricIId.ValueAsInt();
                        metricItem.MetricTitle = lMetricTitle.Text;
                        metricItem.MetricValue = nbMetricValue.Text.AsDecimal();

                        metricItems.Add( metricItem );
                    }                 
                }

                StringBuilder sb = new StringBuilder();
                sb.AppendLine( "The following Metrics can't be saved together.  Please only save one or the other:<br/>" );

                // check to see if there are any conflicts
                foreach ( var blacklistItem in metricEntryBlacklist )
                {
                    var metrics = metricItems.Where( i => i.MetricId == blacklistItem.Key.AsInteger() || i.MetricId == blacklistItem.Value.AsInteger() );

                    if ( metrics.Count() > 1)
                    {
                        noConflicts = false;

                        sb.AppendLine( metrics.Select( m => m.MetricTitle ).ToList().AsDelimited( ","," & " ) + "<br/>" );
                    }
                }

                string customMessage = GetAttributeValue( "BlacklistCustomMessage" );
                if ( customMessage.IsNotNullOrWhiteSpace() )
                {
                    nbMetricErrors.Text = customMessage;
                }
                else
                {
                    nbMetricErrors.Text = sb.ToString();
                }
            }
           
            return noConflicts;
        }

        #endregion
    }

    /// <summary>
    /// Helper class for checking metric ids and values.
    /// </summary>
    public class ServiceMetricItem
    {
        public int MetricId { get; set; }
        public string MetricTitle { get; set; }
        public decimal MetricValue { get; set; }
    }

    /// <summary>
    /// Helper class to display campus and service options
    /// </summary>
    public class ServiceMetricSelectItem
    {
        public string CommandName { get; set; }
        public string CommandArg { get; set; }
        public string OptionText { get; set; }
        public ServiceMetricSelectItem( string commandName, string commandArg, string optionText )
        {
            CommandName = commandName;
            CommandArg = commandArg;
            OptionText = optionText;
        }
    }

    /// <summary>
    /// Helper class for displaying and saving metrics
    /// </summary>
    public class ServiceMetric
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int? Value { get; set; }
        public DateTime? ModifiedDateTime { get; set; }

        public ServiceMetric( int id, string name )
        {
            Id = id;
            Name = name;
        }
    }
}