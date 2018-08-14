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
using System.Web.UI.WebControls;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.UI;
using com.centralaz.RoomManagement.Model;
using DDay.iCal;
using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Web;
using Rock.Web.Cache;
using Rock.Web.UI;
using Newtonsoft.Json;
using Rock.Web.UI.Controls;
using Rock.Security;
using Rock.Communication;
using System.Web;
using System.Data.Entity;
using System.Web.UI.HtmlControls;
using com.centralaz.RoomManagement.Attribute;
using Rock.Constants;

namespace RockWeb.Plugins.com_centralaz.RoomManagement
{
    [DisplayName( "Reservation Detail" )]
    [Category( "com_centralaz > Room Management" )]
    [Description( "Block for viewing a reservation detail" )]

    public partial class ReservationDetail : Rock.Web.UI.RockBlock, IDetailBlock
    {
        #region Fields

        protected string PendingCss = "btn-default";
        protected string ApprovedCss = "btn-default";
        protected string DeniedCss = "btn-default";
        public bool _canEdit = false;
        public bool _canApprove = false;

        #endregion

        #region Properties
        /// <summary>
        /// Gets the base resource picker REST URL.
        /// </summary>
        /// <value>
        /// The base resource REST URL.
        /// </value>
        private string BaseResourceRestUrl
        {
            get
            {
                var baseResourceRestUrl = ViewState["BaseResourceRestUrl"] as string;

                if ( baseResourceRestUrl == null )
                {
                    srpResource.CampusId = ddlCampus.SelectedValueAsInt();
                    srpResource.SetExtraRestParams( srpResource.ShowAllResources );

                    ViewState["BaseResourceRestUrl"] = srpResource.ItemRestUrlExtraParams;
                    baseResourceRestUrl = srpResource.ItemRestUrlExtraParams;
                }
                return baseResourceRestUrl;
            }
            set
            {
                ViewState["BaseResourceRestUrl"] = value;
            }
        }

        /// <summary>
        /// Gets the base location picker REST URL.
        /// </summary>
        /// <value>
        /// The base location REST URL.
        /// </value>
        private string BaseLocationRestUrl
        {
            get
            {
                var baseLocationRestUrl = ViewState["BaseLocationRestUrl"] as string;

                if ( baseLocationRestUrl == null )
                {
                    ViewState["BaseLocationRestUrl"] = slpLocation.ItemRestUrlExtraParams;
                    baseLocationRestUrl = slpLocation.ItemRestUrlExtraParams;
                }
                return baseLocationRestUrl;
            }
        }

        /// <summary>
        /// Gets or sets the type of the reservation.
        /// </summary>
        /// <value>
        /// The type of the reservation.
        /// </value>
        private ReservationType ReservationType { get; set; }

        /// <summary>
        /// Gets or sets the state of the resources.
        /// </summary>
        /// <value>
        /// The state of the resources.
        /// </value>
        private List<ReservationResourceSummary> ResourcesState { get; set; }

        /// <summary>
        /// Gets or sets the state of the locations.
        /// </summary>
        /// <value>
        /// The state of the locations.
        /// </value>
        private List<ReservationLocationSummary> LocationsState { get; set; }

        #endregion

        #region Base Control Methods

        /// <summary>
        /// Restores the view-state information from a previous user control request that was saved by the <see cref="M:System.Web.UI.UserControl.SaveViewState" /> method.
        /// </summary>
        /// <param name="savedState">An <see cref="T:System.Object" /> that represents the user control state to be restored.</param>
        protected override void LoadViewState( object savedState )
        {
            base.LoadViewState( savedState );

            string json = ViewState["ResourcesState"] as string;
            if ( string.IsNullOrWhiteSpace( json ) )
            {
                ResourcesState = new List<ReservationResourceSummary>();
            }
            else
            {
                ResourcesState = JsonConvert.DeserializeObject<List<ReservationResourceSummary>>( json );
            }

            json = ViewState["LocationsState"] as string;
            if ( string.IsNullOrWhiteSpace( json ) )
            {
                LocationsState = new List<ReservationLocationSummary>();
            }
            else
            {
                LocationsState = JsonConvert.DeserializeObject<List<ReservationLocationSummary>>( json );
            }

            json = ViewState["ReservationType"] as string;
            if ( string.IsNullOrWhiteSpace( json ) )
            {
                ReservationType = new ReservationType();
            }
            else
            {
                ReservationType = JsonConvert.DeserializeObject<ReservationType>( json );
            }
        }

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Init" /> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnInit( EventArgs e )
        {
            base.OnInit( e );

            RockPage.AddCSSLink( ResolveRockUrl( "~/Styles/fluidbox.css" ) );
            Page.Header.Controls.Add( new LiteralControl( "<link rel=\"stylesheet\" type=\"text/css\" media=\"print\" href=\"/Plugins/com_centralaz/RoomManagement/Assets/Styles/print.css\" />" ) );

            gLocations.DataKeyNames = new string[] { "Guid" };
            gLocations.Actions.ShowAdd = true;
            gLocations.Actions.AddClick += gLocations_Add;
            gLocations.GridRebind += gLocations_GridRebind;

            gResources.DataKeyNames = new string[] { "Guid" };
            gResources.Actions.ShowAdd = true;
            gResources.Actions.AddClick += gResources_Add;
            gResources.GridRebind += gResources_GridRebind;


            gViewLocations.DataKeyNames = new string[] { "Guid" };
            gViewLocations.GridRebind += gLocations_GridRebind;

            gViewResources.DataKeyNames = new string[] { "Guid" };
            gViewResources.GridRebind += gResources_GridRebind;

            rptWorkflows.ItemCommand += rptWorkflows_ItemCommand;


            // this event gets fired after block settings are updated. it's nice to repaint the screen if these settings would alter it
            this.AddConfigurationUpdateTrigger( upnlContent );

            _canEdit = UserCanEdit;
            _canApprove = UserCanAdministrate;

            string script = string.Format( @"
    $('#{0} .btn-toggle').click(function (e) {{

        e.stopImmediatePropagation();

        $(this).find('.btn').removeClass('active');
        $(e.target).addClass('active');

        $(this).find('a').each(function() {{
            if ($(this).hasClass('active')) {{
                $('#{1}').val($(this).attr('data-status'));
                $(this).removeClass('btn-default');
                $(this).addClass( $(this).attr('data-active-css') );
            }} else {{
                $(this).removeClass( $(this).attr('data-active-css') );
                $(this).addClass('btn-default');
            }}
        }});

    }});
", pnlEditApprovalState.ClientID, hfApprovalState.ClientID );
            ScriptManager.RegisterStartupScript( pnlEditApprovalState, pnlEditApprovalState.GetType(), "status-script-" + this.BlockId.ToString(), script, true );
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
                ShowDetail( PageParameter( "ReservationId" ).AsInteger() );
            }
            else
            {
                // ShowDialog();
                LoadQuestionsAndAnswers();
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
            var jsonSetting = new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                ContractResolver = new Rock.Utility.IgnoreUrlEncodedKeyContractResolver()
            };

            ViewState["ReservationType"] = JsonConvert.SerializeObject( ReservationType, Formatting.None, jsonSetting );
            ViewState["ResourcesState"] = JsonConvert.SerializeObject( ResourcesState, Formatting.None, jsonSetting );
            ViewState["LocationsState"] = JsonConvert.SerializeObject( LocationsState, Formatting.None, jsonSetting );

            return base.SaveViewState();
        }

        /// <summary>
        /// Returns breadcrumbs specific to the block that should be added to navigation
        /// based on the current page reference.  This function is called during the page's
        /// oninit to load any initial breadcrumbs
        /// </summary>
        /// <param name="pageReference">The page reference.</param>
        /// <returns></returns>
        public override List<BreadCrumb> GetBreadCrumbs( PageReference pageReference )
        {
            var breadCrumbs = new List<BreadCrumb>();

            int? reservationId = PageParameter( pageReference, "ReservationId" ).AsIntegerOrNull();
            if ( reservationId != null )
            {
                Reservation reservation = new ReservationService( new RockContext() ).Get( reservationId.Value );
                if ( reservation != null )
                {
                    breadCrumbs.Add( new BreadCrumb( reservation.Name, pageReference ) );
                    lPanelTitle.Text = reservation.Name;
                    RockPage.Title = reservation.Name;
                    RockPage.BrowserTitle = reservation.Name;
                }
                else
                {
                    breadCrumbs.Add( new BreadCrumb( "New Reservation", pageReference ) );
                    lPanelTitle.Text = "New Reservation";
                }
            }
            else
            {
                breadCrumbs.Add( new BreadCrumb( "New Reservation", pageReference ) );
                lPanelTitle.Text = "New Reservation";
            }

            return breadCrumbs;
        }

        #endregion

        #region Events

        /// <summary>
        /// Handles the Click event of the btnSave control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected void btnSave_OnClick( object sender, EventArgs e )
        {
            nbErrorWarning.Visible = false;

            if ( Page.IsValid )
            {
                RockContext rockContext = new RockContext();
                bool saveSuccess = false;

                ResourceService resourceService = new ResourceService( rockContext );
                LocationService locationService = new LocationService( rockContext );
                ReservationService reservationService = new ReservationService( rockContext );
                ReservationMinistryService reservationMinistryService = new ReservationMinistryService( rockContext );
                ReservationResourceService reservationResourceService = new ReservationResourceService( rockContext );
                ReservationLocationService reservationLocationService = new ReservationLocationService( rockContext );

                Reservation reservation = null;
                var changes = new List<string>();

                if ( hfReservationId.ValueAsInt() != null )
                {
                    reservation = reservationService.Get( hfReservationId.ValueAsInt() );
                }

                if ( reservation == null )
                {
                    reservation = new Reservation { Id = 0 };
                    reservation.ApprovalState = ReservationApprovalState.Unapproved;
                    reservation.RequesterAliasId = CurrentPersonAliasId;
                    changes.Add( "Created Reservation" );
                }
                else
                {
                    var uiLocations = LocationsState.Select( l => l.Guid );
                    foreach ( var reservationLocation in reservation.ReservationLocations.Where( l => !uiLocations.Contains( l.Guid ) ).ToList() )
                    {
                        changes.Add( String.Format( "Removed <span class='field-name'>{0}</span> from Reservation", reservationLocation.Location.Name ) );
                        reservation.ReservationLocations.Remove( reservationLocation );
                        reservationLocationService.Delete( reservationLocation );
                    }

                    var uiResources = ResourcesState.Select( l => l.Guid );
                    foreach ( var reservationResource in reservation.ReservationResources.Where( l => !uiResources.Contains( l.Guid ) ).ToList() )
                    {
                        changes.Add( String.Format( "Removed <span class='field-name'>{0} {1}</span> from Reservation", reservationResource.Quantity, reservationResource.Resource.Name ) );
                        reservation.ReservationResources.Remove( reservationResource );
                        reservationResourceService.Delete( reservationResource );
                    }
                }

                Reservation oldReservation = BuildOldReservation( resourceService, locationService, reservationService, reservation );

                var reservationType = new ReservationTypeService( rockContext ).Get( ReservationType.Id );
                reservation.ReservationType = reservationType;
                reservation.ReservationTypeId = reservationType.Id;

                foreach ( var reservationLocationState in LocationsState )
                {
                    ReservationLocation reservationLocation = reservation.ReservationLocations.Where( a => a.Guid == reservationLocationState.Guid ).FirstOrDefault();
                    if ( reservationLocation == null )
                    {
                        reservationLocation = new ReservationLocation();
                        reservation.ReservationLocations.Add( reservationLocation );
                    }
                    else
                    {
                        reservationLocationState.Id = reservationLocation.Id;
                        reservationLocationState.Guid = reservationLocation.Guid;
                    }
                    reservationLocation.CopyPropertiesFrom( reservationLocationState as ReservationLocation );
                    reservationLocation.Reservation = reservationService.Get( reservation.Id );
                    reservationLocation.Location = locationService.Get( reservationLocation.LocationId );
                    reservationLocation.ReservationId = reservation.Id;
                }

                foreach ( var reservationResourceState in ResourcesState )
                {
                    ReservationResource reservationResource = reservation.ReservationResources.Where( a => a.Guid == reservationResourceState.Guid ).FirstOrDefault();
                    if ( reservationResource == null )
                    {
                        reservationResource = new ReservationResource();
                        reservation.ReservationResources.Add( reservationResource );
                    }
                    else
                    {
                        reservationResourceState.Id = reservationResource.Id;
                        reservationResourceState.Guid = reservationResource.Guid;
                    }

                    reservationResource.CopyPropertiesFrom( reservationResourceState as ReservationResource );
                    reservationResource.Reservation = reservationService.Get( reservation.Id );
                    reservationResource.Resource = resourceService.Get( reservationResource.ResourceId );
                    reservationResource.ReservationId = reservation.Id;
                }

                if ( sbSchedule.iCalendarContent != null )
                {
                    reservation.Schedule = new Schedule();
                    reservation.Schedule.iCalendarContent = sbSchedule.iCalendarContent;
                    History.EvaluateChange( changes, "Schedule", oldReservation.GetFriendlyReservationScheduleText(), reservation.GetFriendlyReservationScheduleText() );
                }

                CampusCache oldCampus = null;
                if ( reservation.CampusId.HasValue )
                {
                    oldCampus = CampusCache.Read( reservation.CampusId.Value );
                }

                CampusCache newCampus = null;
                if ( ddlCampus.SelectedValueAsId().HasValue )
                {
                    newCampus = CampusCache.Read( ddlCampus.SelectedValueAsId().Value );
                }

                History.EvaluateChange( changes, "Campus", oldCampus != null ? oldCampus.Name : "None", newCampus != null ? newCampus.Name : "None" );
                reservation.CampusId = ddlCampus.SelectedValueAsId();

                ReservationMinistry oldMinistry = null;
                if ( reservation.ReservationMinistryId.HasValue )
                {
                    oldMinistry = reservationMinistryService.Get( reservation.ReservationMinistryId.Value );
                }

                ReservationMinistry newMinistry = null;
                if ( ddlMinistry.SelectedValueAsId().HasValue )
                {
                    newMinistry = reservationMinistryService.Get( ddlMinistry.SelectedValueAsId().Value );
                }

                History.EvaluateChange( changes, "Ministry", oldMinistry != null ? oldMinistry.Name : "None", newMinistry != null ? newMinistry.Name : "None" );
                reservation.ReservationMinistryId = ddlMinistry.SelectedValueAsId();

                int? orphanedPhotoId = null;
                if ( reservation.SetupPhotoId != fuSetupPhoto.BinaryFileId )
                {
                    orphanedPhotoId = reservation.SetupPhotoId;
                    reservation.SetupPhotoId = fuSetupPhoto.BinaryFileId;

                    if ( orphanedPhotoId.HasValue )
                    {
                        if ( reservation.SetupPhotoId.HasValue )
                        {
                            changes.Add( "Modified the setup photo." );
                        }
                        else
                        {
                            changes.Add( "Deleted the setup photo." );
                        }
                    }
                    else if ( reservation.SetupPhotoId.HasValue )
                    {
                        changes.Add( "Added a setup photo." );
                    }
                }

                History.EvaluateChange( changes, "Note", reservation.Note, rtbNote.Text );
                reservation.Note = rtbNote.Text;

                History.EvaluateChange( changes, "Name", reservation.Name, rtbName.Text );
                reservation.Name = rtbName.Text;

                History.EvaluateChange( changes, "Number Attending", reservation.NumberAttending.ToString(), nbAttending.Text );
                reservation.NumberAttending = nbAttending.Text.AsInteger();

                History.EvaluateChange( changes, "Setup Time", reservation.SetupTime.ToString(), nbSetupTime.Text );
                reservation.SetupTime = nbSetupTime.Text.AsInteger();

                History.EvaluateChange( changes, "Cleanup Time", reservation.CleanupTime.ToString(), nbCleanupTime.Text );
                reservation.CleanupTime = nbCleanupTime.Text.AsInteger();

                if ( !reservation.EventContactPersonAliasId.Equals( ppEventContact.PersonAliasId ) )
                {
                    string prevPerson = ( reservation.EventContactPersonAlias != null && reservation.EventContactPersonAlias.Person != null ) ?
                        reservation.EventContactPersonAlias.Person.FullName : string.Empty;
                    string newPerson = ppEventContact.PersonName;
                    History.EvaluateChange( changes, "Event Contact", prevPerson, newPerson );
                }
                reservation.EventContactPersonAliasId = ppEventContact.PersonAliasId;

                History.EvaluateChange( changes, "Event Contact Phone Number", reservation.EventContactPhone, PhoneNumber.FormattedNumber( PhoneNumber.DefaultCountryCode(), pnEventContactPhone.Number ) );
                reservation.EventContactPhone = PhoneNumber.FormattedNumber( PhoneNumber.DefaultCountryCode(), pnEventContactPhone.Number );

                History.EvaluateChange( changes, "Event Contact Email", reservation.EventContactEmail, tbEventContactEmail.Text );
                reservation.EventContactEmail = tbEventContactEmail.Text;

                if ( !reservation.AdministrativeContactPersonAliasId.Equals( ppAdministrativeContact.PersonAliasId ) )
                {
                    string prevPerson = ( reservation.AdministrativeContactPersonAlias != null && reservation.AdministrativeContactPersonAlias.Person != null ) ?
                        reservation.AdministrativeContactPersonAlias.Person.FullName : string.Empty;
                    string newPerson = ppAdministrativeContact.PersonName;
                    History.EvaluateChange( changes, "Administrative Contact", prevPerson, newPerson );
                }
                reservation.AdministrativeContactPersonAliasId = ppAdministrativeContact.PersonAliasId;

                History.EvaluateChange( changes, "Administrative Contact Phone Number", reservation.AdministrativeContactPhone, PhoneNumber.FormattedNumber( PhoneNumber.DefaultCountryCode(), pnAdministrativeContactPhone.Number ) );
                reservation.AdministrativeContactPhone = PhoneNumber.FormattedNumber( PhoneNumber.DefaultCountryCode(), pnAdministrativeContactPhone.Number );

                History.EvaluateChange( changes, "Administrative Contact Email", reservation.AdministrativeContactEmail, tbAdministrativeContactEmail.Text );
                reservation.AdministrativeContactEmail = tbAdministrativeContactEmail.Text;

                foreach ( var reservationLocation in reservation.ReservationLocations )
                {
                    var headControl = phLocationAnswers.FindControl( "cReservationLocation_" + reservationLocation.Guid.ToString() ) as Control;
                    if ( headControl != null )
                    {
                        var phAttributes = headControl.FindControl( "phAttributes_" + reservationLocation.Guid.ToString() ) as PlaceHolder;
                        if ( phAttributes != null )
                        {
                            reservationLocation.LoadReservationLocationAttributes();
                            Rock.Attribute.Helper.GetEditValues( phAttributes, reservationLocation );
                        }
                    }
                }

                foreach ( var reservationResource in reservation.ReservationResources )
                {
                    var headControl = phResourceAnswers.FindControl( "cReservationResource_" + reservationResource.Guid.ToString() ) as Control;
                    if ( headControl != null )
                    {
                        var phAttributes = headControl.FindControl( "phAttributes_" + reservationResource.Guid.ToString() ) as PlaceHolder;
                        if ( phAttributes != null )
                        {
                            reservationResource.LoadReservationResourceAttributes();
                            Rock.Attribute.Helper.GetEditValues( phAttributes, reservationResource );
                        }
                    }
                }

                // Check to make sure there's a schedule
                if ( String.IsNullOrWhiteSpace( lScheduleText.Text ) )
                {
                    nbErrorWarning.Text = "<b>Please add a schedule.</b>";
                    nbErrorWarning.Visible = true;
                    return;
                }

                // Check to make sure there's no conflicts
                var conflictInfo = reservationService.GenerateConflictInfo( reservation, this.CurrentPageReference.Route );

                if ( !string.IsNullOrWhiteSpace( conflictInfo ) )
                {
                    nbErrorWarning.Text = conflictInfo;
                    nbErrorWarning.Visible = true;
                    return;
                }

                reservation = reservationService.UpdateApproval( reservation, hfApprovalState.Value.ConvertToEnum<ReservationApprovalState>( ReservationApprovalState.Unapproved ), CurrentPerson );

                changes = EvaluateLocationAndResourceChanges( changes, oldReservation, reservation );

                History.EvaluateChange( changes, "Approval State", oldReservation.ApprovalState.ToString(), reservation.ApprovalState.ToString() );

                if ( reservation.Id.Equals( 0 ) )
                {
                    reservationService.Add( reservation );
                }

                rockContext.WrapTransaction( () =>
                {
                    rockContext.SaveChanges();

                    foreach ( var reservationLocation in reservation.ReservationLocations )
                    {
                        reservationLocation.SaveAttributeValues( rockContext );
                    }

                    foreach ( var reservationResource in reservation.ReservationResources )
                    {
                        reservationResource.SaveAttributeValues( rockContext );
                    }

                    saveSuccess = true;
                } );

                if ( saveSuccess )
                {
                    // ..."need to fetch the item using a new service if you need the updated property as a fully hydrated entity"
                    reservation = new ReservationService( new RockContext() ).Get( reservation.Guid );

                    if ( changes.Any() )
                    {
                        HistoryService.SaveChanges(
                            rockContext,
                            typeof( Reservation ),
                            com.centralaz.RoomManagement.SystemGuid.Category.HISTORY_RESERVATION_CHANGES.AsGuid(),
                            reservation.Id,
                            changes );
                    }

                    // We can't send emails because it won't have an ID until the request is saved.
                    reservationService.SendNotifications( reservation );

                    BinaryFileService binaryFileService = new BinaryFileService( rockContext );
                    if ( orphanedPhotoId.HasValue )
                    {
                        var binaryFile = binaryFileService.Get( orphanedPhotoId.Value );
                        if ( binaryFile != null )
                        {
                            // marked the old images as IsTemporary so they will get cleaned up later
                            binaryFile.IsTemporary = true;
                            rockContext.SaveChanges();
                        }
                    }

                    // ensure the IsTemporary is set to false on binaryFile associated with this reservation
                    if ( reservation.SetupPhotoId.HasValue )
                    {
                        var binaryFile = binaryFileService.Get( reservation.SetupPhotoId.Value );
                        if ( binaryFile != null && binaryFile.IsTemporary )
                        {
                            binaryFile.IsTemporary = false;
                            rockContext.SaveChanges();
                        }
                    }

                    // Redirect back to same page so that item grid will show any attributes that were selected to show on grid
                    var qryParams = new Dictionary<string, string>();
                    qryParams["ReservationId"] = reservation.Id.ToString();
                    NavigateToPage( RockPage.Guid, qryParams );
                }
            }
        }

        /// <summary>
        /// Handles the OnClick event of the btnEdit control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void btnEdit_OnClick( object sender, EventArgs e )
        {
            var rockContext = new RockContext();
            var reservation = new ReservationService( rockContext ).Get( hfReservationId.Value.AsInteger() );

            ShowEditDetails( reservation );
        }

        /// <summary>
        /// Handles the Click event of the btnCancel control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected void btnCancel_OnClick( object sender, EventArgs e )
        {
            int reservationId = hfReservationId.ValueAsInt();
            if ( reservationId == 0 )
            {
                ReturnToParentPage();
            }
            else
            {
                var reservation = new ReservationService( new RockContext() ).Get( reservationId );
                ShowReadonlyDetails( reservation );
            }
        }

        /// <summary>
        /// Handles the SelectedIndexChanged event of the ddlCampus control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void ddlCampus_SelectedIndexChanged( object sender, EventArgs e )
        {
            srpResource.CampusId = ddlCampus.SelectedValueAsInt();
            srpResource.SetExtraRestParams( srpResource.ShowAllResources );

            BaseResourceRestUrl = srpResource.ItemRestUrlExtraParams;
            ddlCampus.Focus();
        }

        /// <summary>
        /// Handles the SelectedIndexChanged event of the ddlReservationType control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void ddlReservationType_SelectedIndexChanged( object sender, EventArgs e )
        {
            ReservationType = new ReservationTypeService( new RockContext() ).Get( ddlReservationType.SelectedValueAsId().Value );
        }

        /// <summary>
        /// Handles the Click event of the btnDelete control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected void btnDelete_OnClick( object sender, EventArgs e )
        {
            if ( PageParameter( "ReservationId" ).AsIntegerOrNull() != null )
            {
                RockContext rockContext = new RockContext();
                ReservationService reservationService = new ReservationService( rockContext );
                ReservationResourceService reservationResourceService = new ReservationResourceService( rockContext );
                ReservationLocationService reservationLocationService = new ReservationLocationService( rockContext );
                var reservation = reservationService.Get( hfReservationId.ValueAsInt() );
                if ( reservation != null )
                {
                    if ( reservation.ReservationResources != null )
                    {
                        reservationResourceService.DeleteRange( reservation.ReservationResources );
                    }

                    if ( reservation.ReservationLocations != null )
                    {
                        reservationLocationService.DeleteRange( reservation.ReservationLocations );
                    }

                    reservationService.Delete( reservation );
                    rockContext.SaveChanges();
                }
            }

            ReturnToParentPage();
        }

        /// <summary>
        /// Handles the Click event of the Copy button control and creates a copy of the reservation 
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected void btnCopy_Click( object sender, EventArgs e )
        {
            // Create a new Data View using the current item as a template.
            var id = int.Parse( hfReservationId.Value );

            var reservationService = new ReservationService( new RockContext() );

            var newItem = reservationService.GetNewFromTemplate( id );

            if ( newItem == null )
            {
                return;
            }

            newItem.Name += " (Copy)";

            // Reset the stored identifier for the active Reservation.
            hfReservationId.Value = "0";

            btnCopy.Visible = false;
            btnDelete.Visible = false;
            ShowEditDetails( newItem );
        }

        /// <summary>
        /// Handles the SaveSchedule event of the sbSchedule control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void sbSchedule_SaveSchedule( object sender, EventArgs e )
        {
            nbErrorWarning.Visible = false;

            var schedule = new Schedule { iCalendarContent = sbSchedule.iCalendarContent };
            lScheduleText.Text = schedule.FriendlyScheduleText;
            LoadPickers();
        }

        /// <summary>
        /// Handles the TextChanged event of the nbSetupTime control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void nbSetupTime_TextChanged( object sender, EventArgs e )
        {
            LoadPickers();
        }

        /// <summary>
        /// Handles the TextChanged event of the nbCleanupTime control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void nbCleanupTime_TextChanged( object sender, EventArgs e )
        {
            LoadPickers();
        }

        /// <summary>
        /// Handles the ValueChanged event of the approval toggle. If the reservation is set to approved, it will iterate through each resource and location and approve the ones the user has access to approve.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void hfApprovalState_ValueChanged( object sender, EventArgs e )
        {
            if ( PageParameter( "ReservationId" ).AsIntegerOrNull() != null )
            {
                var reservation = new ReservationService( new RockContext() ).Get( hfReservationId.ValueAsInt() );
                if ( reservation != null )
                {
                    ReservationApprovalState? newApprovalState = hfApprovalState.Value.ConvertToEnum<ReservationApprovalState>();

                    if ( newApprovalState != null && newApprovalState == ReservationApprovalState.Approved )
                    {
                        bool isSuperAdmin = ReservationTypeService.IsPersonInGroupWithId( CurrentPerson, reservation.ReservationType.SuperAdminGroupId );
                        foreach ( var reservationResource in reservation.ReservationResources )
                        {
                            bool canApprove = ReservationService.CanPersonApproveReservationResource( CurrentPerson, isSuperAdmin, reservationResource );
                            if ( canApprove )
                            {
                                reservationResource.ApprovalState = ReservationResourceApprovalState.Approved;
                            }
                        }

                        foreach ( var reservationLocation in reservation.ReservationLocations )
                        {
                            bool canApprove = ReservationService.CanPersonApproveReservationLocation( CurrentPerson, isSuperAdmin, reservationLocation );
                            if ( canApprove )
                            {
                                reservationLocation.ApprovalState = ReservationLocationApprovalState.Approved;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Handles the ItemCommand event of the rptWorkflows control.
        /// </summary>
        /// <param name="source">The source of the event.</param>
        /// <param name="e">The <see cref="RepeaterCommandEventArgs"/> instance containing the event data.</param>
        protected void rptWorkflows_ItemCommand( object source, RepeaterCommandEventArgs e )
        {
            if ( e.CommandName == "LaunchWorkflow" )
            {
                using ( var rockContext = new RockContext() )
                {
                    var reservation = new ReservationService( rockContext ).Get( hfReservationId.ValueAsInt() );
                    var reservationWorkflowTrigger = new ReservationWorkflowTriggerService( rockContext ).Get( e.CommandArgument.ToString().AsInteger() );
                    if ( reservation != null && reservationWorkflowTrigger != null && reservationWorkflowTrigger.WorkflowType.IsAuthorized( Authorization.VIEW, CurrentPerson ) )
                    {
                        LaunchWorkflow( rockContext, reservation, reservationWorkflowTrigger );
                    }
                }
            }
        }

        #region ReservationResource Events

        /// <summary>
        /// Handles the SelectItem event of the srpResource control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void srpResource_SelectItem( object sender, EventArgs e )
        {
            // On Item selected, set maximum value on the quantity number box and display it somewhere
            var rockContext = new RockContext();
            var resource = new ResourceService( rockContext ).Get( srpResource.SelectedValueAsId() ?? 0 );
            if ( resource != null )
            {
                var newReservation = new Reservation() { Id = PageParameter( "ReservationId" ).AsIntegerOrNull() ?? 0, Schedule = new Schedule() { iCalendarContent = sbSchedule.iCalendarContent }, SetupTime = nbSetupTime.Text.AsInteger(), CleanupTime = nbCleanupTime.Text.AsInteger() };
                var availableQuantity = new ReservationService( rockContext ).GetAvailableResourceQuantity( resource, newReservation );
                nbQuantity.MaximumValue = availableQuantity.ToString();
                nbQuantity.Label = String.Format( "Quantity ({0} of {1} Available)", availableQuantity, resource.Quantity );
                if ( availableQuantity >= 1 )
                {
                    nbQuantity.Enabled = true;

                    if ( string.IsNullOrWhiteSpace( nbQuantity.Text ) )
                    {
                        nbQuantity.Text = "1";
                    }
                }
                else
                {
                    nbQuantity.MinimumValue = "0";
                    nbQuantity.Enabled = false;
                }
            }

            LoadResourceConflictMessage();
        }

        /// <summary>
        /// Handles the SaveClick event of the dlgReservationResource control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void dlgReservationResource_SaveClick( object sender, EventArgs e )
        {
            SaveReservationResource();
            dlgReservationResource.Hide();
            hfActiveDialog.Value = string.Empty;
        }

        protected void dlgReservationResource_SaveThenAddClick( object sender, EventArgs e )
        {
            SaveReservationResource();
            LoadPickers();
            gResources_ShowEdit( Guid.Empty );
        }

        /// <summary>
        /// Handles the Delete event of the gResources control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RowEventArgs"/> instance containing the event data.</param>
        protected void gResources_Delete( object sender, RowEventArgs e )
        {
            nbErrorWarning.Visible = false;
            Guid rowGuid = ( Guid ) e.RowKeyValue;
            ResourcesState.RemoveEntity( rowGuid );

            var headControl = phResourceAnswers.FindControl( "cReservationResource_" + rowGuid.ToString() ) as Control;
            if ( headControl != null )
            {
                phResourceAnswers.Controls.Remove( headControl );
            }

            BindReservationResourcesGrid();
        }

        /// <summary>
        /// Handles the GridRebind event of the gResources control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void gResources_GridRebind( object sender, EventArgs e )
        {
            BindReservationResourcesGrid();
        }

        /// <summary>
        /// Handles the Edit event of the gResources control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RowEventArgs"/> instance containing the event data.</param>
        protected void gResources_Edit( object sender, RowEventArgs e )
        {
            Guid reservationResourceGuid = ( Guid ) e.RowKeyValue;
            LoadPickers();
            gResources_ShowEdit( reservationResourceGuid );
        }

        /// <summary>
        /// gs the resources_ show edit.
        /// </summary>
        /// <param name="reservationResourceGuid">The reservation resource unique identifier.</param>
        protected void gResources_ShowEdit( Guid reservationResourceGuid )
        {
            nbErrorWarning.Visible = false;
            nbQuantity.Label = "Quantity";

            ReservationResourceSummary reservationResource = ResourcesState.FirstOrDefault( l => l.Guid.Equals( reservationResourceGuid ) );
            if ( reservationResource != null )
            {
                nbQuantity.Text = reservationResource.Quantity.ToString();
                srpResource.SetValue( reservationResource.ResourceId );
            }
            else
            {
                nbQuantity.Text = String.Empty;
                srpResource.SetValue( null );
            }

            LoadResourceConflictMessage();

            hfAddReservationResourceGuid.Value = reservationResourceGuid.ToString();
            hfActiveDialog.Value = "dlgReservationResource";
            dlgReservationResource.Show();
        }

        /// <summary>
        /// Handles the Add event of the gResources control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void gResources_Add( object sender, EventArgs e )
        {
            LoadPickers();
            gResources_ShowEdit( Guid.Empty );
        }

        /// <summary>
        /// Binds the reservation resources grid.
        /// </summary>
        private void BindReservationResourcesGrid()
        {
            gResources.EntityTypeId = EntityTypeCache.Read<com.centralaz.RoomManagement.Model.ReservationResource>().Id;
            gResources.SetLinqDataSource( ResourcesState.AsQueryable().OrderBy( r => r.Resource.Name ) );
            gResources.DataBind();
        }

        /// <summary>
        /// Handles the ApproveClick event of the gResources control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RowEventArgs"/> instance containing the event data.</param>
        protected void gResources_ApproveClick( object sender, RowEventArgs e )
        {
            bool failure = true;

            if ( e.RowKeyValue != null )
            {
                var reservationResource = ResourcesState.FirstOrDefault( r => r.Guid.Equals( ( Guid ) e.RowKeyValue ) );
                if ( reservationResource != null )
                {
                    failure = false;

                    reservationResource.ApprovalState = ReservationResourceApprovalState.Approved;
                }

                BindReservationResourcesGrid();
            }

            if ( failure )
            {
                maResourceGridWarning.Show( "Unable to approve that resource", ModalAlertType.Warning );
            }
        }

        /// <summary>
        /// Handles the DenyClick event of the gResources control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RowEventArgs"/> instance containing the event data.</param>
        protected void gResources_DenyClick( object sender, RowEventArgs e )
        {
            bool failure = true;

            if ( e.RowKeyValue != null )
            {
                var reservationResource = ResourcesState.FirstOrDefault( r => r.Guid.Equals( ( Guid ) e.RowKeyValue ) );
                if ( reservationResource != null )
                {
                    failure = false;

                    reservationResource.ApprovalState = ReservationResourceApprovalState.Denied;
                }

                BindReservationResourcesGrid();
            }

            if ( failure )
            {
                maResourceGridWarning.Show( "Unable to deny that resource", ModalAlertType.Warning );
            }
        }

        /// <summary>
        /// Handles the RowDataBound event of the gResources control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="GridViewRowEventArgs"/> instance containing the event data.</param>
        protected void gResources_RowDataBound( object sender, GridViewRowEventArgs e )
        {
            var reservationResource = e.Row.DataItem as ReservationResource;
            if ( reservationResource != null )
            {
                var canApprove = false;

                if ( !canApprove )
                {
                    canApprove = ReservationTypeService.IsPersonInGroupWithId( CurrentPerson, reservationResource.Resource.ApprovalGroupId );
                }

                if ( !canApprove )
                {
                    canApprove = ReservationTypeService.IsPersonInGroupWithId( CurrentPerson, ReservationType.SuperAdminGroupId );
                }

                if ( !canApprove )
                {
                    canApprove = ReservationTypeService.IsPersonInGroupWithId( CurrentPerson, ReservationType.FinalApprovalGroupId );
                }

                if ( e.Row.RowType == DataControlRowType.DataRow )
                {
                    if ( !canApprove )
                    {
                        e.Row.Cells[4].Controls[0].Visible = false;
                        e.Row.Cells[5].Controls[0].Visible = false;
                    }
                }
            }
        }

        #endregion

        #region ReservationLocation Events

        /// <summary>
        /// Handles the SelectItem event of the srpLocation control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void slpLocation_SelectItem( object sender, EventArgs e )
        {
            LoadLocationImage();
            LoadLocationConflictMessage();
        }

        /// <summary>
        /// Handles the SaveClick event of the dlgReservationLocation control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void dlgReservationLocation_SaveClick( object sender, EventArgs e )
        {
            SaveReservationLocation();

            dlgReservationLocation.Hide();
            hfActiveDialog.Value = string.Empty;
        }

        /// <summary>
        /// Handles the SaveThenAddClick event of the dlgReservationLocation control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void dlgReservationLocation_SaveThenAddClick( object sender, EventArgs e )
        {
            SaveReservationLocation();
            gLocations_ShowEdit( Guid.Empty );
        }

        /// <summary>
        /// Handles the Delete event of the gLocations control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RowEventArgs"/> instance containing the event data.</param>
        protected void gLocations_Delete( object sender, RowEventArgs e )
        {
            nbErrorWarning.Visible = false;
            Guid rowGuid = ( Guid ) e.RowKeyValue;

            // check for attached resources and remove them too
            var reservationLocation = LocationsState.FirstOrDefault( a => a.Guid == rowGuid );
            if ( reservationLocation != null && reservationLocation.LocationId != null )
            {
                var attachedResources = new ResourceService( new RockContext() ).Queryable().Where( r => r.Location.Id == reservationLocation.LocationId );
                if ( attachedResources.Any() )
                {
                    foreach ( var resource in attachedResources )
                    {
                        var item = ResourcesState.FirstOrDefault( a => a.ResourceId == resource.Id );
                        if ( item != null )
                        {
                            ResourcesState.Remove( item );
                            var headControlResource = phResourceAnswers.FindControl( "cReservationResource_" + item.Guid.ToString() ) as Control;
                            if ( headControlResource != null )
                            {
                                phResourceAnswers.Controls.Remove( headControlResource );
                            }
                        }
                    }
                    BindReservationResourcesGrid();
                }
            }

            LocationsState.RemoveEntity( rowGuid );

            var headControl = phLocationAnswers.FindControl( "cReservationLocation_" + reservationLocation.Guid.ToString() ) as Control;
            if ( headControl != null )
            {
                phLocationAnswers.Controls.Remove( headControl );
            }

            BindReservationLocationsGrid();
        }

        /// <summary>
        /// Handles the GridRebind event of the gLocations control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void gLocations_GridRebind( object sender, EventArgs e )
        {
            BindReservationLocationsGrid();
        }

        /// <summary>
        /// Handles the Edit event of the gLocations control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RowEventArgs"/> instance containing the event data.</param>
        protected void gLocations_Edit( object sender, RowEventArgs e )
        {
            Guid reservationLocationGuid = ( Guid ) e.RowKeyValue;
            gLocations_ShowEdit( reservationLocationGuid );
        }

        /// <summary>
        /// gs the locations_ show edit.
        /// </summary>
        /// <param name="reservationLocationGuid">The reservation location unique identifier.</param>
        protected void gLocations_ShowEdit( Guid reservationLocationGuid )
        {
            nbErrorWarning.Visible = false;
            ReservationLocationSummary reservationLocation = LocationsState.FirstOrDefault( l => l.Guid.Equals( reservationLocationGuid ) );
            if ( reservationLocation != null )
            {
                reservationLocation.ApprovalState = ReservationLocationApprovalState.Unapproved;
                slpLocation.SetValue( reservationLocation.LocationId );
                LoadLocationImage();
            }
            else
            {
                lImage.Text = string.Empty;
                slpLocation.SetValue( null );
            }

            LoadLocationConflictMessage();

            hfAddReservationLocationGuid.Value = reservationLocationGuid.ToString();
            hfActiveDialog.Value = "dlgReservationLocation";
            dlgReservationLocation.Show();
        }

        /// <summary>
        /// Handles the Add event of the gLocations control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void gLocations_Add( object sender, EventArgs e )
        {
            gLocations_ShowEdit( Guid.Empty );
        }

        /// <summary>
        /// Binds the reservation locations grid.
        /// </summary>
        private void BindReservationLocationsGrid()
        {
            gLocations.EntityTypeId = EntityTypeCache.Read<com.centralaz.RoomManagement.Model.ReservationLocation>().Id;
            gLocations.SetLinqDataSource( LocationsState.AsQueryable().OrderBy( l => l.Location.Name ) );
            gLocations.DataBind();
        }

        /// <summary>
        /// Handles the ApproveClick event of the gLocations control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RowEventArgs"/> instance containing the event data.</param>
        protected void gLocations_ApproveClick( object sender, RowEventArgs e )
        {
            bool failure = true;

            if ( e.RowKeyValue != null )
            {
                var reservationLocation = LocationsState.FirstOrDefault( r => r.Guid.Equals( ( Guid ) e.RowKeyValue ) );
                if ( reservationLocation != null )
                {
                    failure = false;

                    reservationLocation.ApprovalState = ReservationLocationApprovalState.Approved;
                }

                BindReservationLocationsGrid();
            }

            if ( failure )
            {
                maLocationGridWarning.Show( "Unable to approve that location", ModalAlertType.Warning );
            }
        }

        /// <summary>
        /// Handles the DenyClick event of the gLocations control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RowEventArgs"/> instance containing the event data.</param>
        protected void gLocations_DenyClick( object sender, RowEventArgs e )
        {
            bool failure = true;

            if ( e.RowKeyValue != null )
            {
                var reservationLocation = LocationsState.FirstOrDefault( r => r.Guid.Equals( ( Guid ) e.RowKeyValue ) );
                if ( reservationLocation != null )
                {
                    failure = false;

                    reservationLocation.ApprovalState = ReservationLocationApprovalState.Denied;
                }

                BindReservationLocationsGrid();
            }

            if ( failure )
            {
                maLocationGridWarning.Show( "Unable to deny that location", ModalAlertType.Warning );
            }
        }

        /// <summary>
        /// Handles the RowDataBound event of the gLocations control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="GridViewRowEventArgs"/> instance containing the event data.</param>
        protected void gLocations_RowDataBound( object sender, GridViewRowEventArgs e )
        {
            Guid? approvalGroupGuid = null;

            var reservationLocation = e.Row.DataItem as ReservationLocationSummary;
            if ( reservationLocation != null )
            {
                var canApprove = false;

                var location = reservationLocation.Location;
                // bug fix:
                if ( location != null )
                {
                    location.LoadAttributes();
                    approvalGroupGuid = location.GetAttributeValue( "ApprovalGroup" ).AsGuidOrNull();
                }

                if ( !canApprove )
                {
                    canApprove = ReservationTypeService.IsPersonInGroupWithGuid( CurrentPerson, approvalGroupGuid );
                }

                if ( !canApprove )
                {
                    canApprove = ReservationTypeService.IsPersonInGroupWithId( CurrentPerson, ReservationType.SuperAdminGroupId );
                }

                if ( !canApprove )
                {
                    canApprove = ReservationTypeService.IsPersonInGroupWithId( CurrentPerson, ReservationType.FinalApprovalGroupId );
                }

                if ( e.Row.RowType == DataControlRowType.DataRow )
                {
                    if ( !canApprove )
                    {
                        e.Row.Cells[2].Controls[0].Visible = false;
                        e.Row.Cells[3].Controls[0].Visible = false;
                    }
                }
            }
        }

        #endregion

        #endregion

        #region Methods

        /// <summary>
        /// Shows the detail.
        /// </summary>
        /// <param name="reservationId">The reservation identifier.</param>
        public void ShowDetail( int reservationId )
        {
            pnlEditDetails.Visible = false;

            Reservation reservation = null;

            var rockContext = new RockContext();

            if ( !reservationId.Equals( 0 ) )
            {
                reservation = new ReservationService( rockContext ).Get( reservationId );
                hfReservationId.Value = reservationId.ToString();
                pdAuditDetails.SetEntity( reservation, ResolveRockUrl( "~" ) );
            }

            if ( reservation == null )
            {
                btnCopy.Visible = false;

                reservation = GenerateNewReservation( rockContext );

                // hide the panel drawer that show created and last modified dates
                pdAuditDetails.Visible = false;
            }
            else
            {
                ReservationType = reservation.ReservationType;
            }

            LocationsState = new List<ReservationLocationSummary>();
            foreach ( var reservationLocation in reservation.ReservationLocations.ToList() )
            {
                var rlSummary = new ReservationLocationSummary();
                rlSummary.CopyPropertiesFrom( reservationLocation );
                LocationsState.Add( rlSummary );
            }

            ResourcesState = new List<ReservationResourceSummary>();
            foreach ( var reservationResource in reservation.ReservationResources.ToList() )
            {
                var rrSummary = new ReservationResourceSummary();
                rrSummary.CopyPropertiesFrom( reservationResource );
                ResourcesState.Add( rrSummary );
            }

            reservation.LoadAttributes( rockContext );

            bool readOnly = false;
            nbEditModeMessage.Text = string.Empty;

            if ( !_canEdit )
            {
                readOnly = true;
                nbEditModeMessage.Text = EditModeMessage.ReadOnlyEditActionNotAllowed( EventItem.FriendlyTypeName );
            }

            if ( readOnly )
            {
                btnEdit.Visible = false;
                btnDelete.Visible = false;
                ShowReadonlyDetails( reservation );
            }
            else
            {
                btnEdit.Visible = true;
                btnDelete.Visible = true;

                if ( !reservationId.Equals( 0 ) )
                {
                    ShowReadonlyDetails( reservation );
                }
                else
                {
                    ShowEditDetails( reservation );
                }

            }
        }

        /// <summary>
        /// Shows the readonly details.
        /// </summary>
        /// <param name="reservation">The reservation.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        private void ShowReadonlyDetails( Reservation reservation )
        {
            SetEditMode( false );

            hfReservationId.SetValue( reservation.Id );

            lName.Text = reservation.Name;
            lNotes.Text = reservation.Note;
            lNumberAttending.Text = reservation.NumberAttending.ToString();
            lSetupTime.Text = reservation.SetupTime.HasValue ? String.Format( "{0} min", reservation.SetupTime ) : "N/A";
            lCleanupTime.Text = reservation.CleanupTime.HasValue ? String.Format( "{0} min", reservation.CleanupTime ) : "N/A";
            lCampus.Text = reservation.Campus != null ? reservation.Campus.Name : string.Empty;
            lMinistry.Text = reservation.ReservationMinistry != null ? reservation.ReservationMinistry.Name : string.Empty;
            lReservationType.Text = ReservationType.Name;
            lSchedule.Text = reservation.GetFriendlyReservationScheduleText();
            lEventContact.Text = String.Format( "<a href='/Person/{0}'>{1}</a><br>{2}<br>{3}",
                reservation.EventContactPersonAlias != null ? reservation.EventContactPersonAlias.PersonId.ToStringSafe() : string.Empty,
                reservation.EventContactPersonAlias != null ? reservation.EventContactPersonAlias.Person.FullName : string.Empty,
                reservation.EventContactPhone,
                reservation.EventContactEmail );
            lAdminContact.Text = String.Format( "<a href='/Person/{0}'>{1}</a><br>{2}<br>{3}",
                reservation.AdministrativeContactPersonAlias != null ? reservation.AdministrativeContactPersonAlias.PersonId.ToStringSafe() : string.Empty,
                reservation.AdministrativeContactPersonAlias != null ? reservation.AdministrativeContactPersonAlias.Person.FullName : string.Empty,
                reservation.AdministrativeContactPhone,
                reservation.AdministrativeContactEmail );

            if ( reservation.SetupPhotoId.HasValue )
            {
                string imgTag = string.Format( "<img src='{0}GetImage.ashx?id={1}&maxwidth=200&maxheight=200'/>", VirtualPathUtility.ToAbsolute( "~/" ), reservation.SetupPhotoId.Value );

                string imgUrl = string.Format( "~/GetImage.ashx?id={0}", reservation.SetupPhotoId );
                if ( System.Web.HttpContext.Current != null )
                {
                    imgUrl = VirtualPathUtility.ToAbsolute( imgUrl );
                }

                lSetupPhoto.Text = string.Format( "<a href='{0}' target='_blank'>{1}</a>", imgUrl, imgTag );
            }

            bool canApprove = false;

            if ( !canApprove )
            {
                canApprove = ReservationTypeService.IsPersonInGroupWithId( CurrentPerson, ReservationType.SuperAdminGroupId );
            }

            if ( !canApprove )
            {
                canApprove = ReservationTypeService.IsPersonInGroupWithId( CurrentPerson, ReservationType.FinalApprovalGroupId );
            }

            // Show the delete button if the person is authorized to delete it
            if ( canApprove || CurrentPersonAliasId == reservation.CreatedByPersonAliasId )
            {
                btnDelete.Visible = true;
            }

            lApproval.Text = hlStatus.Text = reservation.ApprovalState.ConvertToString();
            switch ( reservation.ApprovalState )
            {
                case ReservationApprovalState.Approved:
                    hlStatus.LabelType = LabelType.Success;
                    break;
                case ReservationApprovalState.Denied:
                    hlStatus.LabelType = LabelType.Danger;
                    break;
                case ReservationApprovalState.PendingReview:
                    hlStatus.LabelType = LabelType.Warning;
                    break;
                case ReservationApprovalState.Unapproved:
                    hlStatus.LabelType = LabelType.Warning;
                    break;
                case ReservationApprovalState.ChangesNeeded:
                    hlStatus.LabelType = LabelType.Info;
                    break;
                default:
                    hlStatus.LabelType = LabelType.Default;
                    break;
            }

            hfApprovalState.Value = reservation.ApprovalState.ConvertToString();
            LoadQuestionsAndAnswers( false, true );

            gViewLocations.EntityTypeId = EntityTypeCache.Read<com.centralaz.RoomManagement.Model.ReservationLocation>().Id;
            gViewLocations.SetLinqDataSource( LocationsState.AsQueryable().OrderBy( l => l.Location.Name ) );
            gViewLocations.DataBind();

            gViewResources.EntityTypeId = EntityTypeCache.Read<com.centralaz.RoomManagement.Model.ReservationResource>().Id;
            gViewResources.SetLinqDataSource( ResourcesState.AsQueryable().OrderBy( r => r.Resource.Name ) );
            gViewResources.DataBind();

            if ( ReservationType != null )
            {
                var reservationWorkflowTriggers = ReservationType.ReservationWorkflowTriggers;
                var manualWorkflows = reservationWorkflowTriggers
                    .Where( w =>
                        w.TriggerType == ReservationWorkflowTriggerType.Manual &&
                        w.WorkflowType != null )
                    .OrderBy( w => w.WorkflowType.Name )
                    .Distinct();

                var authorizedWorkflows = new List<ReservationWorkflowTrigger>();
                foreach ( var manualWorkflow in manualWorkflows )
                {
                    if ( manualWorkflow.WorkflowType.IsAuthorized( Authorization.VIEW, CurrentPerson ) )
                    {
                        authorizedWorkflows.Add( manualWorkflow );
                    }
                }

                if ( authorizedWorkflows.Any() )
                {
                    lblWorkflows.Visible = true;
                    rptWorkflows.DataSource = authorizedWorkflows.ToList();
                    rptWorkflows.DataBind();
                }
                else
                {
                    lblWorkflows.Visible = false;
                }
            }


        }

        /// <summary>
        /// Shows the edit details.
        /// </summary>
        /// <param name="reservation">The reservation.</param>
        private void ShowEditDetails( Reservation reservation )
        {
            using ( RockContext rockContext = new RockContext() )
            {

                ddlReservationType.Enabled = ( reservation.Id == 0 );

                SetEditMode( true );
                hfReservationId.SetValue( reservation.Id );

                nbSetupTime.Required = nbCleanupTime.Required = ReservationType.IsSetupTimeRequired;
                nbAttending.Required = ReservationType.IsNumberAttendingRequired;
                bool requireContactDetails = ReservationType.IsContactDetailsRequired;

                if ( requireContactDetails )
                {
                    ppAdministrativeContact.Required = true;
                    pnAdministrativeContactPhone.Required = true;
                    tbAdministrativeContactEmail.Required = true;

                    ppEventContact.Required = true;
                    pnEventContactPhone.Required = true;
                    tbEventContactEmail.Required = true;
                }

                sbSchedule.iCalendarContent = string.Empty;
                if ( reservation.Schedule != null )
                {
                    sbSchedule.iCalendarContent = reservation.Schedule.iCalendarContent;
                    lScheduleText.Text = reservation.Schedule.FriendlyScheduleText;
                    srpResource.Enabled = true;
                    slpLocation.Enabled = true;
                }
                else
                {
                    if ( PageParameter( "ScheduleId" ).AsInteger() != 0 )
                    {
                        var schedule = new ScheduleService( rockContext ).Get( PageParameter( "ScheduleId" ).AsInteger() );
                        if ( schedule != null )
                        {
                            sbSchedule.iCalendarContent = schedule.iCalendarContent;
                        }
                    }
                }

                fuSetupPhoto.BinaryFileId = reservation.SetupPhotoId;

                var defaultTime = ReservationType.DefaultSetupTime.ToString();
                if ( defaultTime == "-1" )
                {
                    defaultTime = string.Empty;
                }

                rtbName.Text = reservation.Name;
                rtbNote.Text = reservation.Note;
                nbAttending.Text = reservation.NumberAttending.ToString();
                nbSetupTime.Text = reservation.SetupTime.HasValue ? reservation.SetupTime.ToString() : defaultTime;
                nbCleanupTime.Text = reservation.CleanupTime.HasValue ? reservation.CleanupTime.ToString() : defaultTime;
                ppEventContact.SetValue( reservation.EventContactPersonAlias != null ? reservation.EventContactPersonAlias.Person : null );
                ppAdministrativeContact.SetValue( reservation.AdministrativeContactPersonAlias != null ? reservation.AdministrativeContactPersonAlias.Person : null );

                pnEventContactPhone.Text = reservation.EventContactPhone;
                tbEventContactEmail.Text = reservation.EventContactEmail;

                pnAdministrativeContactPhone.Text = reservation.AdministrativeContactPhone;
                tbAdministrativeContactEmail.Text = reservation.AdministrativeContactEmail;

                BindReservationLocationsGrid();
                if ( LocationsState.Any() )
                {
                    wpLocations.Expanded = true;
                }

                BindReservationResourcesGrid();
                if ( ResourcesState.Any() )
                {
                    wpResources.Expanded = true;
                }

                foreach ( var reservationLocation in LocationsState )
                {
                    reservationLocation.IsNew = true;
                }

                foreach ( var reservationResource in ResourcesState )
                {
                    reservationResource.IsNew = true;
                }

                LoadQuestionsAndAnswers( resetControls: true );

                ddlCampus.Items.Clear();
                ddlCampus.Items.Add( new ListItem( string.Empty, string.Empty ) );

                foreach ( var campus in CampusCache.All() )
                {
                    ddlCampus.Items.Add( new ListItem( campus.Name, campus.Id.ToString().ToUpper() ) );
                }
                ddlCampus.SetValue( reservation.CampusId );

                ddlMinistry.Items.Clear();
                ddlMinistry.Items.Add( new ListItem( string.Empty, string.Empty ) );

                foreach ( var ministry in new ReservationMinistryService( rockContext ).Queryable().AsNoTracking().OrderBy( m => m.Name ).ToList() )
                {
                    ddlMinistry.Items.Add( new ListItem( ministry.Name, ministry.Id.ToString().ToUpper() ) );
                }
                ddlMinistry.SetValue( reservation.ReservationMinistryId );

                ddlReservationType.Items.Clear();
                foreach ( var reservationType in new ReservationTypeService( rockContext ).Queryable().AsNoTracking().OrderBy( m => m.Name ).ToList() )
                {
                    ddlReservationType.Items.Add( new ListItem( reservationType.Name, reservationType.Id.ToString().ToUpper() ) );
                }
                ddlReservationType.SetValue( ReservationType.Id );

                bool canApprove = false;

                if ( !canApprove )
                {
                    canApprove = ReservationTypeService.IsPersonInGroupWithId( CurrentPerson, ReservationType.SuperAdminGroupId );
                }

                if ( !canApprove )
                {
                    canApprove = ReservationTypeService.IsPersonInGroupWithId( CurrentPerson, ReservationType.FinalApprovalGroupId );
                }

                if ( reservation.Id != 0 )
                {
                    if ( canApprove )
                    {
                        pnlEditApprovalState.Visible = true;
                        pnlReadApprovalState.Visible = false;

                        PendingCss = ( reservation.ApprovalState == ReservationApprovalState.ChangesNeeded ||
                                        reservation.ApprovalState == ReservationApprovalState.PendingReview ||
                                        reservation.ApprovalState == ReservationApprovalState.Unapproved )
                                        ? "btn-default active" : "btn-default";
                        ApprovedCss = reservation.ApprovalState == ReservationApprovalState.Approved ? "btn-success active" : "btn-default";
                        DeniedCss = reservation.ApprovalState == ReservationApprovalState.Denied ? "btn-danger active" : "btn-default";
                    }
                    else
                    {
                        pnlEditApprovalState.Visible = false;
                        pnlReadApprovalState.Visible = true;
                        lApprovalState.Text = hlStatus.Text = reservation.ApprovalState.ConvertToString();
                        switch ( reservation.ApprovalState )
                        {
                            case ReservationApprovalState.Approved:
                                hlStatus.LabelType = LabelType.Success;
                                break;
                            case ReservationApprovalState.Denied:
                                hlStatus.LabelType = LabelType.Danger;
                                break;
                            case ReservationApprovalState.PendingReview:
                                hlStatus.LabelType = LabelType.Warning;
                                break;
                            case ReservationApprovalState.Unapproved:
                                hlStatus.LabelType = LabelType.Warning;
                                break;
                            case ReservationApprovalState.ChangesNeeded:
                                hlStatus.LabelType = LabelType.Info;
                                break;
                            default:
                                hlStatus.LabelType = LabelType.Default;
                                break;
                        }
                    }
                }

                hfApprovalState.Value = reservation.ApprovalState.ConvertToString();
            }
        }

        /// <summary>
        /// Loads the questions and answers.
        /// </summary>
        /// <param name="locationList">The location list.</param>
        /// <param name="resourceList">The resource list.</param>
        private void LoadQuestionsAndAnswers( bool isEditMode = true, bool resetControls = false )
        {
            var rockContext = new RockContext();
            var locationService = new LocationService( rockContext );
            var resourceService = new ResourceService( rockContext );
            var reservationService = new ReservationService( rockContext );

            foreach ( var reservationLocation in LocationsState )
            {
                reservationLocation.Reservation = reservationService.Get( reservationLocation.ReservationId );
                reservationLocation.Location = locationService.Get( reservationLocation.LocationId );
            }

            foreach ( var reservationResource in ResourcesState )
            {
                reservationResource.Reservation = reservationService.Get( reservationResource.ReservationId );
                reservationResource.Resource = resourceService.Get( reservationResource.ResourceId );
            }

            BuildLocationQuestions( isEditMode, resetControls );

            BuildResourceQuestions( isEditMode, resetControls );
        }

        /// <summary>
        /// Builds the location questions.
        /// </summary>
        /// <param name="isEditMode">if set to <c>true</c> [is edit mode].</param>
        /// <param name="resetControls">if set to <c>true</c> [reset controls].</param>
        private void BuildLocationQuestions( bool isEditMode, bool resetControls )
        {
            if ( resetControls )
            {
                phLocationAnswers.Controls.Clear();
                phViewLocationAnswers.Controls.Clear();
            }

            foreach ( var reservationLocation in LocationsState )
            {
                Control headControl = null;
                if ( isEditMode )
                {
                    headControl = phLocationAnswers.FindControl( "cReservationLocation_" + reservationLocation.Guid.ToString() ) as Control;
                }
                else
                {
                    headControl = phViewLocationAnswers.FindControl( "cReservationLocation_" + reservationLocation.Guid.ToString() ) as Control;
                }

                if ( headControl == null )
                {
                    reservationLocation.LoadReservationLocationAttributes();
                    if ( reservationLocation.Attributes.Count > 0 )
                    {
                        Control childControl = new Control();
                        HiddenField hfReservationLocationGuid = new HiddenField();
                        PlaceHolder phAttributes = new PlaceHolder();
                        var headingTitle = new HtmlGenericControl( "h3" );

                        headingTitle.InnerText = reservationLocation.Location.Name;
                        hfReservationLocationGuid.Value = reservationLocation.Guid.ToString();

                        childControl.ID = "cReservationLocation_" + reservationLocation.Guid.ToString();
                        hfReservationLocationGuid.ID = "hfReservationLocationGuid_" + reservationLocation.Guid.ToString();
                        phAttributes.ID = "phAttributes_" + reservationLocation.Guid.ToString();

                        if ( isEditMode )
                        {
                            Rock.Attribute.Helper.AddEditControls( reservationLocation, phAttributes, reservationLocation.IsNew, BlockValidationGroup );
                            reservationLocation.IsNew = false;
                        }
                        else
                        {
                            Rock.Attribute.Helper.AddDisplayControls( reservationLocation, phAttributes, showHeading: false );
                        }

                        childControl.Controls.Add( headingTitle );
                        childControl.Controls.Add( hfReservationLocationGuid );
                        childControl.Controls.Add( phAttributes );

                        if ( isEditMode )
                        {
                            phLocationAnswers.Controls.Add( childControl );
                        }
                        else
                        {
                            phViewLocationAnswers.Controls.Add( childControl );
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Builds the resource questions.
        /// </summary>
        /// <param name="isEditMode">if set to <c>true</c> [is edit mode].</param>
        /// <param name="resetControls">if set to <c>true</c> [reset controls].</param>
        private void BuildResourceQuestions( bool isEditMode, bool resetControls )
        {
            if ( resetControls )
            {
                phResourceAnswers.Controls.Clear();
                phViewResourceAnswers.Controls.Clear();
            }

            foreach ( var reservationResource in ResourcesState )
            {
                Control headControl = null;
                if ( isEditMode )
                {
                    headControl = phResourceAnswers.FindControl( "cReservationResource_" + reservationResource.Guid.ToString() ) as Control;
                }
                else
                {
                    headControl = phViewResourceAnswers.FindControl( "cReservationResource_" + reservationResource.Guid.ToString() ) as Control;
                }

                if ( headControl == null )
                {
                    reservationResource.LoadReservationResourceAttributes();
                    if ( reservationResource.Attributes.Count > 0 )
                    {
                        Control childControl = new Control();
                        HiddenField hfReservationResourceGuid = new HiddenField();
                        PlaceHolder phAttributes = new PlaceHolder();
                        var headingTitle = new HtmlGenericControl( "h3" );

                        headingTitle.InnerText = reservationResource.Resource.Name;
                        hfReservationResourceGuid.Value = reservationResource.Guid.ToString();

                        childControl.ID = "cReservationResource_" + reservationResource.Guid.ToString();
                        hfReservationResourceGuid.ID = "hfReservationResourceGuid_" + reservationResource.Guid.ToString();

                        phAttributes.ID = "phAttributes_" + reservationResource.Guid.ToString();

                        if ( isEditMode )
                        {
                            Rock.Attribute.Helper.AddEditControls( reservationResource, phAttributes, reservationResource.IsNew, BlockValidationGroup );
                            reservationResource.IsNew = false;
                        }
                        else
                        {
                            Rock.Attribute.Helper.AddDisplayControls( reservationResource, phAttributes, showHeading: false );
                        }

                        childControl.Controls.Add( headingTitle );
                        childControl.Controls.Add( hfReservationResourceGuid );
                        childControl.Controls.Add( phAttributes );

                        if ( isEditMode )
                        {
                            phResourceAnswers.Controls.Add( childControl );
                        }
                        else
                        {
                            phViewResourceAnswers.Controls.Add( childControl );
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Handles the SelectPerson event of the ppEventContact control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void ppEventContact_SelectPerson( object sender, EventArgs e )
        {
            if ( ppEventContact.PersonId.HasValue )
            {
                using ( var rockContext = new RockContext() )
                {
                    Guid mobilePhoneGuid = Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_MOBILE.AsGuid();
                    Guid homePhoneGuid = Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_HOME.AsGuid();
                    Guid workPhoneGuid = Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_WORK.AsGuid();
                    var contactInfo = new PersonService( rockContext )
                        .Queryable().AsNoTracking()
                        .Where( p => p.Id == ppEventContact.PersonId.Value )
                        .Select( p => new
                        {
                            Email = p.Email,
                            Phone = p.PhoneNumbers
                                .Where( n => n.NumberTypeValue.Guid.Equals( mobilePhoneGuid ) || n.NumberTypeValue.Guid.Equals( homePhoneGuid ) || n.NumberTypeValue.Guid.Equals( workPhoneGuid ) )
                                .OrderBy( n => n.NumberTypeValue.Order )
                                .Select( n => n.NumberFormatted )
                                .FirstOrDefault()
                        } )
                        .FirstOrDefault();

                    if ( contactInfo != null && !string.IsNullOrWhiteSpace( contactInfo.Email ) )
                    {
                        tbEventContactEmail.Text = contactInfo.Email;
                    }

                    if ( contactInfo != null && !string.IsNullOrWhiteSpace( contactInfo.Phone ) )
                    {
                        pnEventContactPhone.Text = contactInfo.Phone;
                    }
                }
            }
        }

        /// <summary>
        /// Handles the SelectPerson event of the ppAdministrativeContact control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void ppAdministrativeContact_SelectPerson( object sender, EventArgs e )
        {
            if ( ppAdministrativeContact.PersonId.HasValue )
            {
                using ( var rockContext = new RockContext() )
                {
                    Guid mobilePhoneGuid = Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_MOBILE.AsGuid();
                    Guid homePhoneGuid = Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_HOME.AsGuid();
                    Guid workPhoneGuid = Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_WORK.AsGuid();
                    var contactInfo = new PersonService( rockContext )
                        .Queryable().AsNoTracking()
                        .Where( p => p.Id == ppAdministrativeContact.PersonId.Value )
                        .Select( p => new
                        {
                            Email = p.Email,
                            Phone = p.PhoneNumbers
                                .Where( n => n.NumberTypeValue.Guid.Equals( mobilePhoneGuid ) || n.NumberTypeValue.Guid.Equals( homePhoneGuid ) || n.NumberTypeValue.Guid.Equals( workPhoneGuid ) )
                                .OrderBy( n => n.NumberTypeValue.Order )
                                .Select( n => n.NumberFormatted )
                                .FirstOrDefault()
                        } )
                        .FirstOrDefault();

                    if ( contactInfo != null && !string.IsNullOrWhiteSpace( contactInfo.Email ) )
                    {
                        tbAdministrativeContactEmail.Text = contactInfo.Email;
                    }

                    if ( contactInfo != null && !string.IsNullOrWhiteSpace( contactInfo.Phone ) )
                    {
                        pnAdministrativeContactPhone.Text = contactInfo.Phone;
                    }
                }
            }
        }

        /// <summary>
        /// Adds (to the state object) any resources attached to the given wpLocations 
        /// </summary>
        /// <param name="locationid">The location identifier.</param>
        protected void AddAttachedResources( int locationId, Reservation reservation = null )
        {
            var attachedResources = new ResourceService( new RockContext() ).Queryable().Where( r => r.LocationId == locationId );
            if ( attachedResources.Any() )
            {
                foreach ( var resource in attachedResources )
                {
                    var reservationResource = new ReservationResourceSummary();
                    reservationResource.ResourceId = resource.Id;
                    // Do you always get all the quantity of this resource for "attached" resources? I can't see it any other way.
                    reservationResource.Quantity = resource.Quantity;
                    reservationResource.ApprovalState = ReservationResourceApprovalState.Unapproved;
                    reservationResource.IsNew = true;

                    // ResourcesState will be null when this method is being called
                    // from another page that passed in a location that has attached resources
                    // therefore we'll just add it to the reservation and not the state.
                    if ( ResourcesState != null )
                    {
                        ResourcesState.Add( reservationResource );
                    }
                    else if ( reservation != null )
                    {
                        reservation.ReservationResources.Add( reservationResource as ReservationResource );
                    }
                }

                if ( ResourcesState != null )
                {
                    BindReservationResourcesGrid();
                    wpResources.Expanded = true;
                }
            }
        }

        /// <summary>
        /// Adds (to the state object) any locations attached to the given reqource 
        /// </summary>
        /// <param name="resourceId">The resource identifier.</param>
        protected void AddAttachedLocations( int resourceId, Reservation reservation = null )
        {
            var attachedResources = new ResourceService( new RockContext() ).Queryable().Where( r => r.Id == resourceId );
            if ( attachedResources.Any() )
            {
                foreach ( var resource in attachedResources )
                {
                    // bug fix:
                    if ( !resource.LocationId.HasValue )
                    {
                        continue;
                    }

                    var reservationLocation = new ReservationLocationSummary();
                    reservationLocation.LocationId = resource.LocationId.Value;
                    reservationLocation.ApprovalState = ReservationLocationApprovalState.Unapproved;
                    reservationLocation.IsNew = true;

                    // LocationsState will be null when this method is being called
                    // from another page that passed in a resource that has attached locations
                    // therefore we'll just add it to the reservation and not the state.
                    if ( LocationsState != null )
                    {
                        LocationsState.Add( reservationLocation );
                    }
                    else if ( reservation != null )
                    {
                        reservation.ReservationLocations.Add( reservationLocation );
                    }
                }

                if ( LocationsState != null )
                {
                    BindReservationLocationsGrid();
                    wpLocations.Expanded = true;
                }
            }
        }

        /// <summary>
        /// Returns the user to the parent page
        /// </summary>
        protected void ReturnToParentPage()
        {
            Dictionary<string, string> dictionaryInfo = new Dictionary<string, string>();
            dictionaryInfo.Add( "CalendarDate", PageParameter( "CalendarDate" ) );
            NavigateToParentPage( dictionaryInfo );
        }

        /// <summary>
        /// Loads the pickers.
        /// </summary>
        private void LoadPickers()
        {
            srpResource.Enabled = true;
            slpLocation.Enabled = true;
            int reservationId = hfReservationId.Value.AsInteger();

            // Get the selected locations and pass them as extra params to the Resource rest call so
            // we don't get any resources that are attached to other/non-selected locations.
            var locationIds = LocationsState.Select( a => a.LocationId ).ToList().AsDelimited( "," );

            string encodedCalendarContent = Uri.EscapeUriString( sbSchedule.iCalendarContent );
            srpResource.CampusId = ddlCampus.SelectedValueAsInt();
            srpResource.ItemRestUrlExtraParams = BaseResourceRestUrl + String.Format( "&reservationId={0}&iCalendarContent={1}&setupTime={2}&cleanupTime={3}{4}", reservationId, encodedCalendarContent, nbSetupTime.Text.AsInteger(), nbCleanupTime.Text.AsInteger(), string.IsNullOrWhiteSpace( locationIds ) ? "" : "&locationIds=" + locationIds );
            slpLocation.ItemRestUrlExtraParams = BaseLocationRestUrl + String.Format( "?reservationId={0}&iCalendarContent={1}&setupTime={2}&cleanupTime={3}&attendeeCount={4}", reservationId, encodedCalendarContent, nbSetupTime.Text.AsInteger(), nbCleanupTime.Text.AsInteger(), nbAttending.Text.AsInteger() );
        }

        /// <summary>
        /// Loads the location image.
        /// </summary>
        private void LoadLocationImage()
        {
            lImage.Text = string.Empty;
            if ( slpLocation.SelectedValueAsId().HasValue )
            {
                var location = new LocationService( new RockContext() ).Get( slpLocation.SelectedValueAsId().Value );
                if ( location != null && location.ImageId != null )
                {
                    string imgTag = string.Format( "<img src='{0}GetImage.ashx?id={1}&maxwidth=200&maxheight=200'/>", VirtualPathUtility.ToAbsolute( "~/" ), location.ImageId.Value );

                    string imgUrl = string.Format( "~/GetImage.ashx?id={0}", location.ImageId );
                    if ( System.Web.HttpContext.Current != null )
                    {
                        imgUrl = VirtualPathUtility.ToAbsolute( imgUrl );
                    }

                    lImage.Text = string.Format( "<a href='{0}' target='_blank'>{1}</a>", imgUrl, imgTag );
                }
            }
        }

        /// <summary>
        /// Loads the location conflict message when using the location editor modal.
        /// </summary>
        private void LoadLocationConflictMessage()
        {
            if ( slpLocation.SelectedValueAsId().HasValue )
            {
                StringBuilder sb = new StringBuilder();
                var rockContext = new RockContext();

                var locationId = slpLocation.SelectedValueAsId().Value;
                var location = new LocationService( rockContext ).Get( locationId );

                int reservationId = hfReservationId.ValueAsInt();
                var newReservation = new Reservation() { Id = reservationId, Schedule = new Schedule() { iCalendarContent = sbSchedule.iCalendarContent }, SetupTime = nbSetupTime.Text.AsInteger(), CleanupTime = nbCleanupTime.Text.AsInteger() };
                var message = new ReservationService( rockContext ).BuildLocationConflictHtmlList( newReservation, locationId, this.CurrentPageReference.Route );

                if ( message != null )
                {
                    nbLocationConflicts.Text = string.Format( "{0} is already reserved for the scheduled times by the following reservations:<ul>{1}</ul>", location.Name, message );
                    nbLocationConflicts.Visible = true;
                }
                else
                {
                    nbLocationConflicts.Visible = false;
                }
            }
            else
            {
                nbLocationConflicts.Visible = false;
            }
        }

        /// <summary>
        /// Loads the resource conflict message when using the resource editor modal.
        /// </summary>
        private void LoadResourceConflictMessage()
        {
            if ( srpResource.SelectedValueAsId().HasValue )
            {
                var rockContext = new RockContext();
                var resourceId = srpResource.SelectedValueAsId().Value;
                var resource = new ResourceService( rockContext ).Get( resourceId );

                int reservationId = hfReservationId.ValueAsInt();
                var newReservation = new Reservation() { Id = reservationId, Schedule = new Schedule() { iCalendarContent = sbSchedule.iCalendarContent }, SetupTime = nbSetupTime.Text.AsInteger(), CleanupTime = nbCleanupTime.Text.AsInteger() };

                var conflicts = new ReservationService( rockContext ).GetConflictsForResourceId( resource.Id, newReservation );
                if ( conflicts.Any() )
                {
                    StringBuilder sb = new StringBuilder();
                    var route = this.CurrentPageReference.Route; // is either "/page/123" or "ReservationDetail"
                    route = route.StartsWith( "/" ) ? route : "/" + route;

                    sb.AppendFormat( "{0} is already reserved for the scheduled times by the following reservations:<ul>", resource.Name );
                    foreach ( var conflict in conflicts )
                    {
                        var duration = conflict.Reservation.Schedule.GetCalenderEvent().Duration;
                        int hours = duration.Hours;
                        int minutes = duration.Minutes;

                        sb.AppendFormat( "<li>{0} reserved on {1} {4} via <a href='{5}?ReservationId={2}' target='_blank'>'{3}'</a></li>",
                            conflict.ResourceQuantity,
                            conflict.Reservation.Schedule.ToFriendlyScheduleText(),
                            conflict.ReservationId,
                            conflict.Reservation.Name,
                            ( ( hours <= 0 ) ? string.Empty : hours + ( ( hours == 1 ) ? " hr " : " hrs " ) ) + ( ( minutes == 0 ) ? string.Empty : minutes + " min " ),
                            route
                            );
                    }
                    sb.Append( "</ul>" );
                    nbResourceConflicts.Text = sb.ToString();
                    nbResourceConflicts.Visible = true;
                }
                else
                {
                    nbResourceConflicts.Visible = false;
                }
            }
            else
            {
                nbResourceConflicts.Visible = false;
            }
        }

        /// <summary>
        /// Sets the edit mode.
        /// </summary>
        /// <param name="editable">if set to <c>true</c> [editable].</param>
        private void SetEditMode( bool editable )
        {
            pnlEditDetails.Visible = editable;
            pnlViewDetails.Visible = !editable;

            this.HideSecondaryBlocks( editable );
        }

        /// <summary>
        /// Generates the new reservation.
        /// </summary>
        /// <param name="rockContext">The rock context.</param>
        /// <returns></returns>
        private Reservation GenerateNewReservation( RockContext rockContext )
        {
            Reservation reservation;
            pdAuditDetails.Visible = false;
            reservation = new Reservation { Id = 0 };

            // Auto fill only the Administrative Contact section with the Current Person's details...
            reservation.AdministrativeContactPersonAlias = CurrentPersonAlias;
            reservation.AdministrativeContactPersonAliasId = CurrentPersonAliasId;
            reservation.AdministrativeContactEmail = CurrentPerson.Email;

            Guid workPhoneValueGuid = new Guid( Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_WORK );
            var workPhone = CurrentPerson.PhoneNumbers.Where( p => p.NumberTypeValue.Guid == workPhoneValueGuid ).FirstOrDefault();
            if ( workPhone != null )
            {
                reservation.AdministrativeContactPhone = workPhone.NumberFormatted;
            }
            else
            {
                // Try using their mobile number
                Guid mobilePhoneValueGuid = new Guid( Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_MOBILE );
                var mobilePhone = CurrentPerson.PhoneNumbers.Where( p => p.NumberTypeValue.Guid == mobilePhoneValueGuid ).FirstOrDefault();
                if ( mobilePhone != null )
                {
                    reservation.AdministrativeContactPhone = mobilePhone.NumberFormatted;
                }
            }

            var reservationTypeService = new ReservationTypeService( rockContext );
            if ( PageParameter( "ReservationTypeId" ).AsInteger() != 0 )
            {
                ReservationType = reservationTypeService.Get( PageParameter( "ReservationTypeId" ).AsInteger() );
            }

            if ( ReservationType == null )
            {
                ReservationType = reservationTypeService.Get( "E443F926-0882-41D5-91EF-480EA366F660".AsGuid() );
            }

            reservation.ReservationType = ReservationType;

            if ( PageParameter( "LocationId" ).AsInteger() != 0 )
            {
                ReservationLocation reservationLocation = new ReservationLocation();
                reservationLocation.LocationId = PageParameter( "LocationId" ).AsInteger();
                reservationLocation.ApprovalState = ReservationLocationApprovalState.Unapproved;

                // set the campus based on the location that was passed in:
                var location = new LocationService( new RockContext() ).Get( reservationLocation.LocationId );
                if ( location != null )
                {
                    reservation.CampusId = location.CampusId;
                }

                reservation.ReservationLocations.Add( reservationLocation );

                // Add any attached resources...
                AddAttachedResources( reservationLocation.LocationId, reservation );
            }

            if ( PageParameter( "ResourceId" ).AsInteger() != 0 )
            {
                ReservationResource reservationResource = new ReservationResource();
                reservationResource.ResourceId = PageParameter( "ResourceId" ).AsInteger();
                reservationResource.Quantity = 1;
                reservationResource.ApprovalState = ReservationResourceApprovalState.Unapproved;

                // set the campus based on the resource that was passed in:
                var resource = new ResourceService( new RockContext() ).Get( reservationResource.ResourceId );
                if ( resource != null )
                {
                    reservation.CampusId = resource.CampusId;
                }

                reservation.ReservationResources.Add( reservationResource );

                // Add any attached locations...
                AddAttachedLocations( reservationResource.ResourceId, reservation );
            }

            return reservation;
        }


        /// <summary>
        /// Builds the old reservation.
        /// </summary>
        /// <param name="resourceService">The resource service.</param>
        /// <param name="locationService">The location service.</param>
        /// <param name="reservationService">The reservation service.</param>
        /// <param name="reservation">The reservation.</param>
        /// <returns></returns>
        private static Reservation BuildOldReservation( ResourceService resourceService, LocationService locationService, ReservationService reservationService, Reservation reservation )
        {
            var oldReservation = new Reservation();
            oldReservation.Schedule = reservation.Schedule ?? new Schedule();
            if ( reservation.Schedule != null )
            {
                oldReservation.Schedule.iCalendarContent = reservation.Schedule.iCalendarContent;
            }
            oldReservation.ApprovalState = reservation.ApprovalState;
            oldReservation.ReservationLocations = new List<ReservationLocation>();
            oldReservation.ReservationResources = new List<ReservationResource>();

            foreach ( var reservationLocation in reservation.ReservationLocations )
            {
                ReservationLocation oldReservationLocation = new ReservationLocation();
                oldReservation.ReservationLocations.Add( oldReservationLocation );
                oldReservationLocation.CopyPropertiesFrom( reservationLocation );
                oldReservationLocation.Reservation = reservationService.Get( oldReservation.Id );
                oldReservationLocation.Location = locationService.Get( reservationLocation.LocationId );
                oldReservationLocation.ReservationId = reservation.Id;
            }

            foreach ( var reservationResource in reservation.ReservationResources )
            {
                ReservationResource oldReservationResource = new ReservationResource();
                oldReservation.ReservationResources.Add( oldReservationResource );
                oldReservationResource.CopyPropertiesFrom( reservationResource as ReservationResource );
                oldReservationResource.Reservation = reservationService.Get( oldReservation.Id );
                oldReservationResource.Resource = resourceService.Get( oldReservationResource.ResourceId );
                oldReservationResource.ReservationId = reservation.Id;
            }

            return oldReservation;
        }

        /// <summary>
        /// Evaluates the location and resource changes.
        /// </summary>
        /// <param name="changes">The changes.</param>
        /// <param name="oldReservation">The old reservation.</param>
        /// <param name="reservation">The reservation.</param>
        /// <returns></returns>
        private List<string> EvaluateLocationAndResourceChanges( List<string> changes, Reservation oldReservation, Reservation reservation )
        {
            foreach ( var reservationLocation in reservation.ReservationLocations )
            {
                var oldReservationLocation = oldReservation.ReservationLocations.Where( rl => rl.Guid == reservationLocation.Guid ).FirstOrDefault();

                if ( oldReservationLocation != null )
                {
                    History.EvaluateChange( changes, String.Format( "{0} Approval State", reservationLocation.Location.Name ), oldReservationLocation.ApprovalState.ToString(), reservationLocation.ApprovalState.ToString() );
                    if ( reservationLocation.Attributes != null )
                    {
                        oldReservationLocation.LoadAttributes();
                        foreach ( var attribute in reservationLocation.Attributes.Select( a => a.Value ) )
                        {
                            string originalValue = oldReservationLocation.AttributeValues.ContainsKey( attribute.Key ) ? oldReservationLocation.AttributeValues[attribute.Key].Value : string.Empty;
                            string newValue = reservationLocation.AttributeValues.ContainsKey( attribute.Key ) ? reservationLocation.AttributeValues[attribute.Key].Value : string.Empty;
                            if ( newValue != originalValue )
                            {
                                string originalFormattedValue = attribute.FieldType.Field.FormatValue( null, originalValue, attribute.QualifierValues, false );
                                string newFormattedValue = attribute.FieldType.Field.FormatValue( null, newValue, attribute.QualifierValues, false );
                                History.EvaluateChange( changes, String.Format( "({0}) {1}", reservationLocation.Location.Name, attribute.Name ), originalFormattedValue, newFormattedValue );
                            }
                        }
                    }
                }
                else
                {
                    changes.Add( String.Format( "Added <span class='field-name'>{0}</span>", reservationLocation.Location.Name ) );
                }
            }

            foreach ( var reservationResource in reservation.ReservationResources )
            {
                var oldReservationResource = oldReservation.ReservationResources.Where( rl => rl.Guid == reservationResource.Guid ).FirstOrDefault();

                if ( oldReservationResource != null )
                {
                    History.EvaluateChange( changes, String.Format( "{0} Approval State", reservationResource.Resource.Name ), oldReservationResource.ApprovalState.ToString(), reservationResource.ApprovalState.ToString() );
                    History.EvaluateChange( changes, String.Format( "{0} Quantity", reservationResource.Resource.Name ), oldReservationResource.Quantity.ToString(), reservationResource.Quantity.ToString() );

                    if ( reservationResource.Attributes != null )
                    {
                        oldReservationResource.LoadAttributes();
                        foreach ( var attribute in reservationResource.Attributes.Select( a => a.Value ) )
                        {
                            string originalValue = oldReservationResource.AttributeValues.ContainsKey( attribute.Key ) ? oldReservationResource.AttributeValues[attribute.Key].Value : string.Empty;
                            string newValue = reservationResource.AttributeValues.ContainsKey( attribute.Key ) ? reservationResource.AttributeValues[attribute.Key].Value : string.Empty;
                            if ( newValue != originalValue )
                            {
                                string originalFormattedValue = attribute.FieldType.Field.FormatValue( null, originalValue, attribute.QualifierValues, false );
                                string newFormattedValue = attribute.FieldType.Field.FormatValue( null, newValue, attribute.QualifierValues, false );
                                History.EvaluateChange( changes, String.Format( "({0}) {1}", reservationResource.Resource.Name, attribute.Name ), originalFormattedValue, newFormattedValue );
                            }
                        }
                    }
                }
                else
                {
                    changes.Add( String.Format( "Added <span class='field-name'>{0} {1}</span>", reservationResource.Quantity, reservationResource.Resource.Name ) );
                }
            }

            return changes;
        }

        /// <summary>
        /// Launches the workflow.
        /// </summary>
        /// <param name="rockContext">The rock context.</param>
        /// <param name="reservation">The reservation.</param>
        /// <param name="reservationWorkflowTrigger">The reservation workflow trigger.</param>
        private void LaunchWorkflow( RockContext rockContext, Reservation reservation, ReservationWorkflowTrigger reservationWorkflowTrigger )
        {
            if ( reservation != null && reservationWorkflowTrigger != null )
            {
                var workflowType = WorkflowTypeCache.Read( reservationWorkflowTrigger.WorkflowTypeId.Value );
                if ( workflowType != null && ( workflowType.IsActive ?? true ) )
                {
                    var workflow = Rock.Model.Workflow.Activate( workflowType, reservationWorkflowTrigger.WorkflowType.WorkTerm, rockContext );
                    if ( workflow != null )
                    {
                        var workflowService = new Rock.Model.WorkflowService( rockContext );

                        List<string> workflowErrors;
                        if ( workflowService.Process( workflow, reservation, out workflowErrors ) )
                        {
                            if ( workflow.Id != 0 )
                            {
                                ReservationWorkflow reservationWorkflow = new ReservationWorkflow();
                                reservationWorkflow.ReservationId = reservation.Id;
                                reservationWorkflow.WorkflowId = workflow.Id;
                                reservationWorkflow.ReservationWorkflowTriggerId = reservationWorkflowTrigger.Id;
                                reservationWorkflow.TriggerType = reservationWorkflowTrigger.TriggerType;
                                reservationWorkflow.TriggerQualifier = reservationWorkflowTrigger.QualifierValue;
                                new ReservationWorkflowService( rockContext ).Add( reservationWorkflow );

                                rockContext.SaveChanges();

                                if ( workflow.HasActiveEntryForm( CurrentPerson ) )
                                {
                                    var qryParam = new Dictionary<string, string>();
                                    qryParam.Add( "WorkflowTypeId", workflowType.Id.ToString() );
                                    qryParam.Add( "WorkflowId", workflow.Id.ToString() );
                                    NavigateToLinkedPage( "WorkflowEntryPage", qryParam );
                                }
                                else
                                {
                                    mdWorkflowLaunched.Show( string.Format( "A '{0}' workflow has been started.",
                                        workflowType.Name ), ModalAlertType.Information );
                                }

                            }
                            else
                            {
                                mdWorkflowLaunched.Show( string.Format( "A '{0}' workflow was processed.",
                                    workflowType.Name ), ModalAlertType.Information );
                            }
                        }
                        else
                        {
                            mdWorkflowLaunched.Show( "Workflow Processing Error(s):<ul><li>" + workflowErrors.AsDelimited( "</li><li>" ) + "</li></ul>", ModalAlertType.Information );
                        }
                        ShowDetail( hfReservationId.ValueAsInt() );
                    }
                }
            }
        }

        /// <summary>
        /// Saves the reservation location.
        /// </summary>
        private void SaveReservationLocation()
        {
            ReservationLocationSummary reservationLocation = null;
            Guid guid = hfAddReservationLocationGuid.Value.AsGuid();
            if ( !guid.IsEmpty() )
            {
                reservationLocation = LocationsState.FirstOrDefault( l => l.Guid.Equals( guid ) );
            }

            if ( reservationLocation == null )
            {
                reservationLocation = new ReservationLocationSummary();
                reservationLocation.IsNew = true;
            }

            try
            {
                reservationLocation.Location = new LocationService( new RockContext() ).Get( slpLocation.SelectedValueAsId().Value );
            }
            catch { }

            reservationLocation.ApprovalState = ReservationLocationApprovalState.Unapproved;
            reservationLocation.LocationId = slpLocation.SelectedValueAsId().Value;
            reservationLocation.ReservationId = 0;

            if ( !reservationLocation.IsValid )
            {
                return;
            }

            if ( LocationsState.Any( a => a.Guid.Equals( reservationLocation.Guid ) ) )
            {
                LocationsState.RemoveEntity( reservationLocation.Guid );
            }

            // Add any location attached resources to the Resources grid for the location that was just selected.
            AddAttachedResources( reservationLocation.LocationId );

            LocationsState.Add( reservationLocation );
            BindReservationLocationsGrid();

            // Re load the pickers because changing a location should include/exclude resources attached
            // to locations.
            LoadPickers();
            LoadQuestionsAndAnswers();
        }

        /// <summary>
        /// Saves the reservation resource.
        /// </summary>
        private void SaveReservationResource()
        {
            if ( nbQuantity.Text.AsInteger() > 0 )
            {
                ReservationResourceSummary reservationResource = null;
                Guid guid = hfAddReservationResourceGuid.Value.AsGuid();
                if ( !guid.IsEmpty() )
                {
                    reservationResource = ResourcesState.FirstOrDefault( l => l.Guid.Equals( guid ) );
                }

                if ( reservationResource == null )
                {
                    reservationResource = new ReservationResourceSummary();
                    reservationResource.IsNew = true;
                }

                try
                {
                    reservationResource.Resource = new ResourceService( new RockContext() ).Get( srpResource.SelectedValueAsId().Value );
                }
                catch { }

                reservationResource.ApprovalState = ReservationResourceApprovalState.Unapproved;
                reservationResource.ResourceId = srpResource.SelectedValueAsId().Value;
                reservationResource.Quantity = nbQuantity.Text.AsInteger();
                reservationResource.ReservationId = 0;

                if ( !reservationResource.IsValid )
                {
                    return;
                }

                if ( ResourcesState.Any( a => a.Guid.Equals( reservationResource.Guid ) ) )
                {
                    ResourcesState.RemoveEntity( reservationResource.Guid );
                }

                ResourcesState.Add( reservationResource );
            }

            BindReservationResourcesGrid();
            LoadQuestionsAndAnswers();
        }

        #endregion

        #region Helper Classes
        private class ReservationResourceSummary : ReservationResource
        {
            public bool IsNew { get; set; }
        }

        private class ReservationLocationSummary : ReservationLocation
        {
            public bool IsNew { get; set; }
        }
        #endregion
    }
}
