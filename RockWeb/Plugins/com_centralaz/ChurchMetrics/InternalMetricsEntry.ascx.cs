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
using System.Data.Entity;

namespace RockWeb.Plugins.com_centralaz.ChurchMetrics
{
    /// <summary>
    /// Block for easily adding/editing metric values for any metric that has partitions of campus and service time.
    /// </summary>
    [DisplayName( "Internal Metrics Entry" )]
    [Category( "com_centralaz > ChurchMetrics" )]
    [Description( "Block for easily adding/editing metric values for any metric that has partitions of campus and service time." )]

    // Metric Categories
    [MetricCategoriesField( "Metric Categories", "Select the metric categories to display (note: only metrics in those categories with a campus and schedule partition will displayed).", true, "", "Metric Categories", 0 )]

    // Weekend Settings
    [IntegerField( "Weeks Back", "The number of weeks back to display in the 'Week of' selection.", false, 8, "Weekend Settings", 1 )]
    [IntegerField( "Weeks Ahead", "The number of weeks ahead to display in the 'Week of' selection.", false, 0, "Weekend Settings", 2 )]

    // Schedule Categories
    [CategoryField( "Holiday Schedule Category", "The schedule category to use for list of holiday service times. If this category has child categories, Rock will use those too.", false, "Rock.Model.Schedule", "", "", false, "", "Schedule Categories", 3 )]
    [CategoryField( "Weekend Schedule Category", "The schedule category to use for list of service times. If this category has child categories, Rock will search for one that contains the name of the currently selected campus. Otherwise, Rock will use this one.", false, "Rock.Model.Schedule", "", "", false, "", "Schedule Categories", 4 )]
    [CategoryField( "Event Schedule Category", "The schedule category to use for list of event times. If this category has child categories, Rock will search for one that contains the name of the currently selected campus. Otherwise, Rock will use this one.", false, "Rock.Model.Schedule", "", "", false, "", "Schedule Categories", 5 )]

    // Weekend Settings
    [IntegerField( "Weeks Back", "The number of weeks back to display in the 'Week of' selection.", false, 8, "", 6 )]
    [IntegerField( "Weeks Ahead", "The number of weeks ahead to display in the 'Week of' selection.", false, 0, "", 7 )]

    public partial class InternalMetricsEntry : Rock.Web.UI.RockBlock
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
            int campusEntityTypeId = EntityTypeCache.Get( typeof( Rock.Model.Campus ) ).Id;
            int scheduleEntityTypeId = EntityTypeCache.Get( typeof( Rock.Model.Schedule ) ).Id;

            int? campusId = bddlCampus.SelectedValueAsInt();
            int? scheduleId = bddlService.SelectedValueAsInt();
            DateTime? weekend = bddlWeekend.SelectedValue.AsDateTime();

            if ( campusId.HasValue && scheduleId.HasValue && weekend.HasValue )
            {
                using ( var rockContext = new RockContext() )
                {
                    var metricService = new MetricService( rockContext );
                    var metricValueService = new MetricValueService( rockContext );

                    foreach ( RepeaterItem item in rptrMetric.Items )
                    {
                        var hfMetricIId = item.FindControl( "hfMetricId" ) as HiddenField;
                        var nbMetricValue = item.FindControl( "nbMetricValue" ) as NumberBox;

                        if ( hfMetricIId != null && nbMetricValue != null )
                        {
                            int metricId = hfMetricIId.ValueAsInt();
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
                                }

                                metricValue.YValue = nbMetricValue.Text.AsDecimalOrNull();
                                metricValue.Note = tbNote.Text;
                            }
                        }
                    }

                    rockContext.SaveChanges();
                }

                nbMetricsSaved.Text = string.Format( "Your metrics for the {0} service on {1} at the {2} Campus have been saved.",
                    bddlService.SelectedItem.Text, bddlWeekend.SelectedItem.Text, bddlCampus.SelectedItem.Text );
                nbMetricsSaved.Visible = true;

                BindMetrics();

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

            BindMetrics();
        }

        #endregion

        #region Methods

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

            var options = new List<InternalServiceMetricSelectItem>();

            if ( !_selectedCampusId.HasValue )
            {
                lSelection.Text = "Select Location:";
                foreach ( var campus in GetCampuses() )
                {
                    options.Add( new InternalServiceMetricSelectItem( "Campus", campus.Id.ToString(), campus.Name ) );
                }
            }

            if ( !options.Any() && !_selectedWeekend.HasValue )
            {
                lSelection.Text = "Select Week of:";
                foreach ( var weekend in GetWeekendDates( 1, 0 ) )
                {
                    options.Add( new InternalServiceMetricSelectItem( "Weekend", weekend.ToString( "o" ), "Sunday " + weekend.ToShortDateString() ) );
                }
            }

            if ( !options.Any() && !_selectedServiceId.HasValue )
            {
                lSelection.Text = "Select Service Time:";
                foreach ( var service in GetServices() )
                {
                    options.Add( new InternalServiceMetricSelectItem( "Service", service.Id.ToString(), service.Name ) );
                }
            }

            if ( options.Any() )
            {
                rptrSelection.DataSource = options;
                rptrSelection.DataBind();

                pnlSelection.Visible = true;
                pnlMetrics.Visible = false;

                return false;
            }
            else
            {
                pnlSelection.Visible = false;
                pnlMetrics.Visible = true;

                return true;
            }
        }

        /// <summary>
        /// Builds the campus selection.
        /// </summary>
        private void BuildCampusSelection()
        {
            foreach ( var campus in CampusCache.All()
                .Where( c => c.IsActive.HasValue && c.IsActive.Value )
                .OrderBy( c => c.Name ) )
            {
                bddlCampus.Items.Add( new ListItem( campus.Name, campus.Id.ToString() ) );
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
            var weeksBack = GetAttributeValue( "WeeksBack" ).AsInteger();
            var weeksAhead = GetAttributeValue( "WeeksAhead" ).AsInteger();
            foreach ( var date in GetWeekendDates( weeksBack, weeksAhead ) )
            {
                bddlWeekend.Items.Add( new ListItem( "Sunday " + date.ToShortDateString(), date.ToString( "o" ) ) );
            }
            bddlWeekend.SetValue( _selectedWeekend.Value.ToString( "o" ) );

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

            foreach ( var campus in CampusCache.All()
                .Where( c => c.IsActive.HasValue && c.IsActive.Value )
                .OrderBy( c => c.Name ) )
            {
                campuses.Add( campus );
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
            var scheduleSummaryList = new List<ScheduleSummary>();

            if ( _selectedCampusId.HasValue )
            {
                var campus = CampusCache.Get( _selectedCampusId.Value );
                if ( campus != null )
                {
                    using ( var rockContext = new RockContext() )
                    {
                        var scheduleService = new ScheduleService( rockContext );

                        // Grab the weekend schedule categories
                        var weekendScheduleCategory = CategoryCache.Get( GetAttributeValue( "WeekendScheduleCategory" ).AsGuid() );
                        if ( weekendScheduleCategory != null )
                        {
                            //If there is a campus-specific schedule category underneath this one, use that instead
                            if ( weekendScheduleCategory.Categories.Where( c => c.Name.Contains( campus.Name ) ).Any() )
                            {
                                weekendScheduleCategory = weekendScheduleCategory.Categories.Where( c => c.Name.Contains( campus.Name ) ).FirstOrDefault();
                            }

                            foreach ( var schedule in GetSchedulesInCategory( scheduleService, weekendScheduleCategory.Id ) )
                            {
                                var nextStartDateTime = schedule.GetNextStartDateTime( RockDateTime.Now );
                                scheduleSummaryList.Add( new ScheduleSummary()
                                {
                                    Schedule = schedule,
                                    Date = nextStartDateTime != null ? nextStartDateTime.Value.Date : schedule.EffectiveStartDate.Value.Date,
                                    Time = nextStartDateTime != null ? nextStartDateTime.Value.TimeOfDay : schedule.EffectiveStartDate.Value.TimeOfDay,
                                    ScheduleType = 0
                                } );
                            }
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

                            foreach ( var schedule in GetSchedulesInCategory( scheduleService, eventScheduleCategory.Id ) )
                            {
                                var nextStartDateTime = schedule.GetNextStartDateTime( RockDateTime.Now );
                                scheduleSummaryList.Add( new ScheduleSummary()
                                {
                                    Schedule = schedule,
                                    Date = nextStartDateTime != null ? nextStartDateTime.Value.Date : schedule.EffectiveStartDate.Value.Date,
                                    Time = nextStartDateTime != null ? nextStartDateTime.Value.TimeOfDay : schedule.EffectiveStartDate.Value.TimeOfDay,
                                    ScheduleType = 1
                                } );
                            }
                        }

                        // Grab any holiday schedules that have occurred in the last 5 days
                        var holidayScheduleCategory = CategoryCache.Get( GetAttributeValue( "HolidayScheduleCategory" ).AsGuid() );
                        if ( holidayScheduleCategory != null )
                        {
                            var holidayCategoryIds = new List<int>();
                            holidayCategoryIds.Add( holidayScheduleCategory.Id );
                            if ( holidayScheduleCategory.Categories.Any() )
                            {
                                holidayCategoryIds.AddRange( holidayScheduleCategory.Categories.Select( c => c.Id ).ToList() );
                            }

                            foreach ( var schedule in GetSchedulesInCategories( scheduleService, holidayCategoryIds )
                                .Where( s =>
                                     s.EffectiveStartDate.HasValue &&
                                     s.EffectiveStartDate.Value.Date <= RockDateTime.Now.Date &&
                                     s.EffectiveStartDate.Value.Date >= RockDateTime.Now.Date.AddDays( -5 ) )
                                .ToList() )
                            {
                                var nextStartDateTime = schedule.GetNextStartDateTime( RockDateTime.Now );
                                scheduleSummaryList.Add( new ScheduleSummary()
                                {
                                    Schedule = schedule,
                                    Date = nextStartDateTime != null ? nextStartDateTime.Value.Date : schedule.EffectiveStartDate.Value.Date,
                                    Time = nextStartDateTime != null ? nextStartDateTime.Value.TimeOfDay : schedule.EffectiveStartDate.Value.TimeOfDay,
                                    ScheduleType = 2
                                } );
                            }
                        }
                    }

                    scheduleSummaryList = scheduleSummaryList.Distinct().OrderBy( s => s.ScheduleType )
                        .ThenByDescending( s => s.Date.SundayDate() - s.Date )
                        .ThenBy( s => s.Time ).ToList();

                }
            }

            return scheduleSummaryList.Select( s => s.Schedule ).ToList();
        }

        /// <summary>
        /// Gets the schedules in category.
        /// </summary>
        /// <param name="scheduleService">The schedule service.</param>
        /// <param name="categoryId">The category identifier.</param>
        /// <returns></returns>
        private static List<Schedule> GetSchedulesInCategory( ScheduleService scheduleService, int categoryId )
        {
            var categoryIds = new List<int>();
            categoryIds.Add( categoryId );
            return GetSchedulesInCategories( scheduleService, categoryIds );
        }

        /// <summary>
        /// Gets the schedules in categories
        /// </summary>
        /// <param name="scheduleService">The schedule service.</param>
        /// <param name="categoryIds">The category ids.</param>
        /// <returns></returns>
        private static List<Schedule> GetSchedulesInCategories( ScheduleService scheduleService, List<int> categoryIds )
        {
            var schedules = scheduleService
                                .Queryable().AsNoTracking()
                                .Where( s =>
                                    s.CategoryId.HasValue &&
                                    s.IsActive &&
                                    categoryIds.Contains( s.CategoryId.Value ) )
                                .ToList();
            return schedules;
        }

        /// <summary>
        /// Binds the metrics.
        /// </summary>
        private void BindMetrics()
        {
            var serviceMetricValues = new List<InternalServiceMetric>();

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
                        var serviceMetric = new InternalServiceMetric( metric.Id, metric.Title );

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
        }

        #endregion

    }

    /// <summary>
    /// Helper class to display campus and service options
    /// </summary>
    public class InternalServiceMetricSelectItem
    {
        public string CommandName { get; set; }
        public string CommandArg { get; set; }
        public string OptionText { get; set; }
        public InternalServiceMetricSelectItem( string commandName, string commandArg, string optionText )
        {
            CommandName = commandName;
            CommandArg = commandArg;
            OptionText = optionText;
        }
    }

    /// <summary>
    /// Helper class for displaying and saving metrics
    /// </summary>
    public class InternalServiceMetric
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int? Value { get; set; }

        public InternalServiceMetric( int id, string name )
        {
            Id = id;
            Name = name;
        }
    }

    /// <summary>
    /// Helper class for storing information about schedules
    /// </summary>
    public class ScheduleSummary
    {
        public Schedule Schedule { get; set; }

        public DateTime Date { get; set; }

        public TimeSpan Time { get; set; }

        public int ScheduleType { get; set; }
    }
}