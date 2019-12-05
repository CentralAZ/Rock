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
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Web;
using Rock;
using Rock.Data;
using Rock.Model;
namespace com.centralaz.RoomManagement.Model
{
    /// <summary>
    /// A Room Reservation Type
    /// </summary>
    [Table( "_com_centralaz_RoomManagement_ReservationType" )]
    [DataContract]
    public class ReservationType : Rock.Data.Model<ReservationType>, Rock.Data.IRockEntity
    {

        #region Entity Properties

        [DataMember]
        [MaxLength( 50 )]
        public string Name { get; set; }

        [DataMember]
        public string Description { get; set; }

        [DataMember]
        public bool IsActive { get; set; }

        [DataMember]
        public bool IsSystem { get; set; }

        [DataMember]
        public string IconCssClass { get; set; }

        [DataMember]
        public int? FinalApprovalGroupId { get; set; }

        [DataMember]
        public int? SuperAdminGroupId { get; set; }

        [DataMember]
        public int? NotificationEmailId { get; set; }

        [DataMember]
        public int? DefaultSetupTime { get; set; }

        [DataMember]
        public bool IsCommunicationHistorySaved { get; set; }

        [DataMember]
        public bool IsNumberAttendingRequired { get; set; }

        [DataMember]
        public bool IsContactDetailsRequired { get; set; }

        [DataMember]
        public bool IsSetupTimeRequired { get; set; }

        [DataMember]
        public bool IsReservationBookedOnApproval { get; set; }

        #endregion

        #region Virtual Properties

        [LavaInclude]
        public virtual Group FinalApprovalGroup { get; set; }

        [LavaInclude]
        public virtual Group SuperAdminGroup { get; set; }

        [LavaInclude]
        public virtual SystemEmail NotificationEmail { get; set; }

        [LavaInclude]
        public virtual ICollection<Reservation> Reservations
        {
            get { return _reservations ?? ( _reservations = new Collection<Reservation>() ); }
            set { _reservations = value; }
        }
        private ICollection<Reservation> _reservations;

        [LavaInclude]
        public virtual ICollection<ReservationMinistry> ReservationMinistries
        {
            get { return _reservationMinistries ?? ( _reservationMinistries = new Collection<ReservationMinistry>() ); }
            set { _reservationMinistries = value; }
        }
        private ICollection<ReservationMinistry> _reservationMinistries;

        [LavaInclude]
        public virtual ICollection<ReservationWorkflowTrigger> ReservationWorkflowTriggers
        {
            get { return _reservationWorkflowTriggers ?? ( _reservationWorkflowTriggers = new Collection<ReservationWorkflowTrigger>() ); }
            set { _reservationWorkflowTriggers = value; }
        }
        private ICollection<ReservationWorkflowTrigger> _reservationWorkflowTriggers;

        /// <summary>
        /// Gets the supported actions.
        /// </summary>
        /// <value>
        /// The supported actions.
        /// </value>
        [NotMapped]
        public override Dictionary<string, string> SupportedActions
        {
            get
            {
                var supportedActions = base.SupportedActions;
                supportedActions.AddOrReplace( Rock.Security.Authorization.DELETE, "Additional roles and/or users that have access to delete reservations outside of the traditional approval process." );
                return supportedActions;
            }
        }

        #endregion

    }

    #region Entity Configuration


    public partial class ReservationTypeConfiguration : EntityTypeConfiguration<ReservationType>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ReservationTypeConfiguration"/> class.
        /// </summary>
        public ReservationTypeConfiguration()
        {
            this.HasOptional( r => r.FinalApprovalGroup ).WithMany().HasForeignKey( r => r.FinalApprovalGroupId ).WillCascadeOnDelete( false );
            this.HasOptional( r => r.SuperAdminGroup ).WithMany().HasForeignKey( r => r.SuperAdminGroupId ).WillCascadeOnDelete( false );
            this.HasOptional( r => r.NotificationEmail ).WithMany().HasForeignKey( r => r.NotificationEmailId ).WillCascadeOnDelete( false );

            // IMPORTANT!!
            this.HasEntitySetName( "ReservationType" );
        }
    }

    #endregion

}
