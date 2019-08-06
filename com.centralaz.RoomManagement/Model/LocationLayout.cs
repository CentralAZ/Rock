﻿// <copyright>
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
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using System.Runtime.Serialization;

using Rock.Model;
using Rock.Data;
using System.ComponentModel.DataAnnotations;
using System.Web;
using System.Text;

namespace com.centralaz.RoomManagement.Model
{
    /// <summary>
    /// A Location Layout
    /// </summary>
    [Table( "_com_centralaz_RoomManagement_LocationLayout" )]
    [DataContract]
    public class LocationLayout : Rock.Data.Model<LocationLayout>, Rock.Data.IRockEntity
    {

        #region Entity Properties

        [DataMember]
        public int LocationId { get; set; }

        [DataMember]
        public int? LayoutPhotoId { get; set; }

        [MaxLength( 50 )]
        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public string Description { get; set; }

        [DataMember]
        public bool IsActive { get; set; }

        [DataMember]
        public bool IsDefault { get; set; }

        [DataMember]
        public bool IsSystem { get; set; }

        #endregion

        #region Virtual Properties

        [LavaInclude]
        [NotMapped]
        public virtual string LayoutPhotoUrl
        {
            get
            {
                return LocationLayout.GetLayoutPhotoUrl( this );
            }

            private set
            {
                // intentionally blank
            }
        }

        public virtual BinaryFile LayoutPhoto { get; set; }

        [DataMember]
        public virtual Location Location { get; set; }

        #endregion

        #region Methods

        public static string GetLayoutPhotoUrl( LocationLayout locationLayout, int? maxWidth = null, int? maxHeight = null )
        {
            return GetLayoutPhotoUrl( locationLayout.Id, locationLayout.LayoutPhotoId, maxWidth, maxHeight );
        }

        public static string GetLayoutPhotoUrl( int locationLayoutId, int? maxWidth = null, int? maxHeight = null )
        {
            using ( RockContext rockContext = new RockContext() )
            {
                LocationLayout locationLayout = new LocationLayoutService( rockContext ).Get( locationLayoutId );
                return GetLayoutPhotoUrl( locationLayout, maxWidth, maxHeight );
            }
        }

        public static string GetLayoutPhotoUrl( int? locationLayoutId, int? layoutPhotoId, int? maxWidth = null, int? maxHeight = null )
        {
            string virtualPath = string.Empty;
            if ( layoutPhotoId.HasValue )
            {
                string widthHeightParams = string.Empty;
                if ( maxWidth.HasValue )
                {
                    widthHeightParams += string.Format( "&maxwidth={0}", maxWidth.Value );
                }

                if ( maxHeight.HasValue )
                {
                    widthHeightParams += string.Format( "&maxheight={0}", maxHeight.Value );
                }

                virtualPath = string.Format( "~/GetImage.ashx?id={0}" + widthHeightParams, layoutPhotoId );
            }

            if ( System.Web.HttpContext.Current == null )
            {
                return virtualPath;
            }
            else
            {
                return VirtualPathUtility.ToAbsolute( virtualPath );
            }
        }

        public static string GetLayoutPhotoImageTag( LocationLayout locationLayout, int? maxWidth = null, int? maxHeight = null, string altText = "", string className = "" )
        {
            if ( locationLayout != null )
            {
                return GetLayoutPhotoImageTag( locationLayout.Id, locationLayout.LayoutPhotoId, maxWidth, maxHeight, altText, className );
            }
            else
            {
                return GetLayoutPhotoImageTag( null, null, maxWidth, maxHeight, altText, className );
            }

        }

        public static string GetLayoutPhotoImageTag( int? locationLayoutId, int? layoutPhotoId, int? maxWidth = null, int? maxHeight = null, string altText = "", string className = "" )
        {
            var photoUrl = new StringBuilder();

            photoUrl.Append( VirtualPathUtility.ToAbsolute( "~/" ) );

            string styleString = string.Empty;

            string altString = string.IsNullOrWhiteSpace( altText ) ? string.Empty :
                string.Format( " alt='{0}'", altText );

            string classString = string.IsNullOrWhiteSpace( className ) ? string.Empty :
                string.Format( " class='{0}'", className );

            if ( layoutPhotoId.HasValue )
            {
                photoUrl.AppendFormat( "GetImage.ashx?id={0}", layoutPhotoId );
                if ( maxWidth.HasValue )
                {
                    photoUrl.AppendFormat( "&maxwidth={0}", maxWidth.Value );
                }

                if ( maxHeight.HasValue )
                {
                    photoUrl.AppendFormat( "&maxheight={0}", maxHeight.Value );
                }

                return string.Format( "<img src='{0}'{1}{2}{3}/>", photoUrl.ToString(), styleString, altString, classString );
            }

            return string.Empty;
        }

        public void CopyPropertiesFrom( LocationLayout source )
        {
            this.Id = source.Id;
            this.ForeignGuid = source.ForeignGuid;
            this.ForeignKey = source.ForeignKey;
            this.LayoutPhotoId = source.LayoutPhotoId;
            this.LocationId = source.LocationId;
            this.Name = source.Name;
            this.Description = source.Description;
            this.IsActive = source.IsActive;
            this.IsDefault = source.IsDefault;
            this.CreatedDateTime = source.CreatedDateTime;
            this.ModifiedDateTime = source.ModifiedDateTime;
            this.CreatedByPersonAliasId = source.CreatedByPersonAliasId;
            this.ModifiedByPersonAliasId = source.ModifiedByPersonAliasId;
            this.Guid = source.Guid;
            this.ForeignId = source.ForeignId;
        }

        #endregion
    }

    #region Entity Configuration


    public partial class LocationLayoutConfiguration : EntityTypeConfiguration<LocationLayout>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LocationLayoutConfiguration"/> class.
        /// </summary>
        public LocationLayoutConfiguration()
        {
            this.HasOptional( p => p.LayoutPhoto ).WithMany().HasForeignKey( p => p.LayoutPhotoId ).WillCascadeOnDelete( false );
            this.HasRequired( r => r.Location ).WithMany().HasForeignKey( r => r.LocationId ).WillCascadeOnDelete( false );

            // IMPORTANT!!
            this.HasEntitySetName( "LocationLayout" );
        }
    }

    #endregion
}
