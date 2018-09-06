﻿// <copyright>
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
using System.ComponentModel.Composition;
using System.Linq;

using Rock;
using Rock.Attribute;
using Rock.CheckIn;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;
using Rock.Workflow;
using Rock.Workflow.Action.CheckIn;

namespace com.centralaz.Workflow.Action.CheckIn
{
    /// <summary>
    /// Saves the selected check-in data as attendance.
    /// </summary>
    [ActionCategory( "com_centralaz: Check-In" )]
    [Description( "Saves the selected check-in data as attendance after getting a security code for the checkin." )]
    [Export( typeof( ActionComponent ) )]
    [ExportMetadata( "ComponentName", "Save Attendance and Set Label Attribute (CentralAZ)" )]

    [WorkflowAttribute( "Will Label Print Attribute", "The attribute that determines whether the label will print." )]
    [GroupTypesField( "Checkin Types To Limit", "The Checkin Types that will be limited to one label print per group per schedule", false )]
    [IntegerField( "Number Of Alpha Characters", "Used to decide how many alpha characters should be included in the prefix of the security code.", true, 2 )]
    [IntegerField( "Number Of Numeric Characters", "Used to decide how many numeric characters should be included in the suffix of the security code. It is recommended to set no less than 4 to avoid problems obtaining a unique number.", true, 4 )]
    public class SaveAttendanceAndSetLabelAttribute : CheckInActionComponent
    {
        /// <summary>
        /// Executes the specified workflow.
        /// </summary>
        /// <param name="rockContext">The rock context.</param>
        /// <param name="action">The workflow action.</param>
        /// <param name="entity">The entity.</param>
        /// <param name="errorMessages">The error messages.</param>
        /// <returns></returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public override bool Execute( RockContext rockContext, WorkflowAction action, Object entity, out List<string> errorMessages )
        {
            var checkInState = GetCheckInState( entity, out errorMessages );
            if ( checkInState != null )
            {
                AttendanceCode attendanceCode = null;
                DateTime startDateTime = RockDateTime.Now;

                bool canPrintLabelMultipleTimes = true;
                if ( checkInState.CheckinTypeId.HasValue )
                {
                    var checkinType = GroupTypeCache.Get( checkInState.CheckinTypeId.Value );
                    if ( checkinType != null )
                    {
                        var guidList = GetAttributeValue( action, "CheckinTypesToLimit" ).SplitDelimitedValues().AsGuidList();

                        if ( guidList.Contains( checkinType.Guid ) )
                        {
                            canPrintLabelMultipleTimes = false;
                        }
                    }
                }

                bool reuseCodeForFamily = checkInState.CheckInType != null && checkInState.CheckInType.ReuseSameCode;
                int securityCodeAlphaLength = GetAttributeValue( action, "NumberOfAlphaCharacters" ).AsInteger();
                int securityCodeNumericLength = GetAttributeValue( action, "NumberOfNumericCharacters" ).AsInteger();

                var attendanceCodeService = new AttendanceCodeService( rockContext );
                var attendanceService = new AttendanceService( rockContext );
                var groupMemberService = new GroupMemberService( rockContext );
                var personAliasService = new PersonAliasService( rockContext );

                Dictionary<int, bool> canPrintLabel = new Dictionary<int, bool>();

                var family = checkInState.CheckIn.CurrentFamily;
                if ( family != null )
                {
                    foreach ( var person in family.GetPeople( true ) )
                    {
                        if ( reuseCodeForFamily && attendanceCode != null )
                        {
                            person.SecurityCode = attendanceCode.Code;
                        }
                        else
                        {
                            attendanceCode = AttendanceCodeService.GetNew( securityCodeAlphaLength, securityCodeNumericLength, isRandomized: false );
                            person.SecurityCode = attendanceCode.Code;
                        }

                        foreach ( var groupType in person.GetGroupTypes( true ) )
                        {
                            foreach ( var group in groupType.GetGroups( true ) )
                            {
                                if ( groupType.GroupType.AttendanceRule == AttendanceRule.AddOnCheckIn &&
                                    groupType.GroupType.DefaultGroupRoleId.HasValue &&
                                    !groupMemberService.GetByGroupIdAndPersonId( group.Group.Id, person.Person.Id, true ).Any() )
                                {
                                    var groupMember = new GroupMember();
                                    groupMember.GroupId = group.Group.Id;
                                    groupMember.PersonId = person.Person.Id;
                                    groupMember.GroupRoleId = groupType.GroupType.DefaultGroupRoleId.Value;
                                    groupMemberService.Add( groupMember );
                                }

                                foreach ( var location in group.GetLocations( true ) )
                                {
                                    foreach ( var schedule in location.GetSchedules( true ) )
                                    {
                                        // Only create one attendance record per day for each person/schedule/group/location
                                        var attendance = attendanceService.Get( startDateTime, location.Location.Id, schedule.Schedule.Id, group.Group.Id, person.Person.Id );
                                        if ( attendance == null )
                                        {
                                            var primaryAlias = personAliasService.GetPrimaryAlias( person.Person.Id );
                                            if ( primaryAlias != null )
                                            {
                                                attendance = rockContext.Attendances.Create();
                                                attendance.LocationId = location.Location.Id;
                                                attendance.CampusId = location.CampusId;
                                                attendance.ScheduleId = schedule.Schedule.Id;
                                                attendance.GroupId = group.Group.Id;
                                                attendance.PersonAlias = primaryAlias;
                                                attendance.PersonAliasId = primaryAlias.Id;
                                                attendance.DeviceId = checkInState.Kiosk.Device.Id;
                                                attendance.SearchTypeValueId = checkInState.CheckIn.SearchType.Id;
                                                attendanceService.Add( attendance );
                                                
                                                canPrintLabel.AddOrReplace( person.Person.Id, true );
                                            }
                                        }
                                        else
                                        {
                                            canPrintLabel.AddOrReplace( person.Person.Id, canPrintLabelMultipleTimes );
                                        }

                                        attendance.CreatedDateTime = RockDateTime.Now;
                                        attendance.AttendanceCodeId = attendanceCode.Id;
                                        attendance.StartDateTime = startDateTime;
                                        attendance.EndDateTime = null;
                                        attendance.DidAttend = true;

                                        KioskLocationAttendance.AddAttendance( attendance );
                                    }
                                }
                            }
                        }
                    }

                    SetCanPrintLabelValue( rockContext, action, canPrintLabel );
                }

                rockContext.SaveChanges();
                return true;
            }

            return false;
        }

        private void SetCanPrintLabelValue( RockContext rockContext, WorkflowAction action, Dictionary<int, bool> canPrintLabelDictionary )
        {
            Guid guid = GetAttributeValue( action, "WillLabelPrintAttribute" ).AsGuid();
            if ( !guid.IsEmpty() )
            {
                var attribute = AttributeCache.Get( guid, rockContext );
                if ( attribute != null )
                {
                    if ( attribute.EntityTypeId == new Rock.Model.Workflow().TypeId )
                    {
                        action.Activity.Workflow.SetAttributeValue( attribute.Key, canPrintLabelDictionary.ToJson() );
                        action.AddLogEntry( string.Format( "Set '{0}' attribute to '{1}'.", attribute.Name, canPrintLabelDictionary.ToJson() ) );
                    }
                    else if ( attribute.EntityTypeId == new Rock.Model.WorkflowActivity().TypeId )
                    {
                        action.Activity.SetAttributeValue( attribute.Key, canPrintLabelDictionary.ToJson() );
                        action.AddLogEntry( string.Format( "Set '{0}' attribute to '{1}'.", attribute.Name, canPrintLabelDictionary.ToJson() ) );
                    }
                }
            }
        }
    }
}