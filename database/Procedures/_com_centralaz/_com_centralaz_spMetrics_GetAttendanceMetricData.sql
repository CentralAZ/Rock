/*
<doc>
	<summary>
 		This stored procedure returns the following tables of metrics for the given parameters:

		Table 1: Reference data (e.g. IsCampus, IsHoliday, IsServicesOngoing).
		Table 2: Returns attendance totals sorted by attendance type.
		Table 3: Returns attendance totals sorted by service time.			
		Table 4: Returns attendance totals sorted by campus.
		Table 5: Returns worship center totals sorted by either campus or schedule based off of the IsCampus parameter.
		Table 6: Returns children totals sorted by either campus or schedule based off of the IsCampus parameter.
		Table 7: Returns students totals sorted by either campus or schedule based off of the IsCampus parameter.
		Table 8: Returns baptism totals sorted by either campus or schedule based off of the IsCampus parameter.
		Table 9: Returns baptism totals sorted by schedule. 
		Table 10: Returns baptism totals sorted by schedule.
		Table 11: The full metric values table with modified datetime and modified by (for auditing purposes)
	</summary>
	<returns>
		Returns 11 tables of data as described above.
	</returns>
	<param name="IsHoliday" datatype="bit">Boolean to indicate if the weekend is a holiday (special rules apply in that case)</param>
	<param name="IsCampus" datatype="bit">Boolean to indicate if a single campus should be used (0 for all campuses)</param>
	<param name="Holiday" datatype="NVARCHAR(50)">The name of the holiday: Christmas, Easter, Thanksgiving (ignored if IsHoliday = 0)</param>
	<param name="CampusId" datatype="int">The id of a campus (ignored if IsCampus = 0)</param>
	<param name="SundayDate" datatype="datetime">The date of a Sunday to target (optional, use NULL for last data weekend)</param>
	<remarks>	
	</remarks>
	<code>
		EXEC [dbo].[_com_centralaz_spMetrics_GetAttendanceMetricData] @IsHoliday, @IsCampus, @Holiday, @CampusId, @SundayDate

		-- Example getting metrics for Easter holiday for Gilbert campus
		EXEC [dbo].[_com_centralaz_spMetrics_GetAttendanceMetricData] 1, 1, 'Easter', 2, '4/1/2018'

		-- Example getting all campus metrics for non-holiday weekend 4/22/2018
		EXEC [dbo].[_com_centralaz_spMetrics_GetAttendanceMetricData] 0, 0, '', 0, '4/22/2018'

		-- Example getting all campus metrics for the last weekend that has data
		EXEC [dbo].[_com_centralaz_spMetrics_GetAttendanceMetricData] 0, 0, '', 0, 'NULL'
	</code>
</doc>
*/

IF EXISTS (select * from dbo.sysobjects where id = object_id(N'[dbo].[_com_centralaz_spMetrics_GetAttendanceMetricData]') and OBJECTPROPERTY(id, N'IsProcedure') = 1)
	DROP PROCEDURE [dbo].[_com_centralaz_spMetrics_GetAttendanceMetricData]
GO

SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[_com_centralaz_spMetrics_GetAttendanceMetricData]
	 @IsHoliday BIT = 0,
	 @IsCampus BIT = 0,
	 @Holiday NVARCHAR(50) = 'Christmas',
	 @CampusId INT = 1,
	 @SundayDate NVARCHAR(50) = NULL

AS
BEGIN
----------------------------------------------------------------------------
-- QUERY PARAMETERS
----------------------------------------------------------------------------

-- The following declarations are here to make it easy to extract the stored procedure into a new query
-- window for editing and testing. By default they should be commented out.
-- BEGIN FOR TESTING

--DECLARE @IsHoliday BIT = 0;
--DECLARE @IsCampus BIT = 1;
--DECLARE @Holiday NVARCHAR(50) = 'Christmas';
--DECLARE @CampusId INT = 1;
--DECLARE @SundayDate NVARCHAR(50) = NULL;

-- END FOR TESTING

-- If a service has been discontinued within the last year, its numbers will not get included while 
-- services are active (historical numbers are only included once a current value is entered). Because
-- of that is services are active, any historical metric with the campus/schedule entered here will 
-- also be included after the number of hours since the week started (monday morning @ 12:00 AM)
-- and will not require a current value be entered before including them.
DECLARE @OldServices TABLE (
	CampusId int, 
	ScheduleId int,
	AppearHoursAfterWeekStart int )
INSERT INTO @OldServices VALUES 
	( 2, 999, 136 )	-- Gilbert @ Sat 4:00pm; Appear after sat @ 4:00pm

DECLARE @SundayDateTime DATETIME = NULL

BEGIN TRY
	SET @SundayDateTime = Convert(DATE, @SundayDate);
END TRY

BEGIN CATCH

END CATCH 

----------------------------------------------------------------------------
-- GET THE METRIC CATEGORY IDS
----------------------------------------------------------------------------
-- These are the root category Ids of the metrics included in the total attendance counts
DECLARE @RootAttendanceMetricCategoryId INT = 540;

DECLARE @WorshipMetricCategoryId INT = 543;
DECLARE @ChildrenMetricCategoryId INT = 541;
DECLARE @StudentsMetricCategoryId INT = 542;
DECLARE @WorshipNightMetricCategoryId INT = 544;

-- This holds any metrics that are not included in total attendance counts but are still displayed.
-- Currently includes Baptisms and First Time Guests
DECLARE @UncountedMetricCategoryId INT = 545 

-- Here we build a table that contains all the categories for the metrics we'll be displaying on the page
DECLARE @MetricCategoryIds TABLE(
MetricCategoryId INT
);

INSERT INTO @MetricCategoryIds SELECT Id FROM dbo._com_centralaz_unfMetrics_GetDescendantCategoriesFromRoot(@RootAttendanceMetricCategoryId);

----------------------------------------------------------------------------
-- GET THE SCHEDULE CATEGORY IDS
----------------------------------------------------------------------------

-- These are the root category Ids of the schedules used in the total attendance counts
DECLARE @WeekendScheduleCategoryId INT = 50;
DECLARE @StudentsScheduleCategoryId INT = 496;
DECLARE @SpecialEventsCategoryId INT = 138;

DECLARE @HolidayScheduleCategoryId INT;
IF @Holiday = 'Christmas'
	SET @HolidayScheduleCategoryId = 448	
ELSE IF @Holiday = 'Easter'
	SET @HolidayScheduleCategoryId = 284
ELSE IF @Holiday = 'Thanksgiving'
	SET @HolidayScheduleCategoryId = 457
ELSE SET @HolidayScheduleCategoryId = 448

-- Here we build a table that contains all the categories for the schedules we'll be displaying on the page
DECLARE @ScheduleCategoryIds TABLE(
ScheduleCategoryId INT
);

IF @IsHoliday = 1
	-- This holds the correct holiday schedules
	INSERT INTO @ScheduleCategoryIds SELECT Id FROM dbo._com_centralaz_unfMetrics_GetDescendantCategoriesFromRoot(@HolidayScheduleCategoryId) 
ELSE
BEGIN
	-- These are normal weekend schedules
	INSERT INTO @ScheduleCategoryIds SELECT Id FROM dbo._com_centralaz_unfMetrics_GetDescendantCategoriesFromRoot(@WeekendScheduleCategoryId)

	-- These are the student's schedules ( Including Wed Night Bible Study )
	INSERT INTO @ScheduleCategoryIds SELECT Id FROM dbo._com_centralaz_unfMetrics_GetDescendantCategoriesFromRoot(@StudentsScheduleCategoryId) 

	-- These are schedules for events we want to track attendance for, like Worship Night
	INSERT INTO @ScheduleCategoryIds SELECT Id FROM dbo._com_centralaz_unfMetrics_GetDescendantCategoriesFromRoot(@SpecialEventsCategoryId) 
END

----------------------------------------------------------------------------
-- GET THE GENERIC METRICVALUE JOINED TABLE
----------------------------------------------------------------------------

DECLARE @MetricValuesTEMP table(
	[Id] [int] NULL,
	[MetricId] [int] NULL,
	[MetricCategoryId] [int] NULL,
	[MetricCategoryName] [nvarchar](50) NULL,
	[MetricCategoryOrder] [int] NULL,
	[Attendance] [float] NULL,
	[MetricValueDateTime] [datetime] NULL,
	[ModifiedBy] varchar(75),
	[ModifiedDateTime] datetime,
	[ScheduleId] [int] NULL,
	[ScheduleName] [nvarchar](50) NULL,
	[ScheduleICalendarContent] [nvarchar](max) NULL,
	[CampusId] [int] NULL,
	[CampusName] [nvarchar](50) NULL,
	[Note] [nvarchar](max) NULL,
	[MetricKeyString] [nvarchar](max) NULL,
	[GroupingData] [nvarchar](50) NULL
);

-- Here we dump all the relevant metrics and their accessory information into a custom table that 
-- we'll work off of from here on out
INSERT INTO @MetricValuesTEMP
SELECT mv.Id
	,m.Id
	,mc.CategoryId
	,cat.Name
	,CASE
		WHEN  mc.CategoryId = @WorshipMetricCategoryId THEN 1
		WHEN  mc.CategoryId = @WorshipNightMetricCategoryId THEN 2
		WHEN  mc.CategoryId = @ChildrenMetricCategoryId THEN 3
		WHEN  mc.CategoryId = @StudentsMetricCategoryId THEN 4
	END
	,mv.YValue
	,mv.MetricValueDateTime
	,dbo.fGetPersonName( mv.ModifiedByPersonAliasId ) as 'Modified By'
	,mv.ModifiedDateTime
	,s.Id
	,s.Name
	,s.iCalendarContent
	,c.Id
	,c.Name
	,mv.Note
	,STR(m.Id)+'-'+STR(c.Id)+'-'+s.Name
	,''
FROM MetricValue mv
JOIN Metric m ON mv.MetricId = m.Id
JOIN MetricValuePartition mvpC ON mvpC.MetricValueId = mv.Id
JOIN MetricPartition mpC ON mvpC.MetricPartitionId = mpC.Id AND mpC.EntityTypeId IN (SELECT TOP 1 Id FROM EntityType WHERE Name='Rock.Model.Campus')
JOIN Campus c ON mvpC.EntityId = c.Id
JOIN MetricValuePartition mvpS ON mvpS.MetricValueId = mv.Id
JOIN MetricPartition mpS ON mvpS.MetricPartitionId = mpS.Id AND mpS.EntityTypeId IN (SELECT TOP 1 Id FROM EntityType WHERE Name='Rock.Model.Schedule')
JOIN Schedule s ON mvpS.EntityId = s.Id
JOIN MetricCategory mc ON mc.MetricId = m.Id
JOIN Category cat ON cat.Id = mc.CategoryId
WHERE  mc.CategoryId IN (SELECT * FROM @MetricCategoryIds )
AND s.CategoryId IN (SELECT * FROM @ScheduleCategoryIds)
AND mv.YValue IS NOT NULL
AND ( @IsCampus = 0 OR c.id = @CampusId)

-- Select everything except the ModifiedBy and ModifiedDateTime data into the @MetricValues AttendanceMetricValueTableType
DECLARE @MetricValues as AttendanceMetricValueTableType;
INSERT INTO @MetricValues
SELECT
	[Id],
	[MetricId],
	[MetricCategoryId],
	[MetricCategoryName],
	[MetricCategoryOrder],
	[Attendance],
	[MetricValueDateTime],
	[ScheduleId],
	[ScheduleName],
	[ScheduleICalendarContent],
	[CampusId],
	[CampusName],
	[Note],
	[MetricKeyString],
	[GroupingData]
FROM @MetricValuesTEMP

----------------------------------------------------------------------------
-- GET THE DATE RANGES
----------------------------------------------------------------------------

-- First get the latest date where a valid metric was entered, (or the SundayDate parameter) 
DECLARE @ThisWeekEnd DATETIME ;
IF ( @SundayDateTime IS NULL) 
	SET @ThisWeekEnd  = (
		SELECT MAX(MetricValueDateTime) 
		FROM  @MetricValues
		WHERE MetricCategoryId <> @UncountedMetricCategoryId
		)
ELSE SET @ThisWeekEnd  = @SundayDateTime;

-- Next we'll grab the month ranges ( This month, Last Month, 'This Month Last Year')
DECLARE @ThisMonthStart DATETIME= DATEADD(mm, DATEDIFF(mm, 0, @ThisWeekEnd -1), 0)
DECLARE @ThisMonthEnd DATETIME = DATEADD(DAY, -1,DATEADD(mm, 1, @ThisMonthStart));

DECLARE @LastMonthStart DATETIME= DATEADD(mm, -1, @ThisMonthStart);
DECLARE @LastMonthEnd DATETIME= DATEADD(DAY, -1,DATEADD(mm, 1, @LastMonthStart));

DECLARE @LastYearMonthStart DATETIME= DATEADD(wk, -52, @ThisMonthStart);
DECLARE @LastYearMonthEnd DATETIME= DATEADD(DAY, -1,DATEADD(mm, 1, @LastYearMonthStart));

-- Next get the week ranges ( This week, last week, this week last Year). We do this after the month ranges
-- so that recalculating this week doesn't mess them up. NOTE:  We operate off of a Monday - Sunday week.
DECLARE @ThisWeekStart DATETIME= DATEADD(wk, DATEDIFF(wk, 0, @ThisWeekEnd -1), 0)
SET @ThisWeekEnd = DATEADD(DAY, 6, @ThisWeekStart);

DECLARE @LastWeekEnd DATETIME= DATEADD(DAY, -1, @ThisWeekStart);
DECLARE @LastWeekStart DATETIME= DATEADD(DAY, -6, @LastWeekEnd);

DECLARE @LastYearWeekStart DATETIME= DATEADD(wk, -52, @ThisWeekStart);
DECLARE @LastYearWeekEnd DATETIME= DATEADD(DAY, 6, @LastYearWeekStart);

-- Next get the year ranges. These are used by the holiday metrics
DECLARE @ThisYearEnd DATETIME = (SELECT MAX(MetricValueDateTime) FROM @MetricValues)
DECLARE @ThisYearStart DATETIME= DATEADD(DAY, -30, @ThisYearEnd)
SET @ThisYearEnd = DATEADD(DAY, 60, @ThisYearStart);

DECLARE @LastYearEnd DATETIME= DATEADD(YEAR, -1, @ThisYearEnd)
DECLARE @LastYearStart DATETIME= DATEADD(YEAR, -1, @ThisYearStart);

DECLARE @TwoYearStart DATETIME= DATEADD(YEAR, -2, @ThisYearStart);
DECLARE @TwoYearEnd DATETIME= DATEADD(YEAR, -2, @ThisYearEnd);

-- Finally, grab the ministry year start for this year AND last year
DECLARE @ThisMinistryYearStart DATETIME = DATEADD(mm, 7, DATEADD(yy,DATEDIFF(yy,0,@ThisWeekEnd),0));
IF(@ThisWeekEnd < @ThisMinistryYearStart)
	SET @ThisMinistryYearStart = DATEADD(yy,-1, @ThisMinistryYearStart);

DECLARE @LastMinistryYearStart DATETIME = DATEADD(yy,-1, @ThisMinistryYearStart);


DECLARE @FirstColumnStart DATETIME;
DECLARE @FirstColumnEnd DATETIME;
DECLARE @SecondColumnStart DATETIME;
DECLARE @SecondColumnEnd DATETIME;
DECLARE @ThirdColumnStart DATETIME;
DECLARE @ThirdColumnEnd DATETIME;

IF( @IsHoliday = 0)
BEGIN
	SET @FirstColumnStart = @ThisWeekStart;
	SET @FirstColumnEnd = @ThisWeekEnd;
	SET @SecondColumnStart = @LastWeekStart;
	SET @SecondColumnEnd = @LastWeekEnd ;
	SET @ThirdColumnStart = @LastYearWeekStart;
	SET @ThirdColumnEnd = @LastYearWeekEnd;
END
ELSE
BEGIN
	SET @FirstColumnStart = @ThisYearStart;
	SET @FirstColumnEnd = @ThisYearEnd;
	SET @SecondColumnStart = @LastYearStart;
	SET @SecondColumnEnd = @LastYearEnd ;
	SET @ThirdColumnStart = @TwoYearStart;
	SET @ThirdColumnEnd = @TwoYearEnd;
END


----------------------------------------------------------------------------
-- DETERMINE IF SERVICES ARE ONGOING (ie, the weekend is still happening/live)
----------------------------------------------------------------------------

-- This section determines if services are ongoing. This is because while services are ongoing,
-- it only shows past metrics in categories that have a metric this period, that way, instead of 
-- showing 370 this week vs 4500 last week, it shows metrics up to that point in time. 

DECLARE @CurrentDateTime DATETIME = GETDATE();
DECLARE @IsServicesOngoing BIT = 0;

-- Weekend Services are fairly simple: If it's 1) The current week and 2) between Thursday 6pm and Sunday 1pm, then services are ongoing
IF @IsHoliday = 0
BEGIN
	DECLARE @currentPeriodDay INTEGER = DATEPART(WEEKDAY, @CurrentDateTime);
	DECLARE @CurrentHour INTEGER = DATEPART(HOUR, @CurrentDateTime);
	DECLARE @CurrentSundayDate DATE = dbo.ufnUtility_GetSundayDate(@CurrentDateTime);

	IF @CurrentSundayDate = convert(DATE, @ThisWeekEnd)
	BEGIN
		IF @currentPeriodDay = 5 AND @CurrentHour >= 18
			 SET @IsServicesOngoing = 1
		ELSE IF @currentPeriodDay = 1 AND @CurrentHour < 13
			 SET @IsServicesOngoing = 1
	END
END

-- Holiday Services are more complicated, but also simpler. Unlike normal services times, if the user is viewing a 
-- holiday, then the only schedules in @MetricValues will be non-reccurring schedules for that particular holiday. 
-- Because of this, we can grab the earliest and latest service time in @MetricValues, and say that if the current
-- time is between the two, then services are ongoing

IF @IsHoliday = 1
BEGIN 
	DECLARE @ScheduleStartTimes TABLE(
		StartDateTime DATETIME
	);

	INSERT INTO @ScheduleStartTimes
	SELECT DISTINCT 
	CONVERT(DATETIME, STUFF(STUFF(STUFF(splitTable.Date+''+splitTable.Time,13,0,':'),11,0,':'),9,0,' ')) AS 'Datetime'
	FROM (
	SELECT DISTINCT
		SUBSTRING(ScheduleICalendarContent, CHARINDEX('DTSTART:', ScheduleICalendarContent) + 8, (CHARINDEX('SEQUENCE:', ScheduleICalendarContent)-(CHARINDEX('DTSTART:', ScheduleICalendarContent)+17))) AS 'Date'
		,SUBSTRING(ScheduleICalendarContent, CHARINDEX('DTSTART:', ScheduleICalendarContent) + 17, (CHARINDEX('SEQUENCE:', ScheduleICalendarContent)-(CHARINDEX('DTSTART:', ScheduleICalendarContent)+19))) AS 'Time'
		,ScheduleICalendarContent
		 FROM @MetricValues
	 ) AS splitTable

	DECLARE @StartOfServices DATETIME = (SELECT MIN(StartDateTime) FROM @ScheduleStartTimes);
	DECLARE @EndOfServices DATETIME =DATEADD(HOUR, 1, (SELECT MAX(StartDateTime) FROM @ScheduleStartTimes));

	IF (@CurrentDateTime >= @StartOfServices AND @CurrentDateTime < @EndOfServices)
		SET @IsServicesOngoing = 1;
END

----------------------------------------------------------------------------
-- Populate Individual Tables
----------------------------------------------------------------------------

-- In order to increase performance, we'll create several temp tables with individual ministries' data ahead of time 
-- so that we don't have to make multiple WHERE calls on a large table

-- We first build the tables for first-time guests, Discover Central, and baptisms, because they use different time periods
-- than the other tables, and because we'll be pulling them out of @MetricValues in the next step
DECLARE @BaptismMetricValues AS AttendanceMetricValueTableType;
INSERT INTO @BaptismMetricValues
SELECT * FROM @MetricValues
WHERE MetricId = 27

DECLARE @DiscoverCentralMetricValues AS AttendanceMetricValueTableType;
INSERT INTO @DiscoverCentralMetricValues
SELECT * FROM @MetricValues
WHERE MetricId = 73

DECLARE @FirstTimeGuestMetricValues AS AttendanceMetricValueTableType;
INSERT INTO @FirstTimeGuestMetricValues
SELECT * FROM @MetricValues
WHERE MetricId = 74

-- Next we'll slim down the @MetricValues table to only include values counted towards totals, as well as only 
-- including values that fall within the time periods that will be shown
Delete
FROM @MetricValues
WHERE MetricCategoryId = @UncountedMetricCategoryId
OR NOT (
		(
			( MetricValueDateTime >= @FirstColumnStart AND MetricValueDateTime <= @FirstColumnEnd) OR
			( MetricValueDateTime >= @SecondColumnStart AND MetricValueDateTime <= @SecondColumnEnd) OR
			( MetricValueDateTime >= @ThirdColumnStart AND MetricValueDateTime <= @ThirdColumnEnd)
		)
	)

-- Finally, we'll partition off any attendance metrics for the worship center, children's ministry, and students' ministry 
-- into their own temp tables
DECLARE @WorshipCenterMetricValues AS AttendanceMetricValueTableType;
INSERT INTO @WorshipCenterMetricValues
SELECT * FROM @MetricValues
WHERE  MetricCategoryId = @WorshipMetricCategoryId

DECLARE @ChildrensMetricValues AS AttendanceMetricValueTableType;
INSERT INTO @ChildrensMetricValues
SELECT * FROM @MetricValues
WHERE MetricCategoryId = @ChildrenMetricCategoryId

DECLARE @StudentsMetricValues AS AttendanceMetricValueTableType;
INSERT INTO @StudentsMetricValues
SELECT * FROM @MetricValues
WHERE MetricCategoryId = @StudentsMetricCategoryId

----------------------------------------------------------------------------
-- GRAB THE EXISTING CURRENT METRIC VALUES
----------------------------------------------------------------------------

-- Here we grab keystrings of current metric values to use while services are ongoing.
DECLARE @CurrentMetrics AS MetricKeyStringTableType;
-- Columns in this type:
	--KeyString NVARCHAR(MAX),

INSERT INTO @CurrentMetrics
SELECT V.[MetricKeyString]
FROM @MetricValues V
LEFT OUTER JOIN @OldServices O
	ON O.[CampusId] = V.[CampusId]
	AND O.[ScheduleId] = V.[ScheduleId]
	AND @CurrentDateTime >= DATEADD( hour, O.[AppearHoursAfterWeekStart], @ThisWeekStart )
WHERE ( MetricValueDateTime >= @ThisWeekStart AND MetricValueDateTime <= @ThisWeekEnd )
OR ( O.[CampusId] IS NOT NULL )
-----------------------------------------------------------------------------------------------------------------------------------------------------------------
-----------------------------------------------------------------------------------------------------------------------------------------------------------------
------
------ BUILD DATA TABLES
------
-----------------------------------------------------------------------------------------------------------------------------------------------------------------
-----------------------------------------------------------------------------------------------------------------------------------------------------------------

----------------------------------------------------------------------------
--  TABLE 1: GRAB THE REFERENCE DATA
----------------------------------------------------------------------------

-- This returns a reference table with the parameters and years so that the lava template has access to them.
Select	@IsHoliday as 'IsHoliday',
		@IsCampus as 'IsCampus', 
		@IsServicesOngoing as 'IsServicesOngoing',
		( Select Top 1 Name From Campus Where Id = @CampusId) AS 'CampusName',	
		@Holiday as 'Holiday',	
		DATEPART(YEAR, @ThisYearStart ) AS 'ThisYear',
		DATEPART(YEAR, @LastYearStart ) AS 'LastYear',
		DATEPART(YEAR, @TwoYearStart ) AS 'TwoYear'

----------------------------------------------------------------------------
--  TABLE 2: GRAB THE TOTAL WEEKEND ATTENDANCE
----------------------------------------------------------------------------
UPDATE @MetricValues
SET GroupingData = MetricCategoryName

SELECT dataTable.GroupingRow, FirstColumnAttendance, SecondColumnAttendance, ThirdColumnAttendance, Growth
		,FirstColumnNotes, SecondColumnNotes, ThirdColumnNotes
FROM [dbo].[_com_centralaz_unfMetrics_GetAttendanceData](
	@FirstColumnStart,
	@FirstColumnEnd,
	@SecondColumnStart,
	@SecondColumnEnd,
	@ThirdColumnStart,
	@ThirdColumnEnd,
	@IsHoliday,
	@IsServicesOngoing,
	@CurrentMetrics,
	@MetricValues,
	@MetricValues) dataTable
LEFT JOIN [dbo].[_com_centralaz_unfMetrics_GetAttendanceNotes](
	@FirstColumnStart,
	@FirstColumnEnd,
	@SecondColumnStart,
	@SecondColumnEnd,
	@ThirdColumnStart,
	@ThirdColumnEnd,
	@IsHoliday,
	@IsServicesOngoing,
	@CurrentMetrics,
	@MetricValues,
	@MetricValues) notesTable
ON dataTable.GroupingRow = notesTable.GroupingRow
ORDER BY CategoryOrder

----------------------------------------------------------------------------
-- TABLE 3: GRAB THE SERVICE BREAKDOWN (Weekend Services)
----------------------------------------------------------------------------
UPDATE @MetricValues
SET GroupingData = ScheduleName

SELECT GroupingRow, FirstColumnAttendance, SecondColumnAttendance, ThirdColumnAttendance, Growth
	,CASE WHEN iCalendarContent LIKE '%RRULE%' THEN SUBSTRING(iCalendarContent, CHARINDEX('DTSTART:', iCalendarContent) + 16, (CHARINDEX('RRULE:', iCalendarContent)-(CHARINDEX('DTSTART:', iCalendarContent)+16))) ELSE '' END AS 'Time'
	,CASE WHEN iCalendarContent LIKE '%RRULE%' THEN [dbo].[_com_centralaz_unfMetrics_GetServiceDayOrder](SUBSTRING(iCalendarContent, CHARINDEX('BYDAY=', iCalendarContent) + 6, (CHARINDEX('SEQUENCE:', iCalendarContent)-(CHARINDEX('BYDAY=', iCalendarContent)+6)))) ELSE 8 END AS 'Date'
FROM [dbo].[_com_centralaz_unfMetrics_GetAttendanceData](
@FirstColumnStart,
@FirstColumnEnd,
@SecondColumnStart,
@SecondColumnEnd,
@ThirdColumnStart,
@ThirdColumnEnd,
@IsHoliday,
@IsServicesOngoing,
@CurrentMetrics,
@MetricValues,
@MetricValues)
ORDER BY [Date], [Time], iCalendarContent

----------------------------------------------------------------------------
-- TABLE 4: GRAB THE CAMPUS ROLLUP
----------------------------------------------------------------------------
UPDATE @MetricValues
SET GroupingData = CampusName

SELECT GroupingRow, FirstColumnAttendance, SecondColumnAttendance, ThirdColumnAttendance, Growth
FROM [dbo].[_com_centralaz_unfMetrics_GetAttendanceData](
@FirstColumnStart,
@FirstColumnEnd,
@SecondColumnStart,
@SecondColumnEnd,
@ThirdColumnStart,
@ThirdColumnEnd,
@IsHoliday,
@IsServicesOngoing,
@CurrentMetrics,
@MetricValues,
@MetricValues)
ORDER BY RowOrder, GroupingRow

----------------------------------------------------------------------------
-- TABLE 5: GRAB THE WORSHIP CENTER
----------------------------------------------------------------------------
IF @IsCampus = 0
BEGIN
	UPDATE @MetricValues
	SET GroupingData = CampusName

	UPDATE @WorshipCenterMetricValues
	SET GroupingData = CampusName

	SELECT GroupingRow, FirstColumnAttendance, SecondColumnAttendance, ThirdColumnAttendance, Growth
	FROM [dbo].[_com_centralaz_unfMetrics_GetAttendanceData](
	@FirstColumnStart,
	@FirstColumnEnd,
	@SecondColumnStart,
	@SecondColumnEnd,
	@ThirdColumnStart,
	@ThirdColumnEnd,
	@IsHoliday,
	@IsServicesOngoing,
	@CurrentMetrics,
	@MetricValues,
	@WorshipCenterMetricValues)
	ORDER BY RowOrder, GroupingRow
END

IF @IsCampus = 1
BEGIN
	UPDATE @MetricValues
	SET GroupingData = ScheduleName

	UPDATE @WorshipCenterMetricValues
	SET GroupingData = ScheduleName

	SELECT GroupingRow, FirstColumnAttendance, SecondColumnAttendance, ThirdColumnAttendance, Growth
		,CASE WHEN iCalendarContent LIKE '%RRULE%' THEN SUBSTRING(iCalendarContent, CHARINDEX('DTSTART:', iCalendarContent) + 16, (CHARINDEX('RRULE:', iCalendarContent)-(CHARINDEX('DTSTART:', iCalendarContent)+16))) ELSE '' END AS 'Time'
	,CASE WHEN iCalendarContent LIKE '%RRULE%' THEN [dbo].[_com_centralaz_unfMetrics_GetServiceDayOrder](SUBSTRING(iCalendarContent, CHARINDEX('BYDAY=', iCalendarContent) + 6, (CHARINDEX('SEQUENCE:', iCalendarContent)-(CHARINDEX('BYDAY=', iCalendarContent)+6)))) ELSE 8 END AS 'Date'
FROM [dbo].[_com_centralaz_unfMetrics_GetAttendanceData](
	@FirstColumnStart,
	@FirstColumnEnd,
	@SecondColumnStart,
	@SecondColumnEnd,
	@ThirdColumnStart,
	@ThirdColumnEnd,
	@IsHoliday,
	@IsServicesOngoing,
	@CurrentMetrics,
	@MetricValues,
	@WorshipCenterMetricValues)
	ORDER BY [Date], [Time], iCalendarContent
END

----------------------------------------------------------------------------
-- TABLE 6:  GRAB THE CHILDREN'S
----------------------------------------------------------------------------
IF @IsCampus = 0
BEGIN
	UPDATE @MetricValues
	SET GroupingData = CampusName

	UPDATE @ChildrensMetricValues
	SET GroupingData = CampusName

	SELECT GroupingRow, FirstColumnAttendance, SecondColumnAttendance, ThirdColumnAttendance, Growth
	FROM [dbo].[_com_centralaz_unfMetrics_GetAttendanceData](
	@FirstColumnStart,
	@FirstColumnEnd,
	@SecondColumnStart,
	@SecondColumnEnd,
	@ThirdColumnStart,
	@ThirdColumnEnd,
	@IsHoliday,
	@IsServicesOngoing,
	@CurrentMetrics,
	@MetricValues,
	@ChildrensMetricValues)
	ORDER BY RowOrder, GroupingRow
END

IF @IsCampus = 1
BEGIN
	UPDATE @MetricValues
	SET GroupingData = ScheduleName

	UPDATE @ChildrensMetricValues
	SET GroupingData = ScheduleName

	SELECT GroupingRow, FirstColumnAttendance, SecondColumnAttendance, ThirdColumnAttendance, Growth
		,CASE WHEN iCalendarContent LIKE '%RRULE%' THEN SUBSTRING(iCalendarContent, CHARINDEX('DTSTART:', iCalendarContent) + 16, (CHARINDEX('RRULE:', iCalendarContent)-(CHARINDEX('DTSTART:', iCalendarContent)+16))) ELSE '' END AS 'Time'
	,CASE WHEN iCalendarContent LIKE '%RRULE%' THEN [dbo].[_com_centralaz_unfMetrics_GetServiceDayOrder](SUBSTRING(iCalendarContent, CHARINDEX('BYDAY=', iCalendarContent) + 6, (CHARINDEX('SEQUENCE:', iCalendarContent)-(CHARINDEX('BYDAY=', iCalendarContent)+6)))) ELSE 8 END AS 'Date'
FROM [dbo].[_com_centralaz_unfMetrics_GetAttendanceData](
	@FirstColumnStart,
	@FirstColumnEnd,
	@SecondColumnStart,
	@SecondColumnEnd,
	@ThirdColumnStart,
	@ThirdColumnEnd,
	@IsHoliday,
	@IsServicesOngoing,
	@CurrentMetrics,
	@MetricValues,
	@ChildrensMetricValues) 
	ORDER BY [Date], [Time], iCalendarContent
END

----------------------------------------------------------------------------
-- TABLE 7: GRAB THE STUDENTS
----------------------------------------------------------------------------
IF @IsCampus = 0
BEGIN
	UPDATE @MetricValues
	SET GroupingData = CampusName

	UPDATE @StudentsMetricValues
	SET GroupingData = CampusName

	SELECT GroupingRow, FirstColumnAttendance, SecondColumnAttendance, ThirdColumnAttendance, Growth
	FROM [dbo].[_com_centralaz_unfMetrics_GetAttendanceData](
	@FirstColumnStart,
	@FirstColumnEnd,
	@SecondColumnStart,
	@SecondColumnEnd,
	@ThirdColumnStart,
	@ThirdColumnEnd,
	@IsHoliday,
	@IsServicesOngoing,
	@CurrentMetrics,
	@MetricValues,
	@StudentsMetricValues) 
	ORDER BY RowOrder, GroupingRow
END

IF @IsCampus = 1
BEGIN
	UPDATE @MetricValues
	SET GroupingData = ScheduleName

	UPDATE @StudentsMetricValues
	SET GroupingData = ScheduleName

	SELECT GroupingRow, FirstColumnAttendance, SecondColumnAttendance, ThirdColumnAttendance, Growth
		,CASE WHEN iCalendarContent LIKE '%RRULE%' THEN SUBSTRING(iCalendarContent, CHARINDEX('DTSTART:', iCalendarContent) + 16, (CHARINDEX('RRULE:', iCalendarContent)-(CHARINDEX('DTSTART:', iCalendarContent)+16))) ELSE '' END AS 'Time'
	,CASE WHEN iCalendarContent LIKE '%RRULE%' THEN [dbo].[_com_centralaz_unfMetrics_GetServiceDayOrder](SUBSTRING(iCalendarContent, CHARINDEX('BYDAY=', iCalendarContent) + 6, (CHARINDEX('SEQUENCE:', iCalendarContent)-(CHARINDEX('BYDAY=', iCalendarContent)+6)))) ELSE 8 END AS 'Date'
FROM [dbo].[_com_centralaz_unfMetrics_GetAttendanceData](
	@FirstColumnStart,
	@FirstColumnEnd,
	@SecondColumnStart,
	@SecondColumnEnd,
	@ThirdColumnStart,
	@ThirdColumnEnd,
	@IsHoliday,
	@IsServicesOngoing,
	@CurrentMetrics,
	@MetricValues,
	@StudentsMetricValues) 
	ORDER BY [Date], [Time], iCalendarContent
END

----------------------------------------------------------------------------
-- TABLE 8: GRAB THE BAPTISMS (All Church)
----------------------------------------------------------------------------
IF @IsCampus = 0
BEGIN
	UPDATE @MetricValues
	SET GroupingData = CampusName

	UPDATE @BaptismMetricValues
	SET GroupingData = CampusName

	SELECT GroupingRow, FirstColumnAttendance, SecondColumnAttendance, ThirdColumnAttendance, YtdGrowth AS 'Growth'
	FROM [dbo].[_com_centralaz_unfMetrics_GetAttendanceData](
	@ThisWeekStart,
	@ThisWeekEnd,
	@ThisMinistryYearStart,
	@ThisWeekEnd,
	@LastMinistryYearStart,
	@LastYearWeekEnd,
	@IsHoliday,
	@IsServicesOngoing,
	@CurrentMetrics,
	@MetricValues,
	@BaptismMetricValues) 
	ORDER BY RowOrder, GroupingRow
END

IF @IsCampus = 1
BEGIN
	UPDATE @MetricValues
	SET GroupingData = ScheduleName

	UPDATE @BaptismMetricValues
	SET GroupingData = ScheduleName

	SELECT GroupingRow, FirstColumnAttendance, SecondColumnAttendance, ThirdColumnAttendance, Growth
		,CASE WHEN iCalendarContent LIKE '%RRULE%' THEN SUBSTRING(iCalendarContent, CHARINDEX('DTSTART:', iCalendarContent) + 16, (CHARINDEX('RRULE:', iCalendarContent)-(CHARINDEX('DTSTART:', iCalendarContent)+16))) ELSE '' END AS 'Time'
	,CASE WHEN iCalendarContent LIKE '%RRULE%' THEN [dbo].[_com_centralaz_unfMetrics_GetServiceDayOrder](SUBSTRING(iCalendarContent, CHARINDEX('BYDAY=', iCalendarContent) + 6, (CHARINDEX('SEQUENCE:', iCalendarContent)-(CHARINDEX('BYDAY=', iCalendarContent)+6)))) ELSE 8 END AS 'Date'
FROM [dbo].[_com_centralaz_unfMetrics_GetAttendanceData](
	@ThisWeekStart,
	@ThisWeekEnd,
	@LastWeekStart,
	@LastWeekEnd,
	@LastYearWeekStart,
	@LastYearWeekEnd,
	@IsHoliday,
	@IsServicesOngoing,
	@CurrentMetrics,
	@MetricValues,
	@BaptismMetricValues) 
	ORDER BY [Date], [Time], iCalendarContent
END

----------------------------------------------------------------------------
-- TABLE 9: GRAB THE DISCOVER CENTRAL
----------------------------------------------------------------------------

UPDATE @MetricValues
SET GroupingData = ScheduleName

UPDATE @DiscoverCentralMetricValues
SET GroupingData = ScheduleName

SELECT GroupingRow, FirstColumnAttendance, SecondColumnAttendance, ThirdColumnAttendance, Growth
	,CASE WHEN iCalendarContent LIKE '%RRULE%' THEN SUBSTRING(iCalendarContent, CHARINDEX('DTSTART:', iCalendarContent) + 16, (CHARINDEX('RRULE:', iCalendarContent)-(CHARINDEX('DTSTART:', iCalendarContent)+16))) ELSE '' END AS 'Time'
,CASE WHEN iCalendarContent LIKE '%RRULE%' THEN [dbo].[_com_centralaz_unfMetrics_GetServiceDayOrder](SUBSTRING(iCalendarContent, CHARINDEX('BYDAY=', iCalendarContent) + 6, (CHARINDEX('SEQUENCE:', iCalendarContent)-(CHARINDEX('BYDAY=', iCalendarContent)+6)))) ELSE 8 END AS 'Date'
FROM [dbo].[_com_centralaz_unfMetrics_GetAttendanceData](
@ThisMonthStart,
@ThisMonthEnd,
@LastMonthStart,
@LastMonthEnd,
@LastYearMonthStart,
@LastYearMonthEnd,
@IsHoliday,
@IsServicesOngoing,
@CurrentMetrics,
@MetricValues,
@DiscoverCentralMetricValues) 
ORDER BY [Date], [Time], iCalendarContent

----------------------------------------------------------------------------
-- TABLE 10: GRAB THE FIRST TIME GUESTS
----------------------------------------------------------------------------

UPDATE @MetricValues
SET GroupingData = ScheduleName

UPDATE @FirstTimeGuestMetricValues
SET GroupingData = ScheduleName

SELECT GroupingRow, FirstColumnAttendance, SecondColumnAttendance, ThirdColumnAttendance, Growth
	,CASE WHEN iCalendarContent LIKE '%RRULE%' THEN SUBSTRING(iCalendarContent, CHARINDEX('DTSTART:', iCalendarContent) + 16, (CHARINDEX('RRULE:', iCalendarContent)-(CHARINDEX('DTSTART:', iCalendarContent)+16))) ELSE '' END AS 'Time'
,CASE WHEN iCalendarContent LIKE '%RRULE%' THEN [dbo].[_com_centralaz_unfMetrics_GetServiceDayOrder](SUBSTRING(iCalendarContent, CHARINDEX('BYDAY=', iCalendarContent) + 6, (CHARINDEX('SEQUENCE:', iCalendarContent)-(CHARINDEX('BYDAY=', iCalendarContent)+6)))) ELSE 8 END AS 'Date'
FROM [dbo].[_com_centralaz_unfMetrics_GetAttendanceData](
@ThisWeekStart,
@ThisWeekEnd,
@LastWeekStart,
@LastWeekEnd,
@LastYearWeekStart,
@LastYearWeekEnd,
@IsHoliday,
@IsServicesOngoing,
@CurrentMetrics,
@MetricValues,
@FirstTimeGuestMetricValues) 
ORDER BY [Date], [Time], iCalendarContent

----------------------------------------------------------------------------
-- TABLE 11: GRAB THE AUDIT OF THE TOTAL WEEKEND ATTENDANCE
----------------------------------------------------------------------------

IF @IsCampus = 0
	SELECT MetricValueDateTime, mv.ModifiedDateTime, ModifiedBy, ScheduleName as 'Schedule', CampusName as 'Campus', Attendance, m.Title as 'Area', m.ScheduleId, MetricId from @MetricValuesTEMP mv 
	INNER JOIN Metric m on m.Id = mv.MetricId
	WHERE MetricValueDateTime = '1/1/1900'
ELSE
	SELECT MetricValueDateTime, mv.ModifiedDateTime, ModifiedBy, ScheduleName as 'Schedule', CampusName as 'Campus', Attendance, m.Title as 'Area', m.ScheduleId, MetricId from @MetricValuesTEMP mv 
	INNER JOIN Metric m on m.Id = mv.MetricId
	WHERE MetricValueDateTime >= @FirstColumnStart and MetricValueDateTime <= @FirstColumnEnd
	ORDER BY mv.CampusName, mv.ModifiedDateTime desc

END



