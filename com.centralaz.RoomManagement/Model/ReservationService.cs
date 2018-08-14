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
using System.Data.Entity;
using System.Linq;
using System.Text;
using Rock;
using Rock.Communication;
using Rock.Data;
using Rock.Model;

namespace com.centralaz.RoomManagement.Model
{
    /// <summary>
    /// 
    /// </summary>
    public class ReservationService : Service<Reservation>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ReservationService"/> class.
        /// </summary>
        /// <param name="context">The context.</param>
        public ReservationService( RockContext context ) : base( context ) { }

        #region Reservation Methods

        /// <summary>
        /// Gets the reservation summaries.
        /// </summary>
        /// <param name="qry">The qry.</param>
        /// <param name="filterStartDateTime">The filter start date time.</param>
        /// <param name="filterEndDateTime">The filter end date time.</param>
        /// <returns></returns>
        public List<ReservationSummary> GetReservationSummaries( IQueryable<Reservation> qry, DateTime filterStartDateTime, DateTime filterEndDateTime, bool roundToDay = false )
        {
            var qryStartDateTime = filterStartDateTime.AddMonths( -1 );
            var qryEndDateTime = filterEndDateTime.AddMonths( 1 );
            if ( roundToDay )
            {
                filterEndDateTime = filterEndDateTime.AddDays( 1 ).AddMilliseconds( -1 );
            }

            var reservations = qry.ToList();
            var reservationsWithDates = reservations
                .Select( r => new ReservationDate
                {
                    Reservation = r,
                    ReservationDateTimes = r.GetReservationTimes( qryStartDateTime, qryEndDateTime )
                } )
                .Where( r => r.ReservationDateTimes.Any() )
                .ToList();

            var reservationSummaryList = new List<ReservationSummary>();
            foreach ( var reservationWithDates in reservationsWithDates )
            {
                var reservation = reservationWithDates.Reservation;
                foreach ( var reservationDateTime in reservationWithDates.ReservationDateTimes )
                {
                    var reservationStartDateTime = reservationDateTime.StartDateTime.AddMinutes( -reservation.SetupTime ?? 0 );
                    var reservationEndDateTime = reservationDateTime.EndDateTime.AddMinutes( reservation.CleanupTime ?? 0 );

                    if (
                        ( ( reservationStartDateTime >= filterStartDateTime ) || ( reservationEndDateTime >= filterStartDateTime ) ) &&
                        ( ( reservationStartDateTime < filterEndDateTime ) || ( reservationEndDateTime < filterEndDateTime ) ) )
                    {
                        reservationSummaryList.Add( new ReservationSummary
                        {
                            Id = reservation.Id,
                            ReservationType = reservation.ReservationType,
                            ApprovalState = reservation.ApprovalState,
                            ReservationName = reservation.Name,
                            ReservationLocations = reservation.ReservationLocations.ToList(),
                            ReservationResources = reservation.ReservationResources.ToList(),
                            EventStartDateTime = reservationDateTime.StartDateTime,
                            EventEndDateTime = reservationDateTime.EndDateTime,
                            ReservationStartDateTime = reservationStartDateTime,
                            ReservationEndDateTime = reservationEndDateTime,
                            EventDateTimeDescription = GetFriendlyScheduleDescription( reservationDateTime.StartDateTime, reservationDateTime.EndDateTime ),
                            EventTimeDescription = GetFriendlyScheduleDescription( reservationDateTime.StartDateTime, reservationDateTime.EndDateTime, false ),
                            ReservationDateTimeDescription = GetFriendlyScheduleDescription( reservationDateTime.StartDateTime.AddMinutes( -reservation.SetupTime ?? 0 ), reservationDateTime.EndDateTime.AddMinutes( reservation.CleanupTime ?? 0 ) ),
                            ReservationTimeDescription = GetFriendlyScheduleDescription( reservationDateTime.StartDateTime.AddMinutes( -reservation.SetupTime ?? 0 ), reservationDateTime.EndDateTime.AddMinutes( reservation.CleanupTime ?? 0 ), false ),
                            ReservationMinistry = reservation.ReservationMinistry,
                            EventContactPersonAlias = reservation.EventContactPersonAlias,
                            EventContactEmail = reservation.EventContactEmail,
                            EventContactPhoneNumber = reservation.EventContactPhone,
                            SetupPhotoId = reservation.SetupPhotoId,
                            Note = reservation.Note,
                            RequesterAlias = reservation.RequesterAlias
                        } );
                    }
                }
            }
            return reservationSummaryList;
        }

        /// <summary>
        /// Gets the conflicting reservation summaries.
        /// </summary>
        /// <param name="newReservation">The new reservation.</param>
        /// <returns></returns>
        private IEnumerable<ReservationSummary> GetConflictingReservationSummaries( Reservation newReservation )
        {
            return GetConflictingReservationSummaries( newReservation, Queryable() );
        }

        /// <summary>
        /// Gets the conflicting reservation summaries.
        /// </summary>
        /// <param name="newReservation">The new reservation.</param>
        /// <param name="existingReservationQry">The existing reservation qry.</param>
        /// <returns></returns>
        private IEnumerable<ReservationSummary> GetConflictingReservationSummaries( Reservation newReservation, IQueryable<Reservation> existingReservationQry )
        {
            var newReservationSummaries = GetReservationSummaries( new List<Reservation>() { newReservation }.AsQueryable(), RockDateTime.Now.AddMonths( -1 ), RockDateTime.Now.AddYears( 1 ) );
            var conflictingSummaryList = GetReservationSummaries( existingReservationQry.AsNoTracking().Where( r => r.Id != newReservation.Id && r.ApprovalState != ReservationApprovalState.Denied ), RockDateTime.Now.AddMonths( -1 ), RockDateTime.Now.AddYears( 1 ) )
                .Where( currentReservationSummary => newReservationSummaries.Any( newReservationSummary =>
                 ( currentReservationSummary.ReservationStartDateTime > newReservationSummary.ReservationStartDateTime || currentReservationSummary.ReservationEndDateTime > newReservationSummary.ReservationStartDateTime ) &&
                 ( currentReservationSummary.ReservationStartDateTime < newReservationSummary.ReservationEndDateTime || currentReservationSummary.ReservationEndDateTime < newReservationSummary.ReservationEndDateTime )
                 ) );
            return conflictingSummaryList;
        }

        /// <summary>
        /// Gets the friendly schedule description.
        /// </summary>
        /// <param name="startDateTime">The start date time.</param>
        /// <param name="endDateTime">The end date time.</param>
        /// <param name="showDate">if set to <c>true</c> [show date].</param>
        /// <returns></returns>
        public string GetFriendlyScheduleDescription( DateTime startDateTime, DateTime endDateTime, bool showDate = true )
        {
            if ( startDateTime.Date == endDateTime.Date )
            {
                if ( showDate )
                {
                    return String.Format( "{0} {1} - {2}", startDateTime.ToString( "MM/dd" ), startDateTime.ToString( "hh:mmt" ).ToLower(), endDateTime.ToString( "hh:mmt" ).ToLower() );
                }
                else
                {
                    return String.Format( "{0} - {1}", startDateTime.ToString( "hh:mmt" ).ToLower(), endDateTime.ToString( "hh:mmt" ).ToLower() );
                }
            }
            else
            {
                return String.Format( "{0} {1} - {2} {3}", startDateTime.ToString( "MM/dd/yy" ), startDateTime.ToString( "hh:mmt" ).ToLower(), endDateTime.ToString( "MM/dd/yy" ), endDateTime.ToString( "hh:mmt" ).ToLower() );
            }
        }

        /// <summary>
        /// Generates the conflict information.
        /// </summary>
        /// <param name="reservation">The reservation.</param>
        /// <param name="detailPageRoute">The detail page route.</param>
        /// <returns></returns>
        public string GenerateConflictInfo( Reservation reservation, string detailPageRoute )
        {
            // Check to make sure that nothing has a scheduling conflict.
            bool hasConflict = false;
            StringBuilder sb = new StringBuilder();
            sb.Append( "<b>The following items are already reserved for the scheduled times:<br><ul>" );
            var reservedLocationIds = GetReservedLocationIds( reservation );

            // Check self
            string message = string.Empty;
            foreach ( var location in reservation.ReservationLocations.Where( l => reservedLocationIds.Contains( l.LocationId ) ) )
            {
                //sb.AppendFormat( "<li>{0}</li>", location.Location.Name );
                message = BuildLocationConflictHtmlList( reservation, location.Location.Id, detailPageRoute );
                if ( message != null )
                {
                    sb.AppendFormat( "<li>{0} due to:<ul>{1}</ul></li>", location.Location.Name, message );
                }
                else
                {
                    sb.AppendFormat( "<li>{0}</li>", location.Location.Name );
                }
                hasConflict = true;
            }

            // Check resources...
            foreach ( var resource in reservation.ReservationResources )
            {
                var availableQuantity = GetAvailableResourceQuantity( resource.Resource, reservation );
                if ( availableQuantity - resource.Quantity < 0 )
                {
                    sb.AppendFormat( "<li>{0} [note: only {1} available]</li>", resource.Resource.Name, availableQuantity );
                    hasConflict = true;
                }
            }

            if ( hasConflict )
            {
                sb.Append( "</ul>" );
                return sb.ToString();
            }
            else
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Updates the approval.
        /// </summary>
        /// <param name="reservation">The reservation.</param>
        /// <param name="currentReservationApprovalState">State of the current reservation approval.</param>
        /// <param name="person">The person.</param>
        /// <returns></returns>
        public Reservation UpdateApproval( Reservation reservation, ReservationApprovalState currentReservationApprovalState, Person person )
        {
            reservation.ApprovalState = currentReservationApprovalState;

            int? finalApprovalGroupId = null;
            bool inApprovalGroups = false;
            bool isSuperAdmin = false;
            finalApprovalGroupId = reservation.ReservationType.FinalApprovalGroupId;

            if ( !inApprovalGroups )
            {
                inApprovalGroups = isSuperAdmin = ReservationTypeService.IsPersonInGroupWithId( person, reservation.ReservationType.SuperAdminGroupId );
            }

            if ( !inApprovalGroups )
            {
                inApprovalGroups = ReservationTypeService.IsPersonInGroupWithId( person, reservation.ReservationType.FinalApprovalGroupId );
            }

            foreach ( var reservationResource in reservation.ReservationResources )
            {
                bool canApprove = CanPersonApproveReservationResource( person, isSuperAdmin, reservationResource );

                if ( reservationResource.ApprovalState == ReservationResourceApprovalState.Unapproved )
                {
                    if ( canApprove )
                    {
                        reservationResource.ApprovalState = ReservationResourceApprovalState.Approved;
                    }
                    else
                    {
                        reservation.ApprovalState = ReservationApprovalState.Unapproved;
                    }
                }
                else if ( reservationResource.ApprovalState == ReservationResourceApprovalState.Denied )
                {
                    reservation.ApprovalState = ReservationApprovalState.ChangesNeeded;
                }
            }

            foreach ( var reservationLocation in reservation.ReservationLocations )
            {
                bool canApprove = CanPersonApproveReservationLocation( person, isSuperAdmin, reservationLocation );

                if ( reservationLocation.ApprovalState == ReservationLocationApprovalState.Unapproved )
                {
                    if ( canApprove )
                    {
                        reservationLocation.ApprovalState = ReservationLocationApprovalState.Approved;
                    }
                    else
                    {
                        reservation.ApprovalState = ReservationApprovalState.Unapproved;
                        //  groupGuidList.Add( approvalGroupGuid.Value );
                    }
                }
                else if ( reservationLocation.ApprovalState == ReservationLocationApprovalState.Denied )
                {
                    reservation.ApprovalState = ReservationApprovalState.ChangesNeeded;
                }
            }

            if ( reservation.ApprovalState == ReservationApprovalState.Unapproved || reservation.ApprovalState == ReservationApprovalState.PendingReview || reservation.ApprovalState == ReservationApprovalState.ChangesNeeded )
            {
                if ( reservation.ReservationLocations.All( rl => rl.ApprovalState == ReservationLocationApprovalState.Approved ) && reservation.ReservationResources.All( rr => rr.ApprovalState == ReservationResourceApprovalState.Approved ) )
                {
                    if ( finalApprovalGroupId == null || isSuperAdmin )
                    {
                        reservation.ApprovalState = ReservationApprovalState.Approved;
                    }
                    else
                    {
                        reservation.ApprovalState = ReservationApprovalState.PendingReview;
                    }
                }
                else
                {
                    if ( reservation.ReservationLocations.Any( rl => rl.ApprovalState == ReservationLocationApprovalState.Denied ) || reservation.ReservationResources.Any( rr => rr.ApprovalState == ReservationResourceApprovalState.Denied ) )
                    {
                        reservation.ApprovalState = ReservationApprovalState.ChangesNeeded;
                    }
                }
            }

            if ( reservation.ApprovalState == ReservationApprovalState.Denied )
            {
                foreach ( var reservationLocation in reservation.ReservationLocations )
                {
                    reservationLocation.ApprovalState = ReservationLocationApprovalState.Denied;
                }

                foreach ( var reservationResource in reservation.ReservationResources )
                {
                    reservationResource.ApprovalState = ReservationResourceApprovalState.Denied;
                }
            }

            if ( reservation.ApprovalState == ReservationApprovalState.Approved )
            {
                reservation.ApproverAliasId = person.PrimaryAliasId;

                foreach ( var reservationLocation in reservation.ReservationLocations )
                {
                    reservationLocation.ApprovalState = ReservationLocationApprovalState.Approved;
                }

                foreach ( var reservationResource in reservation.ReservationResources )
                {
                    reservationResource.ApprovalState = ReservationResourceApprovalState.Approved;
                }
            }


            return reservation;
        }

        /// <summary>
        /// Determines whether this instance [can person approve reservation resource] the specified person.
        /// </summary>
        /// <param name="person">The person.</param>
        /// <param name="isSuperAdmin">if set to <c>true</c> [is super admin].</param>
        /// <param name="reservationResource">The reservation resource.</param>
        /// <returns>
        ///   <c>true</c> if this instance [can person approve reservation resource] the specified person; otherwise, <c>false</c>.
        /// </returns>
        public static bool CanPersonApproveReservationResource( Person person, bool isSuperAdmin, ReservationResource reservationResource )
        {
            bool canApprove = false;

            if ( isSuperAdmin )
            {
                canApprove = true;
            }
            else
            {
                if ( reservationResource.Resource.ApprovalGroupId == null )
                {
                    canApprove = true;
                }
                else
                {
                    if ( ReservationTypeService.IsPersonInGroupWithId( person, reservationResource.Resource.ApprovalGroupId ) )
                    {
                        canApprove = true;
                    }
                }
            }

            return canApprove;
        }

        /// <summary>
        /// Determines whether this instance [can person approve reservation location] the specified person.
        /// </summary>
        /// <param name="person">The person.</param>
        /// <param name="isSuperAdmin">if set to <c>true</c> [is super admin].</param>
        /// <param name="reservationLocation">The reservation location.</param>
        /// <returns>
        ///   <c>true</c> if this instance [can person approve reservation location] the specified person; otherwise, <c>false</c>.
        /// </returns>
        public static bool CanPersonApproveReservationLocation( Person person, bool isSuperAdmin, ReservationLocation reservationLocation )
        {
            bool canApprove = false;

            if ( isSuperAdmin )
            {
                canApprove = true;
            }
            else
            {
                reservationLocation.Location.LoadAttributes();
                var approvalGroupGuid = reservationLocation.Location.GetAttributeValue( "ApprovalGroup" ).AsGuidOrNull();

                if ( approvalGroupGuid == null )
                {
                    canApprove = true;
                }
                else
                {
                    if ( ReservationTypeService.IsPersonInGroupWithGuid( person, approvalGroupGuid ) )
                    {
                        canApprove = true;
                    }
                }
            }

            return canApprove;
        }

        /// <summary>
        /// Sends the notifications.
        /// </summary>
        /// <param name="reservation">The reservation.</param>
        public void SendNotifications( Reservation reservation )
        {
            var groupGuidList = new List<Guid>();
            groupGuidList.AddRange( reservation.ReservationResources.Where( rr => rr.ApprovalState == ReservationResourceApprovalState.Unapproved && rr.Resource.ApprovalGroupId != null ).Select( rr => rr.Resource.ApprovalGroup.Guid ) );
            foreach ( var reservationLocation in reservation.ReservationLocations.Where( rl => rl.ApprovalState == ReservationLocationApprovalState.Unapproved ).ToList() )
            {
                reservationLocation.Location.LoadAttributes();
                var approvalGroupGuid = reservationLocation.Location.GetAttributeValue( "ApprovalGroup" ).AsGuidOrNull();
                if ( approvalGroupGuid != null && approvalGroupGuid != Guid.Empty )
                {
                    groupGuidList.Add( approvalGroupGuid.Value );
                }
            }

            if ( reservation.ApprovalState == ReservationApprovalState.Unapproved || reservation.ApprovalState == ReservationApprovalState.ChangesNeeded )
            {
                var groups = new GroupService( Context as RockContext ).GetByGuids( groupGuidList.Distinct().ToList() );
                foreach ( var group in groups )
                {
                    var mergeFields = Rock.Lava.LavaHelper.GetCommonMergeFields( null );
                    mergeFields.Add( "Reservation", reservation );
                    var recipients = new List<RecipientData>();

                    foreach ( var person in group.Members
                                       .Where( m => m.GroupMemberStatus == GroupMemberStatus.Active )
                                       .Select( m => m.Person ) )
                    {
                        if ( person.IsEmailActive &&
                            person.EmailPreference != EmailPreference.DoNotEmail &&
                            !string.IsNullOrWhiteSpace( person.Email ) )
                        {
                            var personDict = new Dictionary<string, object>( mergeFields );
                            personDict.Add( "Person", person );
                            recipients.Add( new RecipientData( person.Email, personDict ) );
                        }
                    }

                    if ( recipients.Any() )
                    {
                        Email.Send( reservation.ReservationType.NotificationEmail.Guid, recipients, string.Empty, string.Empty, reservation.ReservationType.IsCommunicationHistorySaved );
                    }
                }
            }
        }

        #endregion

        #region Location Conflict Methods

        /// <summary>
        /// Gets the  location ids for any existing non-denied reservations that have the a location as the ones in the given newReservation object.
        /// </summary>
        /// <param name="newReservation">The new reservation.</param>
        /// <returns></returns>
        public List<int> GetReservedLocationIds( Reservation newReservation )
        {
            var locationService = new LocationService( new RockContext() );

            // Get any Locations related to those reserved by the new Reservation
            var newReservationLocationIds = newReservation.ReservationLocations.Select( rl => rl.LocationId ).ToList();
            var relevantLocationIds = new List<int>();
            relevantLocationIds.AddRange( newReservationLocationIds );
            relevantLocationIds.AddRange( newReservationLocationIds.SelectMany( l => locationService.GetAllAncestorIds( l ) ) );
            relevantLocationIds.AddRange( newReservationLocationIds.SelectMany( l => locationService.GetAllDescendentIds( l ) ) );

            // Get any Reservations containing related Locations
            var existingReservationQry = Queryable().Where( r => r.ReservationLocations.Any( rl => relevantLocationIds.Contains( rl.LocationId ) ) );

            // Check existing Reservations for conflicts
            IEnumerable<ReservationSummary> conflictingReservationSummaries = GetConflictingReservationSummaries( newReservation, existingReservationQry );

            // Grab any locations booked by conflicting Reservations
            var reservedLocationIds = conflictingReservationSummaries.SelectMany( currentReservationSummary =>
                    currentReservationSummary.ReservationLocations.Where( rl =>
                        rl.ApprovalState != ReservationLocationApprovalState.Denied )
                        .Select( rl => rl.LocationId )
                        )
                  .Distinct();

            var reservedLocationAndChildIds = new List<int>();
            reservedLocationAndChildIds.AddRange( reservedLocationIds );
            reservedLocationAndChildIds.AddRange( reservedLocationIds.SelectMany( l => locationService.GetAllAncestorIds( l ) ) );
            reservedLocationAndChildIds.AddRange( reservedLocationIds.SelectMany( l => locationService.GetAllDescendentIds( l ) ) );

            return reservedLocationAndChildIds;
        }

        /// <summary>
        /// Gets the conflicts for location identifier.
        /// </summary>
        /// <param name="locationId">The location identifier.</param>
        /// <param name="newReservation">The new reservation.</param>
        /// <returns></returns>
        public List<ReservationConflict> GetConflictsForLocationId( int locationId, Reservation newReservation )
        {
            var locationService = new LocationService( new RockContext() );

            var relevantLocationIds = new List<int>();
            relevantLocationIds.Add( locationId );
            relevantLocationIds.AddRange( locationService.GetAllAncestorIds( locationId ) );
            relevantLocationIds.AddRange( locationService.GetAllDescendentIds( locationId ) );

            // Get any Reservations containing related Locations
            var existingReservationQry = Queryable().Where( r => r.ReservationLocations.Any( rl => relevantLocationIds.Contains( rl.LocationId ) ) );

            // Check existing Reservations for conflicts
            IEnumerable<ReservationSummary> conflictingReservationSummaries = GetConflictingReservationSummaries( newReservation, existingReservationQry );
            var locationConflicts = conflictingReservationSummaries.SelectMany( currentReservationSummary =>
                    currentReservationSummary.ReservationLocations.Where( rl =>
                        rl.ApprovalState != ReservationLocationApprovalState.Denied &&
                        relevantLocationIds.Contains( rl.LocationId ) )
                     .Select( rl => new ReservationConflict
                     {
                         LocationId = rl.LocationId,
                         Location = rl.Location,
                         ReservationId = rl.ReservationId,
                         Reservation = rl.Reservation
                     } ) )
                 .Distinct()
                 .ToList();

            return locationConflicts;
        }

        /// <summary>
        /// Builds a conflict message string (as HTML List) and returns it if there are location conflicts.
        /// </summary>
        /// <param name="locationId">The location identifier.</param>
        /// <param name="rockContext">The rock context.</param>
        /// <returns>an HTML List if conflicts exists; null otherwise.</returns>
        public string BuildLocationConflictHtmlList( Reservation newReservation, int locationId, string detailPageRoute )
        {
            var conflicts = GetConflictsForLocationId( locationId, newReservation );

            if ( conflicts.Any() )
            {
                StringBuilder sb = new StringBuilder();
                detailPageRoute = detailPageRoute.StartsWith( "/" ) ? detailPageRoute : "/" + detailPageRoute;

                foreach ( var conflict in conflicts )
                {
                    sb.AppendFormat( "<li>{0} [on {1} via <a href='{4}?ReservationId={2}' target='_blank'>'{3}'</a>]</li>",
                        conflict.Location.Name,
                        conflict.Reservation.Schedule.ToFriendlyScheduleText(),
                        conflict.ReservationId,
                        conflict.Reservation.Name,
                        detailPageRoute
                        );
                }
                return sb.ToString();
            }
            else
            {
                return null;
            }
        }

        #endregion

        #region Resource Conflict Methods

        /// <summary>
        /// Gets the available resource quantity for the given resource, during the time (schedule)
        /// of the given resource -- but excluding the ones used by the given resource.
        /// </summary>
        /// <param name="resource">The resource.</param>
        /// <param name="reservation">The reservation.</param>
        /// <returns>a quantity of available resources at </returns>
        public int GetAvailableResourceQuantity( Resource resource, Reservation reservation )
        {
            // For each new reservation summary, make sure that the quantities of existing summaries that come into contact with it
            // do not exceed the resource's quantity

            // Get all existing non-denied reservations (for a huge time period; a month before now and a year after
            // now) which have the given resource in them.
            var existingReservationSummaries = GetReservationSummaries(
                Queryable().AsNoTracking()
                .Where( r => r.Id != reservation.Id && r.ApprovalState != ReservationApprovalState.Denied && r.ReservationResources.Any( rr => resource.Id == rr.ResourceId ) ),
                RockDateTime.Now.AddMonths( -1 ), RockDateTime.Now.AddYears( 1 ) );

            // Now narrow the reservations down to only the ones in the matching/overlapping time frame
            var reservedQuantities = GetReservationSummaries( new List<Reservation>() { reservation }.AsQueryable(), RockDateTime.Now.AddMonths( -1 ), RockDateTime.Now.AddYears( 1 ) )
                .Select( newReservationSummary =>
                    existingReservationSummaries.Where( currentReservationSummary =>
                     ( currentReservationSummary.ReservationStartDateTime > newReservationSummary.ReservationStartDateTime || currentReservationSummary.ReservationEndDateTime > newReservationSummary.ReservationStartDateTime ) &&
                     ( currentReservationSummary.ReservationStartDateTime < newReservationSummary.ReservationEndDateTime || currentReservationSummary.ReservationEndDateTime < newReservationSummary.ReservationEndDateTime )
                    )
                    .DistinctBy( reservationSummary => reservationSummary.Id )
                    .Sum( currentReservationSummary => currentReservationSummary.ReservationResources.Where( rr => rr.ApprovalState != ReservationResourceApprovalState.Denied && rr.ResourceId == resource.Id ).Sum( rr => rr.Quantity ) )
               );

            var maxReservedQuantity = reservedQuantities.Count() > 0 ? reservedQuantities.Max() : 0;
            return resource.Quantity - maxReservedQuantity;
        }

        /// <summary>
        /// Gets the conflicts for resource identifier.
        /// </summary>
        /// <param name="resourceId">The resource identifier.</param>
        /// <param name="newReservation">The new reservation.</param>
        /// <returns></returns>
        public List<ReservationConflict> GetConflictsForResourceId( int resourceId, Reservation newReservation )
        {
            // Get any Reservations containing related Locations
            var existingReservationQry = Queryable().Where( r => r.ReservationResources.Any( rl => rl.ResourceId == resourceId ) );

            // Check existing Reservations for conflicts
            IEnumerable<ReservationSummary> conflictingReservationSummaries = GetConflictingReservationSummaries( newReservation, existingReservationQry );
            var locationConflicts = conflictingReservationSummaries.SelectMany( currentReservationSummary =>
                    currentReservationSummary.ReservationResources.Where( rr =>
                        rr.ApprovalState != ReservationResourceApprovalState.Denied &&
                        rr.ResourceId == resourceId )
                     .Select( rr => new ReservationConflict
                     {
                         ResourceId = rr.ResourceId,
                         Resource = rr.Resource,
                         ResourceQuantity = rr.Quantity,
                         ReservationId = rr.ReservationId,
                         Reservation = rr.Reservation
                     } ) )
                 .Distinct()
                 .ToList();
            return locationConflicts;
        }

        #endregion

        /// <summary>
        /// Create a new non-persisted reservation using an existing reservation as a template. 
        /// </summary>
        /// <param name="reservationId">The identifier of a reservation to use as a template for the new reservation.</param>
        /// <returns></returns>
        public Reservation GetNewFromTemplate( int reservationId )
        {
            var item = this.Queryable()
                           .AsNoTracking()
                           .FirstOrDefault( x => x.Id == reservationId );

            if ( item == null )
            {
                throw new Exception( string.Format( "GetNewFromTemplate method failed. Reservation ID \"{0}\" could not be found.", reservationId ) );
            }

            // Deep-clone the Reservation and reset the properties that connect it to the permanent store.
            var newItem = item.Clone( false );

            newItem.Id = 0;
            newItem.Guid = Guid.NewGuid();
            newItem.ForeignId = null;
            newItem.ForeignGuid = null;
            newItem.ForeignKey = null;

            newItem.CreatedByPersonAlias = null;
            newItem.CreatedByPersonAliasId = null;
            newItem.CreatedDateTime = RockDateTime.Now;
            newItem.ModifiedByPersonAlias = null;
            newItem.ModifiedByPersonAliasId = null;
            newItem.ModifiedDateTime = RockDateTime.Now;

            // Clear the approval state since that would not be fair otherwise...
            newItem.ApprovalState = ReservationApprovalState.Unapproved;
            foreach ( var rl in newItem.ReservationLocations )
            {
                rl.ApprovalState = ReservationLocationApprovalState.Unapproved;
            }

            foreach ( var rr in newItem.ReservationResources )
            {
                rr.ApprovalState = ReservationResourceApprovalState.Unapproved;
            }

            return newItem;
        }

        #region Helper Classes

        public class ReservationSummary
        {
            public int Id { get; set; }
            public ReservationType ReservationType { get; set; }
            public ReservationApprovalState ApprovalState { get; set; }
            public String ReservationName { get; set; }
            public String EventDateTimeDescription { get; set; }
            public String EventTimeDescription { get; set; }
            public String ReservationDateTimeDescription { get; set; }
            public String ReservationTimeDescription { get; set; }
            public List<ReservationLocation> ReservationLocations { get; set; }
            public List<ReservationResource> ReservationResources { get; set; }
            public DateTime ReservationStartDateTime { get; set; }
            public DateTime ReservationEndDateTime { get; set; }
            public DateTime EventStartDateTime { get; set; }
            public DateTime EventEndDateTime { get; set; }
            public ReservationMinistry ReservationMinistry { get; set; }
            public PersonAlias EventContactPersonAlias { get; set; }
            public String EventContactPhoneNumber { get; set; }
            public String EventContactEmail { get; set; }
            public int? SetupPhotoId { get; set; }
            public string Note { get; set; }
            public PersonAlias RequesterAlias { get; set; }
        }

        public class ReservationDate
        {
            public Reservation Reservation { get; set; }
            public List<ReservationDateTime> ReservationDateTimes { get; set; }
        }

        public class ReservationConflict
        {
            public int LocationId { get; set; }

            public Location Location { get; set; }

            public int ResourceId { get; set; }

            public Resource Resource { get; set; }

            public int ResourceQuantity { get; set; }

            public int ReservationId { get; set; }

            public Reservation Reservation { get; set; }
        }

        #endregion
    }

    /// <summary>
    /// Extension Methods
    /// </summary>
    public static partial class ReservationExtensionMethods
    {
        /// <summary>
        /// Clones this Reservation object to a new Reservation object
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="deepCopy">if set to <c>true</c> a deep copy is made. If false, only the basic entity properties are copied.</param>
        /// <returns></returns>
        public static Reservation Clone( this Reservation source, bool deepCopy )
        {
            if ( deepCopy )
            {
                return source.Clone() as Reservation;
            }
            else
            {
                var target = new Reservation();
                target.CopyPropertiesFrom( source );
                return target;
            }
        }

        /// <summary>
        /// Copies the properties from another Reservation object to this Reservation object
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="source">The source.</param>
        public static void CopyPropertiesFrom( this Reservation target, Reservation source )
        {
            target.Id = source.Id;
            target.Name = source.Name;

            target.Schedule = source.Schedule;
            target.ScheduleId = source.ScheduleId;

            target.CampusId = source.CampusId;
            target.ReservationMinistryId = source.ReservationMinistryId;

            //target.ApprovalState = source.ApprovalState;
            target.RequesterAliasId = source.RequesterAliasId;
            //target.ApproverAliasId = source.ApproverAliasId;
            target.SetupTime = source.SetupTime;
            target.CleanupTime = source.CleanupTime;
            target.NumberAttending = source.NumberAttending;
            target.Note = source.Note;
            target.SetupPhotoId = source.SetupPhotoId;
            target.EventContactPersonAlias = source.EventContactPersonAlias;
            target.EventContactPersonAliasId = source.EventContactPersonAliasId;
            target.EventContactPhone = source.EventContactPhone;
            target.EventContactEmail = source.EventContactEmail;
            target.AdministrativeContactPersonAlias = source.AdministrativeContactPersonAlias;
            target.AdministrativeContactPersonAliasId = source.AdministrativeContactPersonAliasId;
            target.AdministrativeContactPhone = source.AdministrativeContactPhone;
            target.AdministrativeContactEmail = source.AdministrativeContactEmail;

            target.ReservationLocations = source.ReservationLocations;
            target.ReservationResources = source.ReservationResources;

            target.CreatedDateTime = source.CreatedDateTime;
            target.ModifiedDateTime = source.ModifiedDateTime;
            target.CreatedByPersonAliasId = source.CreatedByPersonAliasId;
            target.ModifiedByPersonAliasId = source.ModifiedByPersonAliasId;
            target.Guid = source.Guid;
            target.ForeignId = source.ForeignId;
            target.ForeignGuid = source.ForeignGuid;
            target.ForeignKey = source.ForeignKey;
        }
    }
}
