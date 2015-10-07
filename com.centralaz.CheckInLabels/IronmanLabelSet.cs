using System;
using System.Drawing;
using System.Text;
using System.Drawing.Printing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;

namespace com.centralaz.CheckInLabels
{
    /// <summary>
    /// This class represents the set of ironman check-in labels.
    /// </summary>
    public class IronmanLabelSet
    {
        #region Constructors

        //Default constructor
        public IronmanLabelSet()
        {
        }

        #endregion

        #region Protected Members
        private const string possibleInvalidReason = "If you believe the printer is valid and your server is virtualized, this may be caused by incompatible printer drivers.";
        #endregion

        #region NameTag Properties


        protected DateTime _CheckInDate = DateTime.Now;
        public DateTime CheckInDate
        {
            get { return _CheckInDate; }
            set { _CheckInDate = value; }
        }

        protected string _FirstName = string.Empty;
        public string FirstName
        {
            get { return _FirstName; }
            set { _FirstName = value; }
        }

        protected string _LastName = string.Empty;
        public string LastName
        {
            get { return _LastName; }
            set { _LastName = value; }
        }

        protected string _FullName = string.Empty;
        public string FullName
        {
            get { return _FullName; }
            set { _FullName = value; }
        }

        protected string _LogoImageFile = @"C:\Inetpub\wwwroot\CheckIn\images\xlogo_bw_lg.bmp";
        public string LogoImageFile
        {
            get { return _LogoImageFile; }
            set { _LogoImageFile = value; }
        }

        protected string _BirthdayImageFile = @"C:\Inetpub\wwwroot\CheckIn\images\cake.bmp";
        public string BirthdayImageFile
        {
            get { return _BirthdayImageFile; }
            set { _BirthdayImageFile = value; }
        }

        protected DateTime _BirthdayDate = DateTime.MinValue;
        public DateTime BirthdayDate
        {
            get { return _BirthdayDate; }
            set { _BirthdayDate = value; }
        }

        protected string _RoomName = string.Empty;
        public string RoomName
        {
            get { return _RoomName; }
            set { _RoomName = value; }
        }

        #endregion

        #region Public Instance Methods

        /// <summary>
        /// This method will print the Attendance Label and Claim Card to the given printer.
        /// </summary>
        /// <param name="printerURL">the URI/URL of the printer.</param>
        /// <exception cref="Exception">is thrown if the given printer is invalid or
        /// if a problem occurs when printing.</exception>
        public void PrintLabel( string printerURL)
        {

            PrintDocument pDoc = new PrintDocument();

            // hook up the event handler for the PrintPage
            // method, which is where we will build our	document
            pDoc.PrintPage += new PrintPageEventHandler( pEvent_PrintLabel );

            pDoc.PrinterSettings.PrinterName = printerURL;

            // Now check to see if the printer is available
            // and call the Print method
            if ( pDoc.PrinterSettings.IsValid )
            {
                pDoc.Print();
            }
            else
            {
                throw new Exception( "The printer, " + printerURL + ", is not valid. " + possibleInvalidReason );
            }

        }

        /// <summary>
        /// A string representation of a PrinterLabel.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append( "AttendanceLabel -> " );

            sb.Append( "FirstName [" + this.FirstName + "] : " );
            sb.Append( "FullName [" + this.FullName + "] : " );

            return ( sb.ToString() );
        }

        public IronmanLabelSet ShallowCopy()
        {
            return (IronmanLabelSet)this.MemberwiseClone();
        }

        #endregion

        #region Protected Instance Methods

        /// <summary>
        /// This is the event handler for printing all the labels.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void pEvent_PrintLabel( object sender, PrintPageEventArgs e )
        {

            int labelwidth = 216;  // 2.25 inches * 96dpi
            int labelheight = 220;//192;  // 2 inches * 96dpi

            //String format used to center text on label
            StringFormat format = new StringFormat();
            // Define the "brush" for printing
            SolidBrush br = new SolidBrush( Color.Black );
            Rectangle rectangle = new Rectangle();

            Bitmap bmp = new Bitmap( labelwidth, labelheight, PixelFormat.Format8bppIndexed );

            Graphics g = e.Graphics;

            // smothing mode on
            g.SmoothingMode = SmoothingMode.AntiAlias;

            /*******************************************************************/
            /*                              Age Group                          */
            /*******************************************************************/

            //Set color to black
            br.Color = Color.Black;
            g.FillRectangle( br, 0, 0, labelwidth, 20 );

            //draw white text over rectangle, starting at top and about 15 down
            rectangle.X = 0;
            rectangle.Y = 2;
            //Set width of the position rectangle to the width variable
            rectangle.Width = labelwidth;

            //set the alignment of the text 
            format.Alignment = StringAlignment.Far;

            //Set text color to white
            br.Color = Color.White;

            /*******************************************************************/
            /*                              RoomName                           */
            /*******************************************************************/

            //bool printRoomName;
            //
            //if (bool.TryParse(BlahBlahBlah.SomeSortOf.Settings["Cccev.DisplayRoomNameOnNameTag"], out printRoomName))
            //{
            //    if (printRoomName)
            //    {
            //        format.Alignment = StringAlignment.Near;
            //        g.DrawString(RoomName.Length > 20 ? RoomName.Substring(0, 20) : RoomName, 
            //            new Font("Arial", 9, FontStyle.Bold), br, rectangle, format);
            //    }
            //}

            /*******************************************************************/
            /*                           FirstName                             */
            /*******************************************************************/
            br.Color = Color.Black;

            //String format used to center text on label
            format.Alignment = StringAlignment.Near;

            //Set X Position to 0 (left) and Y position down a bit
            rectangle.X = -5;
            rectangle.Y = 20;

            // Set rectangle's width to width of label
            rectangle.Width = labelwidth;

            string firstName = this.FirstName;

            // Resize based on the length of the person's firstname
            int fontSize = 35; // size for names 4 chars in length or less
            if ( 5 < this.FirstName.Length && this.FirstName.Length <= 7 )
            {
                rectangle.X = -3;
                rectangle.Y = 30;
                fontSize = 30;
            }
            else if ( 8 <= this.FirstName.Length && this.FirstName.Length <= 10 )
            {
                rectangle.X = 0;
                rectangle.Y = 35;
                fontSize = 25; //
            }
            else if ( 11 <= this.FirstName.Length )
            {
                rectangle.X = 2;
                rectangle.Y = 40;
                fontSize = 20; // max size
                if ( firstName.Length >= 13 )
                    firstName = firstName.Substring( 0, 13 );
            }

            g.DrawString( firstName, new Font( "Arial", fontSize, FontStyle.Bold ), br, rectangle, format );

            /*******************************************************************/
            /*                           Lastname                              */
            /*******************************************************************/
            br.Color = Color.Black;

            //String format used to center text on label
            format.Alignment = StringAlignment.Near;

            //Set X Position to 0 (left) and Y position down a bit
            rectangle.X = 5;  // from left
            rectangle.Y = 70; // from top

            // Set rectangle's width to width of label
            rectangle.Width = labelwidth;

            // Resize based on the length of the person's firstname
            fontSize = 15;
            if ( 16 <= this.LastName.Length )
            {
                fontSize = 10;
            }

            // 7/9/2007 Per Julie B and Steve H, don't print lastnames.
            //g.DrawString(this.LastName, new Font("Arial", fontSize, FontStyle.Bold), br, rectangle, format);

            /*******************************************************************/
            /*                             Separator Line                      */
            /*******************************************************************/

            //Set color to black
            br.Color = Color.Black;
            g.FillRectangle( br, 0, 95, labelwidth, 1 );

            /*******************************************************************/
            /*                             Birthday Cake or Logo               */
            /*******************************************************************/
            // Try to process the images, but don't die if unable to find them
            try
            {
                System.Drawing.Image img;
                // Load a graphic from a file...
                // based on whether it is the person's birthday this week.
                // BUG FIX: #466 http://redmine.refreshcache.com/issues/466
                var nextBirthday = this.BirthdayDate.AddYears( DateTime.Today.Year - this.BirthdayDate.Year );
                if ( nextBirthday < DateTime.Today )
                {
                    nextBirthday = nextBirthday.AddYears( 1 );
                }
                var numDays = ( nextBirthday - DateTime.Today ).Days;
                if ( this.BirthdayDate != DateTime.MinValue && numDays <= 7 )
                {
                    img = System.Drawing.Image.FromFile( this._BirthdayImageFile, true );

                    // determine which day of the week the birthday falls on this year:
                    string dowBirthdayThisYear = new DateTime( DateTime.Now.Year, this.BirthdayDate.Month, this.BirthdayDate.Day ).DayOfWeek.ToString();
                    if ( numDays == 0 )
                    {
                        dowBirthdayThisYear = "Today!";
                    }
                    // write the DayOfWeek that the birthday occurs under the image
                    br.Color = Color.Black;
                    format.Alignment = StringAlignment.Center;
                    RectangleF dayOfWeekRect = new RectangleF( 130.0F, 173.0F, 56.0F, 13.0F );
                    g.DrawString( dowBirthdayThisYear, new Font( "Arial", 7 ), br, dayOfWeekRect, format );
                }
                else
                {
                    img = System.Drawing.Image.FromFile( this._LogoImageFile, true );
                }

                // Define a rectangle to locate the graphic:
                // x,y ,width, height (where x,y is the coord of the upper left corner of the rectangle)
                RectangleF rect = new RectangleF( 130.0F, 115.0F, 56.0F, 56.0F );

                // Add the image to the document
                g.DrawImage( img, rect );
            }
            catch { }

        }

        #endregion
    }
}