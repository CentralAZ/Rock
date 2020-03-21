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
using System.Linq;
using System.Web.UI;
using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;
using Rock.Web.UI.Controls;

using com.centralaz.RoomManagement.Model;
using System.Web.UI.WebControls;

namespace RockWeb.Plugins.com_centralaz.Utility
{
    /// <summary>
    /// Block for viewing daily reservations
    /// </summary>
    [DisplayName( "Reservations By Date" )]
    [Category( "com_centralaz > Utlity" )]
    [Description( "A custom block for viewing a list of reservation on a particular day." )]

    [LinkedPage( "Detail Page" )]
    public partial class ReservationsByDate : Rock.Web.UI.RockBlock
    {
        #region Control Methods

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Init" /> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnInit( EventArgs e )
        {
            base.OnInit( e );

            dpDate.SelectedDate = RockDateTime.Now;

            gReservations.DataKeyNames = new string[] { "Id" };
            gReservations.Actions.ShowAdd = false;
            gReservations.GridRebind += gReservations_GridRebind;
        }

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Load" /> event.
        /// </summary>
        /// <param name="e">The <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnLoad( EventArgs e )
        {
            if ( !Page.IsPostBack )
            {
                BindGrid();
            }

            base.OnLoad( e );
        }

        #endregion

        #region Events

        /// <summary>
        /// Handles the Edit event of the gReservations control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RowEventArgs"/> instance containing the event data.</param>
        protected void gReservations_Edit( object sender, RowEventArgs e )
        {
            var parms = new Dictionary<string, string>();
            parms.Add( "ReservationId", e.RowKeyValue.ToString() );
            NavigateToLinkedPage( "DetailPage", parms );
        }

        /// <summary>
        /// Handles the GridRebind event of the gReservations control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void gReservations_GridRebind( object sender, EventArgs e )
        {
            BindGrid();
        }

        /// <summary>
        /// Handles the TextChanged event of the dpDate control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void dpDate_TextChanged( object sender, EventArgs e )
        {
            BindGrid();
        }

        /// <summary>
        /// Handles the TextChanged event of the cpCampus control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void cpCampus_TextChanged( object sender, EventArgs e )
        {
            BindGrid();
        }

        #endregion

        #region Methods

        /// <summary>
        /// Binds the grid.
        /// </summary>
        private void BindGrid()
        {
            var rockContext = new RockContext();
            var reservationService = new ReservationService( rockContext );
            var qry = reservationService.Queryable();

            // Filter by Campus
            if ( cpCampus.SelectedCampusId != null )
            {
                qry = qry.Where( r => r.CampusId == cpCampus.SelectedCampusId );
            }

            // Filter by Date
            var date = dpDate.SelectedDate.HasValue ? dpDate.SelectedDate.Value : RockDateTime.Now;
            var reservationSummaryList = reservationService.GetReservationSummaries( qry, date, date, true );

            // Bind to Grid
            gReservations.DataSource = reservationSummaryList.Select( r => new
            {
                Id = r.Id,
                ReservationName = r.ReservationName,
                Locations = r.ReservationLocations.Select( rl => rl.Location.Name ).ToList().AsDelimited( ", " ),
                Resources = r.ReservationResources.Select( rr => rr.Resource.Name ).ToList().AsDelimited( ", " ),
                EventStartDateTime = r.EventStartDateTime,
                EventEndDateTime = r.EventEndDateTime,
                ReservationStartDateTime = r.ReservationStartDateTime,
                ReservationEndDateTime = r.ReservationEndDateTime,
                EventDateTimeDescription = r.EventDateTimeDescription,
                EventContact = (r.EventContactPersonAlias != null ) ? r.EventContactPersonAlias.Person.FullName : string.Empty,
                Notes = r.Note,
                Setup = r.SetupPhotoId.HasValue ? string.Format("<img class='img-responsive' src='/GetImage.ashx?id={0}&width=100' />", r.SetupPhotoId.ToString() ) : "",
                ReservationDateTimeDescription = r.ReservationDateTimeDescription +
                    string.Format( " ({0})", ( r.ReservationStartDateTime.Date == r.ReservationEndDateTime.Date )
                        ? r.ReservationStartDateTime.DayOfWeek.ToStringSafe().Substring( 0, 3 )
                        : r.ReservationStartDateTime.DayOfWeek.ToStringSafe().Substring( 0, 3 ) + "-" + r.ReservationEndDateTime.DayOfWeek.ToStringSafe().Substring( 0, 3 ) ),
                ApprovalState = r.ApprovalState.ConvertToString()
            } )
            .OrderBy( r => r.ReservationStartDateTime ).ToList();
            gReservations.EntityTypeId = EntityTypeCache.Get<Reservation>().Id;
            gReservations.DataBind();
        }

        #endregion
    }
}