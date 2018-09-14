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
using System.Data.Entity;
using System.Linq;
using System.Web.UI;
using System.Web.UI.WebControls;

using Newtonsoft.Json;

using Rock;
using Rock.Attribute;
using Rock.Constants;
using Rock.Data;
using Rock.Model;
using Rock.Security;
using Rock.Web;
using Rock.Web.Cache;
using Rock.Web.UI;
using Rock.Web.UI.Controls;
using Attribute = Rock.Model.Attribute;
using com.centralaz.RoomManagement.Model;

namespace RockWeb.Plugins.com_centralaz.RoomManagement
{
    [DisplayName( "Reservation Type Detail" )]
    [Category( "com_centralaz > Room Management" )]
    [Description( "Displays the details of the given Reservation Type for editing." )]
    public partial class ReservationTypeDetail : RockBlock, IDetailBlock
    {
        #region Properties

        private List<ReservationMinistry> ReservationMinistriesState { get; set; }
        private List<ReservationWorkflowTrigger> ReservationWorkflowTriggersState { get; set; }

        #endregion

        #region Control Methods

        /// <summary>
        /// Restores the view-state information from a previous user control request that was saved by the <see cref="M:System.Web.UI.UserControl.SaveViewState" /> method.
        /// </summary>
        /// <param name="savedState">An <see cref="T:System.Object" /> that represents the user control state to be restored.</param>
        protected override void LoadViewState( object savedState )
        {
            base.LoadViewState( savedState );

            string json = ViewState["ReservationMinistriesState"] as string;
            if ( string.IsNullOrWhiteSpace( json ) )
            {
                ReservationMinistriesState = new List<ReservationMinistry>();
            }
            else
            {
                ReservationMinistriesState = JsonConvert.DeserializeObject<List<ReservationMinistry>>( json );
            }

            json = ViewState["ReservationWorkflowTriggersState"] as string;
            if ( string.IsNullOrWhiteSpace( json ) )
            {
                ReservationWorkflowTriggersState = new List<ReservationWorkflowTrigger>();
            }
            else
            {
                ReservationWorkflowTriggersState = JsonConvert.DeserializeObject<List<ReservationWorkflowTrigger>>( json );
            }
        }

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Init" /> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnInit( EventArgs e )
        {
            base.OnInit( e );
            bool editAllowed = IsUserAuthorized( Authorization.ADMINISTRATE );

            gMinistries.DataKeyNames = new string[] { "Guid" };
            gMinistries.Actions.ShowAdd = true;
            gMinistries.Actions.AddClick += gMinistries_Add;
            gMinistries.GridRebind += gMinistries_GridRebind;

            gWorkflowTriggers.DataKeyNames = new string[] { "Guid" };
            gWorkflowTriggers.Actions.ShowAdd = true;
            gWorkflowTriggers.Actions.AddClick += gWorkflowTriggers_Add;
            gWorkflowTriggers.GridRebind += gWorkflowTriggers_GridRebind;

            btnSecurity.EntityTypeId = EntityTypeCache.Get( typeof( com.centralaz.RoomManagement.Model.ReservationType ) ).Id;

            this.BlockUpdated += Block_BlockUpdated;
            this.AddConfigurationUpdateTrigger( upReservationType );
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
                ShowDetail( PageParameter( "ReservationTypeId" ).AsInteger() );
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

            ViewState["ReservationMinistriesState"] = JsonConvert.SerializeObject( ReservationMinistriesState, Formatting.None, jsonSetting );
            ViewState["ReservationWorkflowTriggersState"] = JsonConvert.SerializeObject( ReservationWorkflowTriggersState, Formatting.None, jsonSetting );

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

            int? reservationTypeId = PageParameter( pageReference, "ReservationTypeId" ).AsIntegerOrNull();
            if ( reservationTypeId != null )
            {
                ReservationType reservationType = new ReservationTypeService( new RockContext() ).Get( reservationTypeId.Value );
                if ( reservationType != null )
                {
                    breadCrumbs.Add( new BreadCrumb( reservationType.Name, pageReference ) );
                }
                else
                {
                    breadCrumbs.Add( new BreadCrumb( "New Reservation Type", pageReference ) );
                }
            }
            else
            {
                // don't show a breadcrumb if we don't have a pageparam to work with
            }

            return breadCrumbs;
        }

        #endregion

        #region Events

        #region Control Events

        /// <summary>
        /// Handles the Click event of the btnEdit control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected void btnEdit_Click( object sender, EventArgs e )
        {
            var rockContext = new RockContext();
            var reservationType = new ReservationTypeService( rockContext ).Get( hfReservationTypeId.Value.AsInteger() );

            ShowEditDetails( reservationType );

        }

        /// <summary>
        /// Handles the Click event of the btnDelete control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected void btnDeleteConfirm_Click( object sender, EventArgs e )
        {
            using ( var rockContext = new RockContext() )
            {
                ReservationWorkflowService reservationWorkflowService = new ReservationWorkflowService( rockContext );
                ReservationTypeService reservationTypeService = new ReservationTypeService( rockContext );
                AuthService authService = new AuthService( rockContext );
                ReservationType reservationType = reservationTypeService.Get( int.Parse( hfReservationTypeId.Value ) );

                if ( reservationType != null )
                {
                    if ( !reservationType.IsAuthorized( Authorization.ADMINISTRATE, this.CurrentPerson ) )
                    {
                        mdDeleteWarning.Show( "You are not authorized to delete this reservation type.", ModalAlertType.Information );
                        return;
                    }

                    // Currently the Cascade delete isn't working, so this is temp code to force the issue.
                    var reservationService = new ReservationService( rockContext );
                    var reservationQry = reservationService.Queryable().Where( r => r.ReservationTypeId == reservationType.Id );
                    reservationService.DeleteRange( reservationQry );

                    reservationTypeService.Delete( reservationType );
                    rockContext.SaveChanges();

                    ReservationWorkflowService.RemoveCachedTriggers();
                }
            }

            NavigateToParentPage();
        }

        /// <summary>
        /// Handles the Click event of the btnDeleteCancel control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void btnDeleteCancel_Click( object sender, EventArgs e )
        {
            btnDelete.Visible = true;
            btnEdit.Visible = true;
            pnlDeleteConfirm.Visible = false;
        }

        /// <summary>
        /// Handles the Click event of the btnDelete control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void btnDelete_Click( object sender, EventArgs e )
        {
            btnDelete.Visible = false;
            btnEdit.Visible = false;
            pnlDeleteConfirm.Visible = true;
        }

        /// <summary>
        /// Handles the Click event of the btnSave control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected void btnSave_Click( object sender, EventArgs e )
        {
            ReservationType reservationType;
            using ( var rockContext = new RockContext() )
            {

                ReservationTypeService reservationTypeService = new ReservationTypeService( rockContext );
                ReservationMinistryService reservationMinistryService = new ReservationMinistryService( rockContext );
                ReservationWorkflowTriggerService reservationWorkflowTriggerService = new ReservationWorkflowTriggerService( rockContext );
                AttributeService attributeService = new AttributeService( rockContext );
                AttributeQualifierService qualifierService = new AttributeQualifierService( rockContext );

                int reservationTypeId = int.Parse( hfReservationTypeId.Value );

                if ( reservationTypeId == 0 )
                {
                    reservationType = new ReservationType();
                    reservationTypeService.Add( reservationType );
                }
                else
                {
                    reservationType = reservationTypeService.Queryable( "ReservationMinistries, ReservationWorkflowTriggers" ).Where( c => c.Id == reservationTypeId ).FirstOrDefault();

                    var uiMinistries = ReservationMinistriesState.Select( r => r.Guid );
                    foreach ( var reservationMinistry in reservationType.ReservationMinistries.Where( r => !uiMinistries.Contains( r.Guid ) ).ToList() )
                    {
                        reservationType.ReservationMinistries.Remove( reservationMinistry );
                        reservationMinistryService.Delete( reservationMinistry );
                    }

                    var uiTriggers = ReservationWorkflowTriggersState.Select( r => r.Guid );
                    foreach ( var reservationWorkflowTrigger in reservationType.ReservationWorkflowTriggers.Where( r => !uiTriggers.Contains( r.Guid ) ).ToList() )
                    {
                        reservationType.ReservationWorkflowTriggers.Remove( reservationWorkflowTrigger );
                        reservationWorkflowTriggerService.Delete( reservationWorkflowTrigger );
                    }
                }

                reservationType.Name = tbName.Text;
                reservationType.Description = tbDescription.Text;
                //reservationType.IsActive = cbActive.Checked;
                reservationType.IsActive = true;
                reservationType.IconCssClass = tbIconCssClass.Text;
                reservationType.FinalApprovalGroupId = ddlFinalApprovalGroup.SelectedValueAsId();
                reservationType.SuperAdminGroupId = ddlSuperAdminGroup.SelectedValueAsId();
                reservationType.NotificationEmailId = ddlNotificationEmail.SelectedValueAsId();
                reservationType.IsCommunicationHistorySaved = cbIsCommunicationHistorySaved.Checked;
                reservationType.IsContactDetailsRequired = cbIsContactDetailsRequired.Checked;
                reservationType.IsNumberAttendingRequired = cbIsNumberAttendingRequired.Checked;
                reservationType.IsSetupTimeRequired = cbIsSetupTimeRequired.Checked;
                reservationType.DefaultSetupTime = nbDefaultSetupTime.Text.AsIntegerOrNull();

                foreach ( var reservationMinistryState in ReservationMinistriesState )
                {
                    ReservationMinistry reservationMinistry = reservationType.ReservationMinistries.Where( a => a.Guid == reservationMinistryState.Guid ).FirstOrDefault();
                    if ( reservationMinistry == null )
                    {
                        reservationMinistry = new ReservationMinistry();
                        reservationType.ReservationMinistries.Add( reservationMinistry );
                    }

                    reservationMinistry.CopyPropertiesFrom( reservationMinistryState );
                    reservationMinistry.ReservationTypeId = reservationType.Id;
                }

                foreach ( var reservationWorkflowTriggerState in ReservationWorkflowTriggersState )
                {
                    ReservationWorkflowTrigger reservationWorkflowTrigger = reservationType.ReservationWorkflowTriggers.Where( a => a.Guid == reservationWorkflowTriggerState.Guid ).FirstOrDefault();
                    if ( reservationWorkflowTrigger == null )
                    {
                        reservationWorkflowTrigger = new ReservationWorkflowTrigger();
                        reservationType.ReservationWorkflowTriggers.Add( reservationWorkflowTrigger );
                    }
                    else
                    {
                        reservationWorkflowTriggerState.Id = reservationWorkflowTrigger.Id;
                        reservationWorkflowTriggerState.Guid = reservationWorkflowTrigger.Guid;
                    }

                    reservationWorkflowTrigger.CopyPropertiesFrom( reservationWorkflowTriggerState );
                    reservationWorkflowTrigger.ReservationTypeId = reservationTypeId;
                }

                if ( !reservationType.IsValid )
                {
                    // Controls will render the error messages
                    return;
                }

                // need WrapTransaction due to Attribute saves
                rockContext.WrapTransaction( () =>
                {
                    rockContext.SaveChanges();

                    reservationType = reservationTypeService.Get( reservationType.Id );
                    if ( reservationType != null )
                    {
                        if ( !reservationType.IsAuthorized( Authorization.VIEW, CurrentPerson ) )
                        {
                            reservationType.AllowPerson( Authorization.VIEW, CurrentPerson, rockContext );
                        }

                        if ( !reservationType.IsAuthorized( Authorization.EDIT, CurrentPerson ) )
                        {
                            reservationType.AllowPerson( Authorization.EDIT, CurrentPerson, rockContext );
                        }

                        if ( !reservationType.IsAuthorized( Authorization.ADMINISTRATE, CurrentPerson ) )
                        {
                            reservationType.AllowPerson( Authorization.ADMINISTRATE, CurrentPerson, rockContext );
                        }
                    }
                } );

                ReservationWorkflowService.RemoveCachedTriggers();

                var qryParams = new Dictionary<string, string>();
                qryParams["ReservationTypeId"] = reservationType.Id.ToString();

                NavigateToPage( RockPage.Guid, qryParams );
            }
        }

        /// <summary>
        /// Handles the Click event of the btnCancel control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected void btnCancel_Click( object sender, EventArgs e )
        {
            if ( hfReservationTypeId.Value.Equals( "0" ) )
            {
                NavigateToParentPage();
            }
            else
            {
                ShowReadonlyDetails( GetReservationType( hfReservationTypeId.ValueAsInt(), new RockContext() ) );
            }
        }

        /// <summary>
        /// Handles the BlockUpdated event of the control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void Block_BlockUpdated( object sender, EventArgs e )
        {
            var currentReservationType = GetReservationType( hfReservationTypeId.Value.AsInteger() );
            if ( currentReservationType != null )
            {
                ShowReadonlyDetails( currentReservationType );
            }
            else
            {
                string reservationTypeId = PageParameter( "ReservationTypeId" );
                if ( !string.IsNullOrWhiteSpace( reservationTypeId ) )
                {
                    ShowDetail( reservationTypeId.AsInteger() );
                }
                else
                {
                    pnlDetails.Visible = false;
                }
            }
        }

        #endregion

        #region ReservationMinistry Events

        /// <summary>
        /// Handles the Delete event of the gMinistries control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RowEventArgs"/> instance containing the event data.</param>
        protected void gMinistries_Delete( object sender, RowEventArgs e )
        {
            Guid rowGuid = ( Guid ) e.RowKeyValue;
            ReservationMinistriesState.RemoveEntity( rowGuid );
            BindReservationMinistriesGrid();
        }

        /// <summary>
        /// Handles the Click event of the btnAddReservationMinistry control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void btnAddMinistry_Click( object sender, EventArgs e )
        {
            ReservationMinistry reservationMinistry = null;
            Guid guid = hfAddMinistryGuid.Value.AsGuid();
            if ( !guid.IsEmpty() )
            {
                reservationMinistry = ReservationMinistriesState.FirstOrDefault( l => l.Guid.Equals( guid ) );
            }

            if ( reservationMinistry == null )
            {
                reservationMinistry = new ReservationMinistry();
            }
            reservationMinistry.Name = tbMinistryName.Text;
            if ( !reservationMinistry.IsValid )
            {
                return;
            }
            if ( ReservationMinistriesState.Any( a => a.Guid.Equals( reservationMinistry.Guid ) ) )
            {
                ReservationMinistriesState.RemoveEntity( reservationMinistry.Guid );
            }
            ReservationMinistriesState.Add( reservationMinistry );

            BindReservationMinistriesGrid();

            HideDialog();
        }

        /// <summary>
        /// Handles the GridRebind event of the gMinistries control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void gMinistries_GridRebind( object sender, EventArgs e )
        {
            BindReservationMinistriesGrid();
        }

        /// <summary>
        /// Handles the Add event of the gMinistries control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void gMinistries_Add( object sender, EventArgs e )
        {
            gMinistries_ShowEdit( Guid.Empty );
        }

        /// <summary>
        /// Handles the Edit event of the gMinistries control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RowEventArgs"/> instance containing the event data.</param>
        protected void gMinistries_Edit( object sender, RowEventArgs e )
        {
            Guid reservationMinistryGuid = ( Guid ) e.RowKeyValue;
            gMinistries_ShowEdit( reservationMinistryGuid );
        }

        /// <summary>
        /// gs the statuses_ show edit.
        /// </summary>
        /// <param name="reservationMinistryGuid">The connection status unique identifier.</param>
        protected void gMinistries_ShowEdit( Guid reservationMinistryGuid )
        {
            ReservationMinistry reservationMinistry = ReservationMinistriesState.FirstOrDefault( l => l.Guid.Equals( reservationMinistryGuid ) );
            tbMinistryName.Text = reservationMinistry != null ? reservationMinistry.Name : string.Empty;

            hfAddMinistryGuid.Value = reservationMinistryGuid.ToString();
            ShowDialog( "ReservationMinistries", true );
        }

        /// <summary>
        /// Binds the connection activity types grid.
        /// </summary>
        private void BindReservationMinistriesGrid()
        {
            SetReservationMinistryListOrder( ReservationMinistriesState );
            gMinistries.DataSource = ReservationMinistriesState.OrderBy( a => a.Name ).ToList();

            gMinistries.DataBind();
        }

        /// <summary>
        /// Sets the attribute list order.
        /// </summary>
        /// <param name="attributeList">The attribute list.</param>
        private void SetReservationMinistryListOrder( List<ReservationMinistry> reservationMinistryList )
        {
            if ( reservationMinistryList != null )
            {
                if ( reservationMinistryList.Any() )
                {
                    reservationMinistryList.OrderBy( a => a.Name ).ToList();
                }
            }
        }

        #endregion

        #region ReservationWorkflowTrigger Events

        /// <summary>
        /// Handles the SaveClick event of the dlgReservationWorkflowTrigger control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void dlgWorkflowTrigger_SaveClick( object sender, EventArgs e )
        {
            ReservationWorkflowTrigger reservationWorkflowTrigger = null;
            Guid guid = hfAddWorkflowTriggerGuid.Value.AsGuid();
            if ( !guid.IsEmpty() )
            {
                reservationWorkflowTrigger = ReservationWorkflowTriggersState.FirstOrDefault( l => l.Guid.Equals( guid ) );
            }

            if ( reservationWorkflowTrigger == null )
            {
                reservationWorkflowTrigger = new ReservationWorkflowTrigger();
            }
            try
            {
                reservationWorkflowTrigger.WorkflowType = new WorkflowTypeService( new RockContext() ).Get( ddlWorkflowType.SelectedValueAsId().Value );
            }
            catch { }
            reservationWorkflowTrigger.WorkflowTypeId = ddlWorkflowType.SelectedValueAsId().Value;
            reservationWorkflowTrigger.TriggerType = ddlTriggerType.SelectedValueAsEnum<ReservationWorkflowTriggerType>();
            reservationWorkflowTrigger.QualifierValue = String.Format( "|{0}|{1}|", ddlPrimaryQualifier.SelectedValue, ddlSecondaryQualifier.SelectedValue );
            if ( !reservationWorkflowTrigger.IsValid )
            {
                return;
            }
            if ( ReservationWorkflowTriggersState.Any( a => a.Guid.Equals( reservationWorkflowTrigger.Guid ) ) )
            {
                ReservationWorkflowTriggersState.RemoveEntity( reservationWorkflowTrigger.Guid );
            }

            ReservationWorkflowTriggersState.Add( reservationWorkflowTrigger );
            BindReservationWorkflowTriggersGrid();
            HideDialog();
        }

        /// <summary>
        /// Handles the Delete event of the gWorkflowTriggers control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RowEventArgs"/> instance containing the event data.</param>
        protected void gWorkflowTriggers_Delete( object sender, RowEventArgs e )
        {
            Guid rowGuid = ( Guid ) e.RowKeyValue;
            ReservationWorkflowTriggersState.RemoveEntity( rowGuid );

            BindReservationWorkflowTriggersGrid();
        }

        /// <summary>
        /// Handles the GridRebind event of the gWorkflowTriggers control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void gWorkflowTriggers_GridRebind( object sender, EventArgs e )
        {
            BindReservationWorkflowTriggersGrid();
        }

        /// <summary>
        /// Handles the Edit event of the gWorkflowTriggers control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RowEventArgs"/> instance containing the event data.</param>
        protected void gWorkflowTriggers_Edit( object sender, RowEventArgs e )
        {
            Guid reservationWorkflowTriggerGuid = ( Guid ) e.RowKeyValue;
            gWorkflowTriggers_ShowEdit( reservationWorkflowTriggerGuid );
        }

        /// <summary>
        /// Gs the workflows_ show edit.
        /// </summary>
        /// <param name="reservationWorkflowTriggerGuid">The connection workflow unique identifier.</param>
        protected void gWorkflowTriggers_ShowEdit( Guid reservationWorkflowTriggerGuid )
        {
            ReservationWorkflowTrigger reservationWorkflowTrigger = ReservationWorkflowTriggersState.FirstOrDefault( l => l.Guid.Equals( reservationWorkflowTriggerGuid ) );
            if ( reservationWorkflowTrigger != null )
            {
                ddlTriggerType.BindToEnum<ReservationWorkflowTriggerType>();
                ddlWorkflowType.Items.Clear();
                ddlWorkflowType.Items.Add( new ListItem( string.Empty, string.Empty ) );

                foreach ( var workflowType in new WorkflowTypeService( new RockContext() ).Queryable().OrderBy( w => w.Name ) )
                {
                    if ( workflowType.IsAuthorized( Authorization.VIEW, CurrentPerson ) )
                    {
                        ddlWorkflowType.Items.Add( new ListItem( workflowType.Name, workflowType.Id.ToString() ) );
                    }
                }

                if ( reservationWorkflowTrigger.WorkflowTypeId == null )
                {
                    ddlWorkflowType.SelectedValue = "0";
                }
                else
                {
                    ddlWorkflowType.SelectedValue = reservationWorkflowTrigger.WorkflowTypeId.ToString();
                }

                ddlTriggerType.SelectedValue = reservationWorkflowTrigger.TriggerType.ConvertToInt().ToString();
            }
            else
            {
                ddlTriggerType.BindToEnum<ReservationWorkflowTriggerType>();
                ddlWorkflowType.Items.Clear();
                ddlWorkflowType.Items.Add( new ListItem( string.Empty, string.Empty ) );

                foreach ( var workflowType in new WorkflowTypeService( new RockContext() ).Queryable().OrderBy( w => w.Name ) )
                {
                    if ( workflowType.IsAuthorized( Authorization.VIEW, CurrentPerson ) )
                    {
                        ddlWorkflowType.Items.Add( new ListItem( workflowType.Name, workflowType.Id.ToString() ) );
                    }
                }
            }

            hfAddWorkflowTriggerGuid.Value = reservationWorkflowTriggerGuid.ToString();
            UpdateTriggerQualifiers();
            ShowDialog( "ReservationWorkflowTriggers", true );
        }

        /// <summary>
        /// Handles the Add event of the gWorkflowTriggers control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void gWorkflowTriggers_Add( object sender, EventArgs e )
        {
            gWorkflowTriggers_ShowEdit( Guid.Empty );
        }

        /// <summary>
        /// Handles the SelectedIndexChanged event of the ddlTriggerType control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void ddlTriggerType_SelectedIndexChanged( object sender, EventArgs e )
        {
            UpdateTriggerQualifiers();
        }

        /// <summary>
        /// Updates the trigger qualifiers.
        /// </summary>
        private void UpdateTriggerQualifiers()
        {
            using ( var rockContext = new RockContext() )
            {
                var isQualifierDropdownsVisible = false;
                String[] qualifierValues = new String[2];
                ReservationWorkflowTrigger reservationWorkflowTrigger = ReservationWorkflowTriggersState.FirstOrDefault( l => l.Guid.Equals( hfAddWorkflowTriggerGuid.Value.AsGuid() ) );
                ReservationWorkflowTriggerType reservationWorkflowTriggerType = ddlTriggerType.SelectedValueAsEnum<ReservationWorkflowTriggerType>();
                switch ( reservationWorkflowTriggerType )
                {
                    case ReservationWorkflowTriggerType.ReservationCreated:
                        ddlPrimaryQualifier.Items.Clear();
                        ddlSecondaryQualifier.Items.Clear();
                        break;

                    case ReservationWorkflowTriggerType.Manual:
                        ddlPrimaryQualifier.Items.Clear();
                        ddlSecondaryQualifier.Items.Clear();
                        break;

                    case ReservationWorkflowTriggerType.StateChanged:
                        ddlPrimaryQualifier.Label = "From";
                        ddlPrimaryQualifier.BindToEnum<ReservationApprovalState>( true );

                        ddlSecondaryQualifier.Label = "To";
                        ddlSecondaryQualifier.BindToEnum<ReservationApprovalState>( true );

                        isQualifierDropdownsVisible = true;
                        break;

                    case ReservationWorkflowTriggerType.ReservationUpdated:
                        ddlPrimaryQualifier.Items.Clear();
                        ddlSecondaryQualifier.Items.Clear();
                        break;

                }

                ddlPrimaryQualifier.Visible = ddlSecondaryQualifier.Visible = isQualifierDropdownsVisible;

                if ( reservationWorkflowTrigger != null )
                {
                    if ( reservationWorkflowTrigger.TriggerType == ddlTriggerType.SelectedValueAsEnum<ReservationWorkflowTriggerType>() )
                    {
                        qualifierValues = reservationWorkflowTrigger.QualifierValue.SplitDelimitedValues();
                        if ( isQualifierDropdownsVisible && qualifierValues.Length > 0 )
                        {
                            ddlPrimaryQualifier.SelectedValue = qualifierValues[0];
                        }

                        if ( isQualifierDropdownsVisible && qualifierValues.Length > 1 )
                        {
                            ddlSecondaryQualifier.SelectedValue = qualifierValues[1];
                        }
                    }
                }
            }

        }

        /// <summary>
        /// Binds the connection workflows grid.
        /// </summary>
        private void BindReservationWorkflowTriggersGrid()
        {
            SetReservationWorkflowTriggerListOrder( ReservationWorkflowTriggersState );
            gWorkflowTriggers.DataSource = ReservationWorkflowTriggersState.Select( c => new
            {
                c.Id,
                c.Guid,
                WorkflowType = c.WorkflowType.Name,
                Trigger = c.TriggerType.ConvertToString()
            } ).ToList();
            gWorkflowTriggers.DataBind();
        }

        /// <summary>
        /// Sets the connection workflow list order.
        /// </summary>
        /// <param name="reservationWorkflowTriggerList">The connection workflow list.</param>
        private void SetReservationWorkflowTriggerListOrder( List<ReservationWorkflowTrigger> reservationWorkflowTriggerList )
        {
            if ( reservationWorkflowTriggerList != null )
            {
                if ( reservationWorkflowTriggerList.Any() )
                {
                    reservationWorkflowTriggerList.OrderBy( c => c.WorkflowType.Name ).ThenBy( c => c.TriggerType.ConvertToString() ).ToList();
                }
            }
        }

        #endregion

        #endregion

        #region Internal Methods

        /// <summary>
        /// Shows the edit.
        /// </summary>
        /// <param name="reservationTypeId">The Reservation Type Type identifier.</param>
        public void ShowDetail( int reservationTypeId )
        {
            pnlDetails.Visible = false;

            ReservationType reservationType = null;
            using ( var rockContext = new RockContext() )
            {
                if ( !reservationTypeId.Equals( 0 ) )
                {
                    reservationType = GetReservationType( reservationTypeId, rockContext );
                    pdAuditDetails.SetEntity( reservationType, ResolveRockUrl( "~" ) );
                }

                if ( reservationType == null )
                {
                    reservationType = new ReservationType { Id = 0 };
                    // hide the panel drawer that show created and last modified dates
                    pdAuditDetails.Visible = false;
                }

                // Admin rights are needed to edit a reservation type ( Edit rights only allow adding/removing items )
                bool adminAllowed = UserCanAdministrate || reservationType.IsAuthorized( Authorization.ADMINISTRATE, CurrentPerson );
                pnlDetails.Visible = true;
                hfReservationTypeId.Value = reservationType.Id.ToString();
                lIcon.Text = string.Format( "<i class='{0}'></i>", reservationType.IconCssClass );
                bool readOnly = false;

                nbEditModeMessage.Text = string.Empty;
                if ( !adminAllowed )
                {
                    readOnly = true;
                    nbEditModeMessage.Text = EditModeMessage.ReadOnlyEditActionNotAllowed( ReservationType.FriendlyTypeName );
                }

                // Security won't be enabled until 1.3.1/1.4
                btnSecurity.Visible = false;

                if ( readOnly )
                {
                    btnEdit.Visible = false;
                    btnDelete.Visible = false;
                    btnSecurity.Visible = false;
                    ShowReadonlyDetails( reservationType );
                }
                else
                {
                    btnEdit.Visible = true;
                    btnDelete.Visible = true;
                    btnSecurity.Visible = true;

                    btnSecurity.Title = "Secure " + reservationType.Name;
                    btnSecurity.EntityId = reservationType.Id;

                    if ( !reservationTypeId.Equals( 0 ) )
                    {
                        ShowReadonlyDetails( reservationType );
                    }
                    else
                    {
                        ShowEditDetails( reservationType );
                    }
                }
            }
        }

        /// <summary>
        /// Shows the edit details.
        /// </summary>
        /// <param name="reservationType">Type of the reservation.</param>
        private void ShowEditDetails( ReservationType reservationType )
        {
            if ( reservationType == null )
            {
                reservationType = new ReservationType();
                reservationType.IconCssClass = "fa fa-compress";
            }
            if ( reservationType.Id == 0 )
            {
                lReadOnlyTitle.Text = ActionTitle.Add( ReservationType.FriendlyTypeName ).FormatAsHtmlTitle();
            }
            else
            {
                lReadOnlyTitle.Text = reservationType.Name.FormatAsHtmlTitle();
            }

            SetEditMode( true );

            // General
            tbName.Text = reservationType.Name;
            tbDescription.Text = reservationType.Description;
            tbIconCssClass.Text = reservationType.IconCssClass;
            //cbActive.Checked = reservationType.IsActive;
            cbIsCommunicationHistorySaved.Checked = reservationType.IsCommunicationHistorySaved;
            cbIsContactDetailsRequired.Checked = reservationType.IsContactDetailsRequired;
            cbIsNumberAttendingRequired.Checked = reservationType.IsNumberAttendingRequired;
            cbIsSetupTimeRequired.Checked = reservationType.IsSetupTimeRequired;
            nbDefaultSetupTime.Text = reservationType.DefaultSetupTime.ToStringSafe();

            LoadDropDowns();
            if ( reservationType.NotificationEmailId.HasValue )
            {
                ddlNotificationEmail.SetValue( reservationType.NotificationEmailId.Value );

            }
            if ( reservationType.FinalApprovalGroupId.HasValue )
            {
                ddlFinalApprovalGroup.SetValue( reservationType.FinalApprovalGroupId.Value );

            }
            if ( reservationType.SuperAdminGroupId.HasValue )
            {
                ddlSuperAdminGroup.SetValue( reservationType.SuperAdminGroupId.Value );

            }

            ReservationWorkflowTriggersState = reservationType.ReservationWorkflowTriggers.ToList();
            ReservationMinistriesState = reservationType.ReservationMinistries.ToList();

            BindReservationWorkflowTriggersGrid();
            BindReservationMinistriesGrid();
        }

        private void LoadDropDowns()
        {
            ddlFinalApprovalGroup.Items.Clear();
            ddlSuperAdminGroup.Items.Clear();

            ddlFinalApprovalGroup.Items.Add( new ListItem( string.Empty, string.Empty ) );
            ddlSuperAdminGroup.Items.Add( new ListItem( string.Empty, string.Empty ) );

            var roles = new GroupService( new RockContext() ).Queryable().Where( g => g.IsSecurityRole ).OrderBy( t => t.Name );
            if ( roles.Any() )
            {
                foreach ( var role in roles )
                {
                    ddlFinalApprovalGroup.Items.Add( new ListItem( role.Name, role.Id.ToString() ) );
                    ddlSuperAdminGroup.Items.Add( new ListItem( role.Name, role.Id.ToString() ) );
                }

            }

            ddlNotificationEmail.Items.Clear();
            ddlNotificationEmail.Items.Add( new ListItem( string.Empty, string.Empty ) );

            using ( var rockContext = new RockContext() )
            {
                foreach ( var systemEmail in new SystemEmailService( rockContext )
                    .Queryable().AsNoTracking()
                    .OrderBy( e => e.Title )
                    .Select( e => new
                    {
                        e.Id,
                        e.Title
                    } ) )
                {
                    ddlNotificationEmail.Items.Add( new ListItem( systemEmail.Title, systemEmail.Id.ToString() ) );
                }
            }
        }

        /// <summary>
        /// Shows the readonly details.
        /// </summary>
        /// <param name="reservationType">Type of the reservation.</param>
        private void ShowReadonlyDetails( ReservationType reservationType )
        {
            SetEditMode( false );

            hfReservationTypeId.SetValue( reservationType.Id );
            ReservationWorkflowTriggersState = null;
            ReservationMinistriesState = null;

            lReadOnlyTitle.Text = reservationType.Name.FormatAsHtmlTitle();
            lReservationTypeDescription.Text = reservationType.Description.ScrubHtmlAndConvertCrLfToBr();
        }

        /// <summary>
        /// Gets the type of the reservation.
        /// </summary>
        /// <param name="reservationTypeId">The reservation type identifier.</param>
        /// <param name="rockContext">The rock context.</param>
        /// <returns></returns>
        private ReservationType GetReservationType( int reservationTypeId, RockContext rockContext = null )
        {
            string key = string.Format( "ReservationType:{0}", reservationTypeId );
            ReservationType reservationType = RockPage.GetSharedItem( key ) as ReservationType;
            if ( reservationType == null )
            {
                rockContext = rockContext ?? new RockContext();
                reservationType = new ReservationTypeService( rockContext ).Queryable()
                    .Where( c => c.Id == reservationTypeId )
                    .FirstOrDefault();
                RockPage.SaveSharedItem( key, reservationType );
            }

            return reservationType;
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
        /// Shows the dialog.
        /// </summary>
        /// <param name="dialog">The dialog.</param>
        /// <param name="setValues">if set to <c>true</c> [set values].</param>
        private void ShowDialog( string dialog, bool setValues = false )
        {
            hfActiveDialog.Value = dialog.ToUpper().Trim();
            ShowDialog( setValues );
        }

        /// <summary>
        /// Shows the dialog.
        /// </summary>
        /// <param name="setValues">if set to <c>true</c> [set values].</param>
        private void ShowDialog( bool setValues = false )
        {
            switch ( hfActiveDialog.Value )
            {

                case "RESERVATIONMINISTRIES":
                    dlgMinistries.Show();
                    break;
                case "RESERVATIONWORKFLOWTRIGGERS":
                    dlgWorkflowTrigger.Show();
                    break;
            }
        }

        /// <summary>
        /// Hides the dialog.
        /// </summary>
        private void HideDialog()
        {
            switch ( hfActiveDialog.Value )
            {
                case "RESERVATIONMINISTRIES":
                    dlgMinistries.Hide();
                    break;
                case "RESERVATIONWORKFLOWTRIGGERS":
                    dlgWorkflowTrigger.Hide();
                    break;
            }

            hfActiveDialog.Value = string.Empty;
        }

        #endregion
    }
}