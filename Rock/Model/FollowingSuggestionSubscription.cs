﻿// <copyright>
// Copyright 2013 by the Spark Development Network
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//
using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using System.Runtime.Serialization;
using System.Text;

using Rock.Data;

namespace Rock.Model
{
    /// <summary>
    /// Represents an instance where a <see cref="Rock.Model.Person"/> subscribes to a following event
    /// </summary>
    [Table( "FollowingSuggestionSubscription" )]
    [DataContract]
    public partial class FollowingSuggestionSubscription : Model<FollowingSuggestionSubscription>
    {

        #region Entity Properties

        /// <summary>
        /// Gets or sets the entity type identifier.
        /// </summary>
        /// <value>
        /// The entity type identifier.
        /// </value>
        [DataMember]
        public int SuggestionTypeId { get; set; }

        /// <summary>
        /// Gets or sets the PersonAliasId of the person that is following the Entity
        /// </summary>
        /// <value>
        /// The person alias identifier.
        /// </value>
        [DataMember]
        public int PersonAliasId { get; set; }

        #endregion

        #region Virtual Properties

        /// <summary>
        /// Gets or sets the type of the entity.
        /// </summary>
        /// <value>
        /// The type of the entity.
        /// </value>
        [DataMember]
        public virtual FollowingSuggestionType SuggestionType { get; set; }

        /// <summary>
        /// Gets or sets the person alias.
        /// </summary>
        /// <value>
        /// The person alias.
        /// </value>
        [DataMember]
        public virtual PersonAlias PersonAlias { get; set; }

        #endregion

        #region Public Methods

        #endregion

    }

    #region Entity Configuration

    /// <summary>
    /// File Configuration class.
    /// </summary>
    public partial class FollowingSuggestionSubscriptionConfiguration : EntityTypeConfiguration<FollowingSuggestionSubscription>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FollowingSuggestionSubscriptionConfiguration"/> class.
        /// </summary>
        public FollowingSuggestionSubscriptionConfiguration()
        {
            this.HasRequired( f => f.SuggestionType ).WithMany().HasForeignKey( f => f.SuggestionTypeId ).WillCascadeOnDelete( true );
            this.HasRequired( f => f.PersonAlias ).WithMany().HasForeignKey( f => f.PersonAliasId ).WillCascadeOnDelete( true );
        }
    }

    #endregion

}
