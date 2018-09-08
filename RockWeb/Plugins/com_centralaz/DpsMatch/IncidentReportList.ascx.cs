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
using System.ComponentModel;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Web.UI;
using Rock.Web.UI.Controls;
using Rock;
using Attribute = Rock.Model.Attribute;
using Rock.Security;
using Rock.Web.Cache;
using Newtonsoft.Json;
using System.Text;

namespace RockWeb.Plugins.com_centralaz.DpsMatch
{
    [DisplayName( "Incident Report List" )]
    [Category( "com_centralaz > DpsMatch" )]
    [Description( "Lists all the Incident Reports." )]

    [LinkedPage( "Detail Page", "Page used to display details about a workflow." )]
    [WorkflowTypeField( "Incident Report WorkflowType", "The default workflow type to use. If provided the query string will be ignored." )]
    [WorkflowTypeField( "Supplemental Report WorkflowType", "The default workflow type to use. If provided the query string will be ignored." )]
    [TextField( "Report Id Attribute Guid", "The Guid of the attribute used to tie the supplemental form to the incident report form", true, "AA55D285-C06B-43F1-9DD2-AF23678F948E" )]
    public partial class IncidentReportList : RockBlock
    {
        #region Fields

        private bool _canView = false;
        private bool _canEdit = false;
        private WorkflowType _workflowType = null;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the available attributes.
        /// </summary>
        /// <value>
        /// The available attributes.
        /// </value>
        public List<AttributeCache> AvailableAttributes { get; set; }

        #endregion

        #region Base Control Methods

        /// <summary>
        /// Restores the view-state information from a previous user control request that was saved by the <see cref="M:System.Web.UI.UserControl.SaveViewState" /> method.
        /// </summary>
        /// <param name="savedState">An <see cref="T:System.Object" /> that represents the user control state to be restored.</param>
        protected override void LoadViewState( object savedState )
        {
            base.LoadViewState( savedState );

            AvailableAttributes = ViewState["AvailableAttributes"] as List<AttributeCache>;

            AddDynamicControls();
        }

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Init" /> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnInit( EventArgs e )
        {
            base.OnInit( e );

            if ( !string.IsNullOrWhiteSpace( GetAttributeValue( "IncidentReportWorkflowType" ) ) )
            {
                Guid workflowTypeGuid = Guid.Empty;
                Guid.TryParse( GetAttributeValue( "IncidentReportWorkflowType" ), out workflowTypeGuid );
                _workflowType = new WorkflowTypeService( new RockContext() ).Get( workflowTypeGuid );
            }

            if ( _workflowType != null )
            {
                _canEdit = UserCanEdit || _workflowType.IsAuthorized( Authorization.EDIT, CurrentPerson );
                _canView = _canEdit || ( _workflowType.IsAuthorized( Authorization.VIEW, CurrentPerson ) && _workflowType.IsAuthorized( "ViewList", CurrentPerson ) );

                gfWorkflows.ApplyFilterClick += gfWorkflows_ApplyFilterClick;
                gfWorkflows.DisplayFilterValue += gfWorkflows_DisplayFilterValue;

                // this event gets fired after block settings are updated. it's nice to repaint the screen if these settings would alter it
                this.BlockUpdated += Block_BlockUpdated;
                this.AddConfigurationUpdateTrigger( upnlSettings );

                gWorkflows.DataKeyNames = new string[] { "Id" };
                gWorkflows.GridRebind += gWorkflows_GridRebind;
                gWorkflows.IsDeleteEnabled = _canEdit;

                if ( !string.IsNullOrWhiteSpace( _workflowType.WorkTerm ) )
                {
                    gWorkflows.RowItemText = _workflowType.WorkTerm;
                    lGridTitle.Text = _workflowType.WorkTerm.Pluralize();
                }

                RockPage.PageTitle = _workflowType.Name;

                if ( !string.IsNullOrWhiteSpace( _workflowType.IconCssClass ) )
                {
                    lHeadingIcon.Text = string.Format( "<i class='{0}'></i>", _workflowType.IconCssClass );
                }
            }
            else
            {
                pnlWorkflowList.Visible = false;
            }
        }


        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Load" /> event.
        /// </summary>
        /// <param name="e">The <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );

            if ( !Page.IsPostBack && _canView )
            {
                SetFilter();
                BindGrid();
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
            ViewState["AvailableAttributes"] = AvailableAttributes;

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
        }

        protected void gfWorkflows_DisplayFilterValue( object sender, Rock.Web.UI.Controls.GridFilter.DisplayFilterValueArgs e )
        {

            if ( AvailableAttributes != null )
            {
                var attribute = AvailableAttributes.FirstOrDefault( a => MakeKeyUniqueToType( a.Key ) == e.Key );
                if ( attribute != null )
                {
                    try
                    {
                        var values = JsonConvert.DeserializeObject<List<string>>( e.Value );
                        e.Value = attribute.FieldType.Field.FormatFilterValues( attribute.QualifierValues, values );
                        return;
                    }
                    catch { }
                }
            }

            if ( e.Key == MakeKeyUniqueToType( "Activated" ) )
            {
                e.Value = DateRangePicker.FormatDelimitedValues( e.Value );
            }
            else if ( e.Key == MakeKeyUniqueToType( "Completed" ) )
            {
                e.Value = DateRangePicker.FormatDelimitedValues( e.Value );
            }
            else if ( e.Key == MakeKeyUniqueToType( "Initiator" ) )
            {
                int? personId = e.Value.AsIntegerOrNull();
                if ( personId.HasValue )
                {
                    var person = new PersonService( new RockContext() ).Get( personId.Value );
                    if ( person != null )
                    {
                        e.Value = person.FullName;
                    }
                }
            }
            else if ( e.Key == MakeKeyUniqueToType( "Name" ) )
            {
                return;
            }
            else if ( e.Key == MakeKeyUniqueToType( "Status" ) )
            {
                return;
            }
            else if ( e.Key == MakeKeyUniqueToType( "State" ) )
            {
                return;
            }
            else
            {
                e.Value = string.Empty;
            }
        }

        protected void gfWorkflows_ApplyFilterClick( object sender, EventArgs e )
        {
            if ( AvailableAttributes != null )
            {
                foreach ( var attribute in AvailableAttributes )
                {
                    var filterControl = phAttributeFilters.FindControl( "filter_" + attribute.Id.ToString() );
                    if ( filterControl != null )
                    {
                        try
                        {
                            var values = attribute.FieldType.Field.GetFilterValues( filterControl, attribute.QualifierValues, Rock.Reporting.FilterMode.SimpleFilter );
                            gfWorkflows.SaveUserPreference( MakeKeyUniqueToType( attribute.Key ), attribute.Name, attribute.FieldType.Field.GetFilterValues( filterControl, attribute.QualifierValues, Rock.Reporting.FilterMode.SimpleFilter ).ToJson() );
                        }
                        catch { }
                    }
                }
            }

            BindGrid();
        }

        /// <summary>
        /// Handles the Edit event of the gWorkflows control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RowEventArgs" /> instance containing the event data.</param>
        protected void gWorkflows_Manage( object sender, RowEventArgs e )
        {
            NavigateToLinkedPage( "DetailPage", "workflowId", e.RowKeyId );
        }

        /// <summary>
        /// Handles the Delete event of the gWorkflows control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RowEventArgs" /> instance containing the event data.</param>
        protected void gWorkflows_Delete( object sender, RowEventArgs e )
        {
            var rockContext = new RockContext();
            WorkflowService workflowService = new WorkflowService( rockContext );
            Workflow workflow = workflowService.Get( e.RowKeyId );
            if ( workflow != null )
            {
                string errorMessage;
                if ( !workflowService.CanDelete( workflow, out errorMessage ) )
                {
                    mdGridWarning.Show( errorMessage, ModalAlertType.Information );
                    return;
                }

                workflowService.Delete( workflow );
                rockContext.SaveChanges();
            }

            BindGrid();
        }

        /// <summary>
        /// Handles the GridRebind event of the gWorkflows control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected void gWorkflows_GridRebind( object sender, EventArgs e )
        {
            BindGrid();
        }

        #endregion

        #region Methods

        /// <summary>
        /// Binds the filter.
        /// </summary>
        private void SetFilter()
        {
            BindAttributes();
            AddDynamicControls();
        }

        private void BindAttributes()
        {
            // Parse the attribute filters 
            AvailableAttributes = new List<AttributeCache>();
            if ( _workflowType != null )
            {
                int entityTypeId = new Workflow().TypeId;
                string workflowQualifier = _workflowType.Id.ToString();
                foreach ( var attributeModel in new AttributeService( new RockContext() ).Queryable()
                    .Where( a =>
                        a.EntityTypeId == entityTypeId &&
                        a.IsGridColumn &&
                        a.EntityTypeQualifierColumn.Equals( "WorkflowTypeId", StringComparison.OrdinalIgnoreCase ) &&
                        a.EntityTypeQualifierValue.Equals( workflowQualifier ) )
                    .OrderByDescending( a => a.EntityTypeQualifierColumn )
                    .ThenBy( a => a.Order )
                    .ThenBy( a => a.Name ) )
                {
                    AvailableAttributes.Add( AttributeCache.Get( attributeModel ) );
                }
            }
        }

        /// <summary>
        /// Adds the attribute columns.
        /// </summary>
        private void AddDynamicControls()
        {
            // Clear the filter controls
            phAttributeFilters.Controls.Clear();

            // Remove attribute columns
            foreach ( var column in gWorkflows.Columns.OfType<AttributeField>().ToList() )
            {
                gWorkflows.Columns.Remove( column );
            }

            if ( AvailableAttributes != null )
            {
                foreach ( var attribute in AvailableAttributes )
                {
                    var control = attribute.FieldType.Field.FilterControl( attribute.QualifierValues, "filter_" + attribute.Id.ToString(), false, Rock.Reporting.FilterMode.SimpleFilter );
                    if ( control != null )
                    {
                        if ( control is IRockControl )
                        {
                            var rockControl = ( IRockControl ) control;
                            rockControl.Label = attribute.Name;
                            rockControl.Help = attribute.Description;
                            phAttributeFilters.Controls.Add( control );
                        }
                        else
                        {
                            var wrapper = new RockControlWrapper();
                            wrapper.ID = control.ID + "_wrapper";
                            wrapper.Label = attribute.Name;
                            wrapper.Controls.Add( control );
                            phAttributeFilters.Controls.Add( wrapper );
                        }
                    }

                    string savedValue = gfWorkflows.GetUserPreference( MakeKeyUniqueToType( attribute.Key ) );
                    if ( !string.IsNullOrWhiteSpace( savedValue ) )
                    {
                        try
                        {
                            var values = JsonConvert.DeserializeObject<List<string>>( savedValue );
                            attribute.FieldType.Field.SetFilterValues( control, attribute.QualifierValues, values );
                        }
                        catch { }
                    }

                    string dataFieldExpression = attribute.Key;
                    bool columnExists = gWorkflows.Columns.OfType<AttributeField>().FirstOrDefault( a => a.DataField.Equals( dataFieldExpression ) ) != null;
                    if ( !columnExists )
                    {
                        AttributeField boundField = new AttributeField();
                        boundField.DataField = dataFieldExpression;
                        boundField.AttributeId = attribute.Id;
                        boundField.HeaderText = attribute.Name;
                        boundField.Condensed = false;

                        var attributeCache = AttributeCache.Get( attribute.Id );
                        if ( attributeCache != null )
                        {
                            boundField.ItemStyle.HorizontalAlign = attributeCache.FieldType.Field.AlignValue;
                        }

                        gWorkflows.Columns.Add( boundField );
                    }
                }
            }

            var supplementFormField = new CallbackField();
            gWorkflows.Columns.Add( supplementFormField );
            supplementFormField.DataField = "SupplementalForms";
            supplementFormField.HeaderText = "Supplemental Forms";
            supplementFormField.HtmlEncode = false;

            if ( _canView )
            {
                var manageField = new LinkButtonField();
                gWorkflows.Columns.Add( manageField );
                manageField.CssClass = "btn btn-default btn-sm fa fa-file-text-o";
                manageField.Click += gWorkflows_Manage;
            }

            if ( _canEdit )
            {
                var deleteField = new DeleteField();
                gWorkflows.Columns.Add( deleteField );
                deleteField.Click += gWorkflows_Delete;
            }
        }

        /// <summary>
        /// Binds the grid.
        /// </summary>
        private void BindGrid()
        {
            if ( _workflowType != null )
            {
                pnlWorkflowList.Visible = true;

                var idCol = gWorkflows.ColumnsOfType<BoundField>().Where( c => c.DataField == "WorkflowId" ).FirstOrDefault();
                if ( idCol != null )
                {
                    idCol.Visible = !string.IsNullOrWhiteSpace( _workflowType.WorkflowIdPrefix );
                }

                var rockContext = new RockContext();
                var workflowService = new WorkflowService( rockContext );

                var qry = workflowService
                    .Queryable( "Activities.ActivityType,InitiatorPersonAlias.Person" ).AsNoTracking()
                    .Where( w => w.WorkflowTypeId.Equals( _workflowType.Id ) );

                qry = qry.Where( w => w.CompletedDateTime.HasValue );

                // Filter query by any configured attribute filters
                if ( AvailableAttributes != null && AvailableAttributes.Any() )
                {
                    var attributeValueService = new AttributeValueService( rockContext );
                    var parameterExpression = attributeValueService.ParameterExpression;

                    foreach ( var attribute in AvailableAttributes )
                    {
                        var filterControl = phAttributeFilters.FindControl( "filter_" + attribute.Id.ToString() );
                        if ( filterControl != null )
                        {
                            var filterValues = attribute.FieldType.Field.GetFilterValues( filterControl, attribute.QualifierValues, Rock.Reporting.FilterMode.SimpleFilter );
                            var expression = attribute.FieldType.Field.AttributeFilterExpression( attribute.QualifierValues, filterValues, parameterExpression );
                            if ( expression != null )
                            {
                                var attributeValues = attributeValueService
                                    .Queryable()
                                    .Where( v => v.Attribute.Id == attribute.Id );

                                attributeValues = attributeValues.Where( parameterExpression, expression, null );

                                qry = qry.Where( w => attributeValues.Select( v => v.EntityId ).Contains( w.Id ) );
                            }
                        }
                    }
                }

                IQueryable<Workflow> workflows = null;

                var sortProperty = gWorkflows.SortProperty;
                if ( sortProperty != null )
                {
                    if ( sortProperty.Property == "Initiator" )
                    {
                        if ( sortProperty.Direction == SortDirection.Ascending )
                        {
                            workflows = qry
                                .OrderBy( w => w.InitiatorPersonAlias.Person.LastName )
                                .ThenBy( w => w.InitiatorPersonAlias.Person.NickName );
                        }
                        else
                        {
                            workflows = qry
                                .OrderByDescending( w => w.InitiatorPersonAlias.Person.LastName )
                                .ThenByDescending( w => w.InitiatorPersonAlias.Person.NickName );
                        }
                    }
                    else
                    {
                        workflows = qry.Sort( sortProperty );
                    }
                }
                else
                {
                    workflows = qry.OrderByDescending( s => s.CreatedDateTime );
                }

                // Since we're not binding to actual workflow list, but are using AttributeField columns,
                // we need to save the workflows into the grid's object list
                var workflowObjectQry = workflows;
                if ( gWorkflows.AllowPaging )
                {
                    workflowObjectQry = workflowObjectQry.Skip( gWorkflows.PageIndex * gWorkflows.PageSize ).Take( gWorkflows.PageSize );
                }

                gWorkflows.ObjectList = workflowObjectQry.ToList().ToDictionary( k => k.Id.ToString(), v => v as object );

                // Get Supplemental Forms
                var supplementalWorkflowType = new WorkflowTypeService( new RockContext() ).Get( GetAttributeValue( "SupplementalReportWorkflowType" ).AsGuid() );
                var supplementTypeId = 0;
                if ( supplementalWorkflowType != null )
                {
                    supplementTypeId = supplementalWorkflowType.Id;
                }

                var reportIdAttributeGuid = GetAttributeValue( "ReportIdAttributeGuid" ).AsGuid();
                var qrySupplement = workflowService.Queryable().AsNoTracking().Where( w => w.WorkflowTypeId.Equals( supplementTypeId ) );
                var qryAttributeValue = new AttributeValueService( rockContext ).Queryable().AsNoTracking().Where( av => av.Attribute.Guid == reportIdAttributeGuid );

                var supplementalWorkflowSummaryList = qryAttributeValue.ToList()
                    .Join( qrySupplement.ToList(), av => av.EntityId, w => w.Id, ( av, w ) => new
                    {
                        Id = w.Id,
                        WorkflowId = w.WorkflowId,
                        ParentWorkflowId = av.Value
                    } )
                    .Where( s => workflowObjectQry.Select( w => w.WorkflowId ).Contains( s.ParentWorkflowId ) )
                    .ToList()
                    .Select( s => new SupplementalWorkflowSummary
                    {
                        Id = s.Id,
                        WorkflowId = s.WorkflowId,
                        ParentWorkflowId = s.ParentWorkflowId,
                        FormattedValue = String.Format( "<li><a href='{0}?workflowId={1}'>{2}</li>", LinkedPageUrl( "DetailPage" ), s.Id, s.WorkflowId )
                    } );

                gWorkflows.EntityTypeId = EntityTypeCache.Get<Workflow>().Id;
                var qryGrid = workflowObjectQry.ToList().Select( w => new
                {
                    w.Id,
                    w.WorkflowId,
                    w.Name,
                    Initiator = w.InitiatorPersonAlias != null ? w.InitiatorPersonAlias.Person : null,
                    Activities = w.Activities.Where( a => a.ActivatedDateTime.HasValue && !a.CompletedDateTime.HasValue ).OrderBy( a => a.ActivityType.Order ).Select( a => a.ActivityType.Name ),
                    w.CreatedDateTime,
                    Status = w.Status,
                    IsCompleted = w.CompletedDateTime.HasValue,
                    SupplementalForms = String.Format( "<ul>{0}</ul>", supplementalWorkflowSummaryList.Where( sws => sws.ParentWorkflowId == w.WorkflowId ).Select( sws => sws.FormattedValue ).ToList().AsDelimited( "" ) )
                } ).AsQueryable();

                gWorkflows.SetLinqDataSource( qryGrid );
                gWorkflows.DataBind();
            }
            else
            {
                pnlWorkflowList.Visible = false;
            }

        }

        private string MakeKeyUniqueToType( string key )
        {
            if ( _workflowType != null )
            {
                return string.Format( "{0}-{1}", _workflowType.Id, key );
            }
            return key;
        }

        public class SupplementalWorkflowSummary
        {
            public int Id { get; set; }
            public String WorkflowId { get; set; }
            public String ParentWorkflowId { get; set; }
            public String FormattedValue { get; set; }
        }

        #endregion
    }
}