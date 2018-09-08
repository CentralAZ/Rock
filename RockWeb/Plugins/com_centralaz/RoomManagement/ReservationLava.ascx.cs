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
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
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
using Rock.Store;
using System.Text;
using Rock.Security;

using com.centralaz.RoomManagement.Model;
using com.centralaz.RoomManagement.Web.Cache;
using com.centralaz.RoomManagement.Attribute;
using iTextSharp.text;
using iTextSharp.text.pdf;
using iTextSharp.text.html.simpleparser;
using com.centralaz.RoomManagement.ReportTemplates;

namespace RockWeb.Plugins.com_centralaz.RoomManagement
{
    [DisplayName( "Reservation Lava" )]
    [Category( "com_centralaz > Room Management" )]
    [Description( "Renders a list of reservations in lava." )]

    [CustomRadioListField( "Location Filter Display Mode", "", "1^Hidden, 2^Plain, 3^Panel Open, 4^Panel Closed", true, "1", order: 1, category: "Filter Settings" )]
    [CustomRadioListField( "Resource Filter Display Mode", "", "1^Hidden, 2^Plain, 3^Panel Open, 4^Panel Closed", true, "1", order: 2, category: "Filter Settings" )]
    [CustomRadioListField( "Campus Filter Display Mode", "", "1^Hidden, 2^Plain, 3^Panel Open, 4^Panel Closed", true, "1", order: 3, category: "Filter Settings" )]
    [CustomRadioListField( "Ministry Filter Display Mode", "", "1^Hidden, 2^Plain, 3^Panel Open, 4^Panel Closed", true, "1", key: "MinistryFilterDisplayMode", order: 4, category: "Filter Settings" )]
    [CustomRadioListField( "Approval Filter Display Mode", "", "1^Hidden, 2^Plain, 3^Panel Open, 4^Panel Closed", true, "1", key: "ApprovalFilterDisplayMode", order: 5, category: "Filter Settings" )]
    [CustomRadioListField( "Reservation Type Filter Display Mode", "", "1^Hidden, 2^Plain, 3^Panel Open, 4^Panel Closed", true, "1", key: "ReservationTypeFilterDisplayMode", order: 6, category: "Filter Settings" )]
    [BooleanField( "Show Date Range Filter", "Determines whether the date range filters are shown", false, order: 7, category: "Filter Settings" )]

    [LinkedPage( "Details Page", "Detail page for events", order: 8, category: "Lava Settings" )]
    [CodeEditorField( "Lava Template", "Lava template to use to display the list of reservations.", CodeEditorMode.Lava, CodeEditorTheme.Rock, 400, true, @"{% include '~/Plugins/com_centralaz/RoomManagement/Assets/Lava/Reservation.lava' %}", "Lava Settings", 9 )]
    [BooleanField( "Enable Debug", "Display a list of merge fields available for lava.", false, "Lava Settings", 10 )]

    [TextField( "Report Font", "", true, "Gotham", "Report Settings", 11 )]
    [TextField( "Report Logo", "URL to the logo (PNG) to display in the printed report.", true, "~/Plugins/com_centralaz/RoomManagement/Assets/Icons/Central_Logo_Black_rgb_165_90.png", "Report Settings", 12 )]
    [ReportTemplateField( "Report Template", "The template for the printed report. The Default and Advanced Templates will generate a printed report based on the templates' hardcoded layout. The Lava Template will generate a report based on the lava provided below in the Report Lava Setting. Any other custom templates will format based on their developer's documentation.", true, "9b74314a-37e0-40f2-906c-2862c93f8888", "Report Settings", 13 )]
    [CodeEditorField( "Report Lava", "If the Lava Template is selected, this is the lava that will be used in the report", CodeEditorMode.Lava, CodeEditorTheme.Rock, 400, true, @"{% include '~/Plugins/com_centralaz/RoomManagement/Assets/Lava/ReservationReport.lava' %}", "Report Settings", 14 )]

    [CustomDropdownListField( "Default View Option", "Determines the default view option", "Day,Week,Month", true, "Week", order: 15, category: "View Settings" )]
    [DayOfWeekField( "Start of Week Day", "Determines what day is the start of a week.", true, DayOfWeek.Sunday, order: 16, category: "View Settings" )]
    [BooleanField( "Show Small Calendar", "Determines whether the calendar widget is shown", true, order: 17, category: "View Settings" )]
    [BooleanField( "Show Day View", "Determines whether the day view option is shown", false, order: 18, category: "View Settings" )]
    [BooleanField( "Show Week View", "Determines whether the week view option is shown", true, order: 19, category: "View Settings" )]
    [BooleanField( "Show Month View", "Determines whether the month view option is shown", true, order: 20, category: "View Settings" )]

    public partial class ReservationLava : Rock.Web.UI.RockBlock
    {
        #region Fields

        private DayOfWeek _firstDayOfWeek = DayOfWeek.Sunday;

        protected bool LocationPanelOpen { get; set; }
        protected bool LocationPanelClosed { get; set; }
        protected bool ResourcePanelOpen { get; set; }
        protected bool ResourcePanelClosed { get; set; }
        protected bool CampusPanelOpen { get; set; }
        protected bool CampusPanelClosed { get; set; }
        protected bool MinistryPanelOpen { get; set; }
        protected bool MinistryPanelClosed { get; set; }
        protected bool ApprovalPanelOpen { get; set; }
        protected bool ApprovalPanelClosed { get; set; }
        protected bool ReservationTypePanelOpen { get; set; }
        protected bool ReservationTypePanelClosed { get; set; }

        #endregion

        #region Properties

        private String ViewMode { get; set; }
        private DateTime? FilterStartDate { get; set; }
        private DateTime? FilterEndDate { get; set; }
        private List<DateTime> ReservationDates { get; set; }

        #endregion

        #region Base ControlMethods

        /// <summary>
        /// Restores the view-state information from a previous user control request that was saved by the <see cref="M:System.Web.UI.UserControl.SaveViewState" /> method.
        /// </summary>
        /// <param name="savedState">An <see cref="T:System.Object" /> that represents the user control state to be restored.</param>
        protected override void LoadViewState( object savedState )
        {
            base.LoadViewState( savedState );

            ViewMode = ViewState["ViewMode"] as String;
            FilterStartDate = ViewState["FilterStartDate"] as DateTime?;
            FilterEndDate = ViewState["FilterEndDate"] as DateTime?;
            ReservationDates = ViewState["ReservationDates"] as List<DateTime>;
        }

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Init" /> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnInit( EventArgs e )
        {
            base.OnInit( e );

            _firstDayOfWeek = GetAttributeValue( "StartofWeekDay" ).ConvertToEnum<DayOfWeek>();

            lVersionText.Text = com.centralaz.RoomManagement.VersionInfo.GetPluginProductVersionNumber();

            LocationPanelOpen = GetAttributeValue( "LocationFilterDisplayMode" ) == "3";
            LocationPanelClosed = GetAttributeValue( "LocationFilterDisplayMode" ) == "4";

            ResourcePanelOpen = GetAttributeValue( "ResourceFilterDisplayMode" ) == "3";
            ResourcePanelClosed = GetAttributeValue( "ResourceFilterDisplayMode" ) == "4";

            CampusPanelOpen = GetAttributeValue( "CampusFilterDisplayMode" ) == "3";
            CampusPanelClosed = GetAttributeValue( "CampusFilterDisplayMode" ) == "4";

            MinistryPanelOpen = GetAttributeValue( "MinistryFilterDisplayMode" ) == "3";
            MinistryPanelClosed = GetAttributeValue( "MinistryFilterDisplayMode" ) == "4";

            ApprovalPanelOpen = GetAttributeValue( "ApprovalFilterDisplayMode" ) == "3";
            ApprovalPanelClosed = GetAttributeValue( "ApprovalFilterDisplayMode" ) == "4";

            ReservationTypePanelOpen = GetAttributeValue( "ReservationTypeFilterDisplayMode" ) == "3";
            ReservationTypePanelClosed = GetAttributeValue( "ReservationTypeFilterDisplayMode" ) == "4";

            // this event gets fired after block settings are updated. it's nice to repaint the screen if these settings would alter it
            this.BlockUpdated += Block_BlockUpdated;
            this.AddConfigurationUpdateTrigger( upnlContent );

            // register lbPrint as a PostBackControl since it is returning a File download
            ScriptManager scriptManager = ScriptManager.GetCurrent( Page );
            scriptManager.RegisterPostBackControl( lbPrint );

            RockPage.AddScriptLink( "~/Plugins/com_centralaz/RoomManagement/Assets/Scripts/circle-progress.js", fingerprint: false );
        }

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Load" /> event.
        /// </summary>
        /// <param name="e">The <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );

            nbMessage.Visible = false;

            if ( !Page.IsPostBack )
            {
                if ( SetFilterControls() )
                {
                    pnlDetails.Visible = true;
                    BindData();
                }
                else
                {
                    pnlDetails.Visible = false;
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
            ViewState["ViewMode"] = ViewMode;
            ViewState["FilterStartDate"] = FilterStartDate;
            ViewState["FilterEndDate"] = FilterEndDate;
            ViewState["ReservationDates"] = ReservationDates;

            return base.SaveViewState();
        }

        protected override void OnPreRender( EventArgs e )
        {
            btnDay.CssClass = "btn btn-default" + ( ViewMode == "Day" ? " active" : "" );
            btnWeek.CssClass = "btn btn-default" + ( ViewMode == "Week" ? " active" : "" );
            btnMonth.CssClass = "btn btn-default" + ( ViewMode == "Month" ? " active" : "" );

            base.OnPreRender( e );
        }

        /// <summary>
        /// Handles the BlockUpdated event of the control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void Block_BlockUpdated( object sender, EventArgs e )
        {
            if ( SetFilterControls() )
            {
                pnlDetails.Visible = true;
                BindData();
            }
            else
            {
                pnlDetails.Visible = false;
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// Handles the SelectionChanged event of the calReservationCalendar control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void calReservationCalendar_SelectionChanged( object sender, EventArgs e )
        {
            ResetCalendarSelection();
            BindData();
        }

        /// <summary>
        /// Handles the DayRender event of the calReservationCalendar control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="DayRenderEventArgs"/> instance containing the event data.</param>
        protected void calReservationCalendar_DayRender( object sender, DayRenderEventArgs e )
        {
            DateTime day = e.Day.Date;
            if ( ReservationDates != null && ReservationDates.Any( d => d.Date.Equals( day.Date ) ) )
            {
                e.Cell.AddCssClass( "calendar-hasevent" );
            }
        }

        /// <summary>
        /// Handles the VisibleMonthChanged event of the calReservationCalendar control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MonthChangedEventArgs"/> instance containing the event data.</param>
        protected void calReservationCalendar_VisibleMonthChanged( object sender, MonthChangedEventArgs e )
        {
            calReservationCalendar.SelectedDate = e.NewDate;
            Session["CalendarVisibleDate"] = e.NewDate;
            ResetCalendarSelection();
            BindData();
        }

        protected void lipLocation_SelectItem( object sender, EventArgs e )
        {
            this.SetUserPreference( "Locations", lipLocation.SelectedValues.AsIntegerList().AsDelimited( "," ) );
            BindData();
        }

        protected void rpResource_SelectItem( object sender, EventArgs e )
        {
            this.SetUserPreference( "Resources", rpResource.SelectedValues.AsIntegerList().AsDelimited( "," ) );
            BindData();
        }

        /// <summary>
        /// Handles the SelectedIndexChanged event of the cblCampus control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void cblCampus_SelectedIndexChanged( object sender, EventArgs e )
        {
            this.SetUserPreference( "Campuses", cblCampus.Items.OfType<System.Web.UI.WebControls.ListItem>().Where( l => l.Selected ).Select( a => a.Value.AsInteger() ).ToList().AsDelimited( "," ) );
            BindData();
        }

        /// <summary>
        /// Handles the SelectedIndexChanged event of the cblMinistry control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void cblMinistry_SelectedIndexChanged( object sender, EventArgs e )
        {
            this.SetUserPreference( "Ministries", cblMinistry.Items.OfType<System.Web.UI.WebControls.ListItem>().Where( l => l.Selected ).Select( a => a.Value.AsInteger() ).ToList().AsDelimited( "," ) );
            BindData();
        }

        /// <summary>
        /// Handles the SelectedIndexChanged event of the cblApproval control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void cblApproval_SelectedIndexChanged( object sender, EventArgs e )
        {
            this.SetUserPreference( "Approval State", cblApproval.Items.OfType<System.Web.UI.WebControls.ListItem>().Where( l => l.Selected ).Select( a => a.Value.ConvertToEnum<ReservationApprovalState>().ConvertToInt() ).ToList().AsDelimited( "," ) );
            BindData();
        }

        /// <summary>
        /// Handles the SelectedIndexChanged event of the cblReservationType control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void cblReservationType_SelectedIndexChanged( object sender, EventArgs e )
        {
            this.SetUserPreference( "Reservation Type", cblReservationType.Items.OfType<System.Web.UI.WebControls.ListItem>().Where( l => l.Selected ).Select( a => a.Value.AsInteger() ).ToList().AsDelimited( "," ) );
            BindData();
        }

        protected void dpStartDate_TextChanged( object sender, EventArgs e )
        {
            FilterStartDate = dpStartDate.SelectedDate;
            BindData();
        }

        protected void dpEndDate_TextChanged( object sender, EventArgs e )
        {
            FilterEndDate = dpEndDate.SelectedDate;
            BindData();
        }

        protected void btnViewMode_Click( object sender, EventArgs e )
        {
            var btnViewMode = sender as BootstrapButton;
            if ( btnViewMode != null )
            {
                this.SetUserPreference( "ViewMode", btnViewMode.Text );
                ViewMode = btnViewMode.Text;
                ResetCalendarSelection();
                BindData();
            }
        }

        protected void lbPrint_Click( object sender, EventArgs e )
        {
            List<ReservationService.ReservationSummary> reservationSummaryList = GetReservationSummaries();

            string logoFileUrl = GetAttributeValue( "ReportLogo" );
            if ( !logoFileUrl.ToLower().StartsWith( "http" ) )
            {
                logoFileUrl = Server.MapPath( ResolveRockUrl( logoFileUrl ) );
            }

            var reportTemplate = GetReportTemplate( GetAttributeValue( "ReportTemplate" ).AsGuidOrNull() );

            var outputArray = reportTemplate.GenerateReport( reservationSummaryList, logoFileUrl, GetAttributeValue( "ReportFont" ), FilterStartDate, FilterEndDate, GetAttributeValue( "ReportLava" ) );

            Response.ClearHeaders();
            Response.ClearContent();
            Response.Clear();
            Response.ContentType = "application/pdf";
            Response.AddHeader( "Content-Disposition", string.Format( "attachment;filename=Reservation Schedule for {0} - {1}.pdf", FilterStartDate.Value.ToString( "MMMM d" ), FilterEndDate.Value.ToString( "MMMM d" ) ) );
            Response.BinaryWrite( outputArray );
            Response.Flush();
            Response.End();
            return;
        }

        #endregion

        #region Methods

        private void BindData()
        {
            List<ReservationService.ReservationSummary> reservationSummaryList = GetReservationSummaries();

            // Bind to Grid
            var reservationSummaries = reservationSummaryList.Select( r => new
            {
                Id = r.Id,
                ReservationName = r.ReservationName,
                ReservationType = r.ReservationType,
                ApprovalState = r.ApprovalState.ConvertToString(),
                Locations = r.ReservationLocations.ToList(),
                Resources = r.ReservationResources.ToList(),
                CalendarDate = r.EventStartDateTime.ToLongDateString(),
                EventStartDateTime = r.EventStartDateTime,
                EventEndDateTime = r.EventEndDateTime,
                ReservationStartDateTime = r.ReservationStartDateTime,
                ReservationEndDateTime = r.ReservationEndDateTime,
                EventDateTimeDescription = r.EventTimeDescription,
                ReservationDateTimeDescription = r.ReservationTimeDescription,
                SetupPhotoId = r.SetupPhotoId,
                SetupPhotoLink = ResolveRockUrl( String.Format( "~/GetImage.ashx?id={0}", r.SetupPhotoId ?? 0 ) ),
                Note = r.Note,
                RequesterAlias = r.RequesterAlias,
                EventContactPersonAlias = r.EventContactPersonAlias,
                EventContactEmail = r.EventContactEmail,
                EventContactPhoneNumber = r.EventContactPhoneNumber,
                MinistryName = r.ReservationMinistry != null ? r.ReservationMinistry.Name : string.Empty,
            } )
            .OrderBy( r => r.EventStartDateTime )
            .GroupBy( r => r.EventStartDateTime.Date )
            .Select( r => r.ToList() )
            .ToList();

            var mergeFields = new Dictionary<string, object>();
            mergeFields.Add( "TimeFrame", ViewMode );
            mergeFields.Add( "DetailsPage", LinkedPageUrl( "DetailsPage", null ) );
            mergeFields.Add( "ReservationSummaries", reservationSummaries );
            mergeFields.Add( "CurrentPerson", CurrentPerson );

            lOutput.Text = GetAttributeValue( "LavaTemplate" ).ResolveMergeFields( mergeFields );

            // show debug info
            if ( GetAttributeValue( "EnableDebug" ).AsBoolean() && IsUserAuthorized( Authorization.EDIT ) )
            {
                lDebug.Visible = true;
                lDebug.Text = mergeFields.lavaDebugInfo();
            }
            else
            {
                lDebug.Visible = false;
                lDebug.Text = string.Empty;
            }

        }

        private List<ReservationService.ReservationSummary> GetReservationSummaries()
        {
            var rockContext = new RockContext();
            var reservationService = new ReservationService( rockContext );
            var qry = reservationService.Queryable();

            // Filter by Resources
            var resourceIdList = rpResource.SelectedValuesAsInt().ToList();
            if ( resourceIdList.Where( r => r != 0 ).Any() )
            {
                qry = qry.Where( r => r.ReservationResources.Any( rr => resourceIdList.Contains( rr.ResourceId ) ) );
            }

            // Filter by Locations
            var locationIdList = lipLocation.SelectedValuesAsInt().ToList();
            if ( locationIdList.Where( r => r != 0 ).Any() )
            {
                qry = qry.Where( r => r.ReservationLocations.Any( rr => locationIdList.Contains( rr.LocationId ) ) );
            }

            // Filter by campus
            List<int> campusIds = cblCampus.Items.OfType<System.Web.UI.WebControls.ListItem>().Where( l => l.Selected ).Select( a => a.Value.AsInteger() ).ToList();
            if ( campusIds.Any() )
            {
                qry = qry
                    .Where( r =>
                        !r.CampusId.HasValue ||    // All
                        campusIds.Contains( r.CampusId.Value ) );
            }

            // Filter by Ministry
            List<int> ministryIds = cblMinistry.Items.OfType<System.Web.UI.WebControls.ListItem>().Where( l => l.Selected ).Select( a => a.Value.AsInteger() ).ToList();
            if ( ministryIds.Any() )
            {
                qry = qry
                    .Where( r =>
                        !r.ReservationMinistryId.HasValue ||    // All
                        ministryIds.Contains( r.ReservationMinistryId.Value ) );
            }

            // Filter by Approval
            List<ReservationApprovalState> approvalValues = cblApproval.Items.OfType<System.Web.UI.WebControls.ListItem>().Where( l => l.Selected ).Select( a => a.Value.ConvertToEnum<ReservationApprovalState>() ).ToList();
            if ( approvalValues.Any() )
            {
                qry = qry
                    .Where( r =>
                        approvalValues.Contains( r.ApprovalState ) );
            }

            // Filter by Reservation Type
            List<int> reservationTypeIds = cblReservationType.Items.OfType<System.Web.UI.WebControls.ListItem>().Where( l => l.Selected ).Select( a => a.Value.AsInteger() ).ToList();
            if ( reservationTypeIds.Any() )
            {
                qry = qry
                    .Where( r => reservationTypeIds.Contains( r.ReservationTypeId ) );
            }

            // Filter by Time
            var today = RockDateTime.Today;
            var filterStartDateTime = FilterStartDate.HasValue ? FilterStartDate.Value : today;
            var filterEndDateTime = FilterEndDate.HasValue ? FilterEndDate.Value : today.AddMonths( 1 );
            var reservationSummaryList = reservationService.GetReservationSummaries( qry, filterStartDateTime, filterEndDateTime, true );
            return reservationSummaryList;
        }

        /// <summary>
        /// Loads the drop downs.
        /// </summary>
        private bool SetFilterControls()
        {
            // Get and verify the view mode
            ViewMode = this.GetUserPreference( "ViewMode" );
            if ( string.IsNullOrWhiteSpace( ViewMode ) )
            {
                ViewMode = GetAttributeValue( "DefaultViewOption" );
            }

            if ( !GetAttributeValue( string.Format( "Show{0}View", ViewMode ) ).AsBoolean() )
            {
                ShowError( "Configuration Error", string.Format( "The Default View Option setting has been set to '{0}', but the Show {0} View setting has not been enabled.", ViewMode ) );
                return false;
            }

            // Show/Hide calendar control
            pnlCalendar.Visible = GetAttributeValue( "ShowSmallCalendar" ).AsBoolean();

            // Get the first/last dates based on today's date and the viewmode setting
            var today = RockDateTime.Today;

            // Use the CalendarVisibleDate if it's in session.
            if ( Session["CalendarVisibleDate"] != null )
            {
                today = ( DateTime ) Session["CalendarVisibleDate"];
                calReservationCalendar.VisibleDate = today;
            }

            FilterStartDate = today;
            FilterEndDate = today;
            if ( ViewMode == "Week" )
            {
                FilterStartDate = today.StartOfWeek( _firstDayOfWeek );
                FilterEndDate = today.EndOfWeek( _firstDayOfWeek );
            }
            else if ( ViewMode == "Month" )
            {
                FilterStartDate = new DateTime( today.Year, today.Month, 1 );
                FilterEndDate = FilterStartDate.Value.AddMonths( 1 ).AddDays( -1 );
            }

            // Setup small calendar Filter
            calReservationCalendar.FirstDayOfWeek = _firstDayOfWeek.ConvertToInt().ToString().ConvertToEnum<FirstDayOfWeek>();
            calReservationCalendar.SelectedDates.Clear();
            calReservationCalendar.SelectedDates.SelectRange( FilterStartDate.Value, FilterEndDate.Value );

            // Setup Location Filter
            lipLocation.Visible = GetAttributeValue( "LocationFilterDisplayMode" ).AsInteger() > 1;
            if ( !string.IsNullOrWhiteSpace( this.GetUserPreference( "Locations" ) ) )
            {
                lipLocation.SetValues( this.GetUserPreference( "Locations" ).Split( ',' ).AsIntegerList() );
            }

            // Setup Resource Filter
            rpResource.Visible = GetAttributeValue( "ResourceFilterDisplayMode" ).AsInteger() > 1;
            if ( !string.IsNullOrWhiteSpace( this.GetUserPreference( "Resources" ) ) )
            {
                rpResource.SetValues( this.GetUserPreference( "Resources" ).Split( ',' ).AsIntegerList() );
            }

            // Setup Campus Filter
            rcwCampus.Visible = GetAttributeValue( "CampusFilterDisplayMode" ).AsInteger() > 1;
            cblCampus.DataSource = CampusCache.All( false );
            cblCampus.DataBind();
            if ( !string.IsNullOrWhiteSpace( this.GetUserPreference( "Campuses" ) ) )
            {
                cblCampus.SetValues( this.GetUserPreference( "Campuses" ).SplitDelimitedValues() );
            }
            else
            {
                if ( GetAttributeValue( "EnableCampusContext" ).AsBoolean() )
                {
                    var contextCampus = RockPage.GetCurrentContext( EntityTypeCache.Get( "Rock.Model.Campus" ) ) as Campus;
                    if ( contextCampus != null )
                    {
                        cblCampus.SetValue( contextCampus.Id );
                    }
                }
            }

            // Setup Ministry Filter
            rcwMinistry.Visible = GetAttributeValue( "MinistryFilterDisplayMode" ).AsInteger() > 1;
            cblMinistry.DataSource = ReservationMinistryCache.All();
            cblMinistry.DataBind();

            if ( !string.IsNullOrWhiteSpace( this.GetUserPreference( "Ministries" ) ) )
            {
                cblMinistry.SetValues( this.GetUserPreference( "Ministries" ).SplitDelimitedValues() );
            }

            // Setup Approval Filter
            rcwApproval.Visible = GetAttributeValue( "ApprovalFilterDisplayMode" ).AsInteger() > 1;
            cblApproval.BindToEnum<ReservationApprovalState>();

            if ( !string.IsNullOrWhiteSpace( this.GetUserPreference( "Approval State" ) ) )
            {
                cblApproval.SetValues( this.GetUserPreference( "Approval State" ).SplitDelimitedValues() );
            }

            // Setup Reservation Type Filter
            rcwReservationType.Visible = GetAttributeValue( "ReservationTypeFilterDisplayMode" ).AsInteger() > 1;
            cblReservationType.DataSource = new ReservationTypeService( new RockContext() ).Queryable().ToList();
            cblReservationType.DataBind();

            if ( !string.IsNullOrWhiteSpace( this.GetUserPreference( "Reservation Type" ) ) )
            {
                cblReservationType.SetValues( this.GetUserPreference( "Reservation Type" ).SplitDelimitedValues() );
            }

            // Date Range Filter
            dpStartDate.Visible = GetAttributeValue( "ShowDateRangeFilter" ).AsBoolean();
            dpStartDate.SelectedDate = FilterStartDate;

            dpEndDate.Visible = GetAttributeValue( "ShowDateRangeFilter" ).AsBoolean();
            dpEndDate.SelectedDate = FilterEndDate;

            // Get the View Modes, and only show them if more than one is visible
            var viewsVisible = new List<bool> {
                GetAttributeValue( "ShowDayView" ).AsBoolean(),
                GetAttributeValue( "ShowWeekView" ).AsBoolean(),
                GetAttributeValue( "ShowMonthView" ).AsBoolean()
            };

            var howManyVisible = viewsVisible.Where( v => v ).Count();
            btnDay.Visible = howManyVisible > 1 && viewsVisible[0];
            btnWeek.Visible = howManyVisible > 1 && viewsVisible[1];
            btnMonth.Visible = howManyVisible > 1 && viewsVisible[2];

            // Set filter visibility
            bool showFilter = ( pnlCalendar.Visible || lipLocation.Visible || rpResource.Visible || rcwCampus.Visible || rcwMinistry.Visible || rcwApproval.Visible || dpStartDate.Visible || dpEndDate.Visible );
            pnlFilters.Visible = showFilter;
            pnlList.CssClass = showFilter ? "col-md-9" : "col-md-12";

            return true;
        }

        /// <summary>
        /// Resets the calendar selection. The control is configured for day selection, but selection will be changed to the week or month if that is the viewmode being used
        /// </summary>
        private void ResetCalendarSelection()
        {
            // Even though selection will be a single date due to calendar's selection mode, set the appropriate days
            var selectedDate = calReservationCalendar.SelectedDate;
            calReservationCalendar.VisibleDate = selectedDate;
            Session["CalendarVisibleDate"] = selectedDate;
            FilterStartDate = selectedDate;
            FilterEndDate = selectedDate;
            if ( ViewMode == "Week" )
            {
                FilterStartDate = selectedDate.StartOfWeek( _firstDayOfWeek );
                FilterEndDate = selectedDate.EndOfWeek( _firstDayOfWeek );
            }
            else if ( ViewMode == "Month" )
            {
                FilterStartDate = new DateTime( selectedDate.Year, selectedDate.Month, 1 );
                FilterEndDate = FilterStartDate.Value.AddMonths( 1 ).AddDays( -1 );
            }

            // Reset the selection
            calReservationCalendar.SelectedDates.SelectRange( FilterStartDate.Value, FilterEndDate.Value );
        }

        private void SetCalendarFilterDates()
        {
            FilterStartDate = calReservationCalendar.SelectedDates.Count > 0 ? calReservationCalendar.SelectedDates[0] : ( DateTime? ) null;
            FilterEndDate = calReservationCalendar.SelectedDates.Count > 0 ? calReservationCalendar.SelectedDates[calReservationCalendar.SelectedDates.Count - 1] : ( DateTime? ) null;
        }

        /// <summary>
        /// Shows the warning.
        /// </summary>
        /// <param name="heading">The heading.</param>
        /// <param name="message">The message.</param>
        private void ShowWarning( string heading, string message )
        {
            nbMessage.Heading = heading;
            nbMessage.Text = string.Format( "<p>{0}</p>", message );
            nbMessage.NotificationBoxType = NotificationBoxType.Danger;
            nbMessage.Visible = true;
        }

        /// <summary>
        /// Shows the error.
        /// </summary>
        /// <param name="heading">The heading.</param>
        /// <param name="message">The message.</param>
        private void ShowError( string heading, string message )
        {
            nbMessage.Heading = heading;
            nbMessage.Text = string.Format( "<p>{0}</p>", message );
            nbMessage.NotificationBoxType = NotificationBoxType.Danger;
            nbMessage.Visible = true;
        }


        /// <summary>
        /// Gets the report template.
        /// </summary>
        /// <param name="reportTemplateEntityTypeId">The report template entity type identifier.</param>
        /// <returns></returns>
        private ReportTemplate GetReportTemplate( Guid? reportTemplateGuid )
        {
            if ( reportTemplateGuid.HasValue )
            {
                var reportTemplateEntityType = EntityTypeCache.Get( reportTemplateGuid.Value );
                if ( reportTemplateEntityType == null )
                {
                    return null;
                }
                else
                {
                    return ReportTemplateContainer.GetComponent( reportTemplateEntityType.Name );
                }
            }
            else
            {
                return null;
            }
        }

        #endregion
    }
}
