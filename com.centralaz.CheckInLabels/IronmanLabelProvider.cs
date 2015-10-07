using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using Rock;
using Rock.CheckIn;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;

namespace com.centralaz.CheckInLabels
{
    internal class IronmanLabelProvider : IPrintLabel
    {
        private RockContext rockContext = new RockContext();
        private IronmanLabelSet label;

        public IronmanLabelProvider()
        {
        }

        /// <summary>
        /// IPrintLabel implementation to print out name tags.
        /// </summary>
        void IPrintLabel.Print( CheckInLabel checkInLabel, CheckInPerson person, CheckInState checkInState, CheckInGroupType groupType )
        {
            IEnumerable<CheckInLabel> printFromServer = groupType.Labels.Where( l => l.PrintFrom == Rock.Model.PrintFrom.Server );
            if ( printFromServer.Any() )
            {
                string printerAddress = string.Empty;

                foreach ( var label in printFromServer )
                {
                    var labelCache = KioskLabel.Read( label.FileGuid );
                    if ( labelCache != null )
                    {
                        if ( !string.IsNullOrWhiteSpace( label.PrinterAddress ) )
                        {
                            printerAddress = label.PrinterAddress;
                            break;
                        }
                    }
                }

                if ( !string.IsNullOrWhiteSpace( printerAddress ) )
                {
                    InitLabel( checkInLabel, person, checkInState, groupType );
                    label.PrintLabel( printerAddress );
                }
            }

        }

        /// <summary>
        /// Intialize a person's label set with the information from the given person, occurrence(s),
        /// and attendance record.
        /// </summary>
        private void InitLabel( CheckInLabel checkInLabel, CheckInPerson attendee, CheckInState checkInState, CheckInGroupType groupType )
        {
            CheckInLocation firstLocation = null;
            label = new IronmanLabelSet
            {
                FirstName = attendee.Person.NickName.Trim() != string.Empty ? attendee.Person.NickName : attendee.Person.FirstName,
                LastName = attendee.Person.LastName,
                FullName = string.Format( "{0} {1}", attendee.Person.NickName, attendee.Person.LastName ),
                LogoImageFile = checkInLabel.MergeFields["CentralAZ.LogoImageFile"],
            };

            // Get start times from any selected schedules...
            // This section is only needed because we have a weird "Transfer: " chunk
            // on the label that lists all the services the person is checked into.
            StringBuilder services = new StringBuilder();
            foreach ( var group in groupType.Groups.Where( g => g.Selected ) )
            {
                foreach ( var location in group.Locations.Where( l => l.Selected ).OrderBy( e => e.Schedules.Min( s => s.StartTime ) ) )
                {
                    // Put the first location's name on the label
                    if ( firstLocation == null )
                    {
                        firstLocation = location;
                        label.RoomName = firstLocation.Location.Name;
                    }

                    foreach ( var schedule in location.Schedules.Where( s => s.Selected ) )
                    {
                        if ( services.Length > 0 )
                        {
                            services.Append( ", " );
                        }
                        services.Append( schedule.StartTime.Value.ToShortTimeString() );
                    }
                }
            }

        }
    }
}
