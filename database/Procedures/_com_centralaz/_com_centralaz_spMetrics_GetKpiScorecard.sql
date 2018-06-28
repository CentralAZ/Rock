USE [RockDB_Test]
GO

/****** Object:  StoredProcedure [dbo].[_com_centralaz_spMetrics_GetKpiScorecard]    Script Date: 6/28/2018 11:57:26 AM ******/
DROP PROCEDURE [dbo].[_com_centralaz_spMetrics_GetKpiScorecard]
GO

/****** Object:  StoredProcedure [dbo].[_com_centralaz_spMetrics_GetKpiScorecard]    Script Date: 6/28/2018 11:57:26 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


-- =============================================
-- Author:		Taylor Cavaletto
-- Create date: 6/28/2018
-- Description:	A stored procedure used to generate data for the KPI Scorecard pages
-- =============================================
CREATE PROCEDURE [dbo].[_com_centralaz_spMetrics_GetKpiScorecard]
		 @IsCampus BIT = 0,
		 @CampusId INT = 1,
		 @SundayDate NVARCHAR(50) = NULL,
		 @IsAssociate BIT = 0
AS
BEGIN

DECLARE @SundayDateTime DATETIME = NULL

BEGIN TRY
	SET @SundayDateTime = CONVERT(DATE, @SundayDate);
END TRY

BEGIN CATCH

END CATCH 

----------------------------------------------------------------------------
-- GET THE CONSTANTS
----------------------------------------------------------------------------
DECLARE @RootCategoryId INT = 541;

DECLARE @WeightedValueAttributeId INT = 16976;
DECLARE @AssociateWeightedValueAttributeId INT = 16977;
DECLARE @IsPercentageAttributeId INT = 16983;

DECLARE @ConnectionCardParentGroupId int = 258830;
DECLARE @FamilyGroupTypeId INT = 10;
DECLARE @LifeGroupTypeId INT = 42;
DECLARE @ServingTeamGroupTypeId INT = 23;

DECLARE @DiscoverCentralClassA_AttributeId INT = 16374;
DECLARE @DiscoverCentralClassB_AttributeId INT = 16375;
DECLARE @DiscoverCentralClassC_AttributeId INT = 16376;
DECLARE @BaptismDate_AttributeId int = 174;

DECLARE @cGROUPTYPEROLE_FAMILY_MEMBER_ADULT UNIQUEIDENTIFIER = '2639F9A5-2AAE-4E48-A8C3-4FFE86681E42';

----------------------------------------------------------------------------
-- GET THE DATE RANGES
----------------------------------------------------------------------------

-- First get the latest date or the SundayDate parameter
DECLARE @SelectedDate DATETIME ;
IF ( @SundayDateTime IS NULL) 
	SET @SelectedDate  = GETDATE()
ELSE SET @SelectedDate  = @SundayDateTime;

-- Next we'll grab the month ranges ( This month, Last Month, 'This Month Last Year')
DECLARE @ThisMonthStart DATETIME= DATEADD(mm, DATEDIFF(mm, 0, @SelectedDate ), 0)
DECLARE @ThisMonthEnd DATETIME = DATEADD(DAY, -1,DATEADD(mm, 1, @ThisMonthStart));


-- Finally, grab the ministry year start for this year and last year
DECLARE @ThisMinistryYearStart DATETIME = DATEADD(mm, 7, DATEADD(yy,DATEDIFF(yy,0,@SelectedDate),0));
IF(@SelectedDate < @ThisMinistryYearStart)
	SET @ThisMinistryYearStart = DATEADD(yy,-1, @ThisMinistryYearStart);

DECLARE @ThisMinistryYearEnd DATETIME = DATEADD(dd,-1, DATEADD(yy,1,@ThisMinistryYearStart));

----------------------------------------------------------------------------
-- GET THE GENERIC METRICVALUE JOINED TABLE
----------------------------------------------------------------------------

DECLARE @MetricValues TABLE(
	[Id] [int] NULL,
	[MetricId] [int] NULL,
	[MetricCategoryId] [int] null,
	[CategoryId][int] null,
	[MetricName] [nvarchar](50) NULL,
	[YValue] [FLOAT] NULL,
	[MetricValueType] [int] NOT NULL,
	[MetricValueDateTime] [datetime] NULL,
	[CampusId] [int] NULL,
	[CampusName] [nvarchar](50) NULL
);

-- Here we dump all the relevant metrics and their accessory information into a custom table that 
-- we'll work off of from here on out
INSERT INTO @MetricValues
SELECT mv.Id
	,m.Id
	,mc.Id
	,cat.Id
	,m.Title
	,mv.YValue
	,mv.MetricValueType
	,mv.MetricValueDateTime
	,c.Id
	,c.Name
FROM MetricValue mv
JOIN Metric m ON mv.MetricId = m.Id
JOIN MetricValuePartition mvpC ON mvpC.MetricValueId = mv.Id
JOIN MetricPartition mpC ON mvpC.MetricPartitionId = mpC.Id AND mpC.EntityTypeId IN (SELECT TOP 1 Id FROM EntityType WHERE Name='Rock.Model.Campus')
JOIN Campus c ON mvpC.EntityId = c.Id
JOIN MetricCategory mc ON mc.MetricId = m.Id
JOIN Category cat ON cat.Id = mc.CategoryId
WHERE  mc.CategoryId = @RootCategoryId
AND mv.YValue IS NOT NULL
AND ( @IsCampus = 0 OR c.id = @CampusId)
AND MetricValueDateTime >= @ThisMinistryYearStart

----------------------------------------------------------------------------
-- GET THE ATTENDER MEASURES
----------------------------------------------------------------------------

DECLARE @Attender TABLE(
	[PersonId] [int] NULL,
	[FamilyCampusId] [int] NULL,
	[ConnectionCardDate] [datetime] NULL,
	[DiscoverMoreDate] [datetime] NULL,
	[BaptismDate] [datetime] NULL,
	[LifeGroupDate] [datetime] NULL,
	[ServingDate] [datetime] NULL
);

INSERT INTO @Attender
SELECT personTable.Id,
	personTable.CampusId,
	connectionCardTable.ConnectionCardDate,
	discoverMoreTable.DiscoverMoreDate,
	baptismTable.BaptismDate,
	lifeGroupTable.LifeGroupDate,
	servingTeamTable.ServingDate
FROM 
	( SELECT  p.Id,
		f.CampusId
		FROM Person p
		INNER JOIN [GroupMember] gmF ON gmF.PersonId = p.Id
		INNER JOIN [GROUP] f ON f.Id = gmF.GroupId AND f.GroupTypeId = @FamilyGroupTypeId
		INNER JOIN [GroupTypeRole] gr ON gmF.GroupRoleId = gr.Id AND gr.[Guid]= @cGROUPTYPEROLE_FAMILY_MEMBER_ADULT
	) AS personTable
LEFT JOIN (
	SELECT gm.PersonId AS 'PersonId',
	gm.DateTimeAdded AS 'ConnectionCardDate'
	FROM GroupMember gm
	JOIN [GROUP] g ON gm.GroupId = g.Id
	WHERE g.ParentGroupId = @ConnectionCardParentGroupId
	AND gm.DateTimeAdded >= @ThisMinistryYearStart
	AND gm.DateTimeAdded <= @ThisMinistryYearEnd
	) AS connectionCardTable ON connectionCardTable.PersonId = personTable.Id
LEFT JOIN
	(
		SELECT av.EntityId, ValueAsDateTime  AS 'BaptismDate'
		FROM [AttributeValue] av WHERE av.AttributeId = @BaptismDate_AttributeId
	) baptismTable ON baptismTable.EntityId = personTable.Id
LEFT JOIN
	(
		SELECT at1.EntityId, MIN( ValueAsDateTime ) AS 'DiscoverMoreDate'
		FROM [AttributeValue] at1 WHERE at1.AttributeId IN (@DiscoverCentralClassA_AttributeId, @DiscoverCentralClassB_AttributeId, @DiscoverCentralClassC_AttributeId)
		GROUP BY at1.EntityId
	) discoverMoreTable ON discoverMoreTable.EntityId = personTable.Id
LEFT JOIN
	(
		SELECT p.Id, MIN( DateTimeAdded ) AS 'LifeGroupDate'
		FROM Person p
		INNER JOIN [GroupMember] gmL ON gmL.PersonId = p.Id
		INNER JOIN [GROUP] lg ON lg.Id = gmL.GroupId AND lg.GroupTypeId = @LifeGroupTypeId
		AND gmL.GroupMemberStatus = 1 AND lg.IsActive = 1
		GROUP BY (p.Id)
	) lifeGroupTable ON lifeGroupTable.Id = personTable.Id
LEFT JOIN
	(
		SELECT p.Id, MIN( DateTimeAdded ) AS 'ServingDate'
		FROM Person p
		INNER JOIN [GroupMember] gmL ON gmL.PersonId = p.Id
		INNER JOIN [GROUP] lg ON lg.Id = gmL.GroupId AND lg.GroupTypeId = @ServingTeamGroupTypeId
		AND gmL.GroupMemberStatus = 1 AND lg.IsActive = 1
		GROUP BY (p.Id)
	) servingTeamTable ON servingTeamTable.Id = personTable.Id
WHERE ( @IsCampus = 0 OR CampusId = @CampusId)
AND (
		(ConnectionCardDate >= @ThisMinistryYearStart AND ConnectionCardDate <= @ThisMinistryYearEnd) OR
		(BaptismDate >= @ThisMinistryYearStart AND BaptismDate <= @ThisMinistryYearEnd) OR
		(DiscoverMoreDate >= @ThisMinistryYearStart AND DiscoverMoreDate <= @ThisMinistryYearEnd)
	)

DECLARE @ConnectionCardMonth FLOAT = (SELECT COUNT(*)
		FROM @Attender AS a
		WHERE a.ConnectionCardDate >= @ThisMonthStart
		AND a.ConnectionCardDate <= @ThisMonthEnd)

DECLARE @ConnectionCardYear FLOAT = (SELECT COUNT(*)
		FROM @Attender AS a
		WHERE a.ConnectionCardDate >= @ThisMinistryYearStart
		AND a.ConnectionCardDate <= @ThisMinistryYearEnd)

DECLARE @DiscoverMoreMonth FLOAT = (SELECT COUNT(*)
		FROM @Attender AS a
		WHERE a.DiscoverMoreDate >= @ThisMonthStart
		AND a.DiscoverMoreDate <= @ThisMonthEnd)

DECLARE @DiscoverMoreYear FLOAT = (SELECT COUNT(*)
		FROM @Attender AS a
		WHERE a.DiscoverMoreDate >= @ThisMinistryYearStart
		AND a.DiscoverMoreDate <= @ThisMinistryYearEnd)

DECLARE @BaptismMonth FLOAT = (SELECT COUNT(*)
		FROM @Attender AS a
		WHERE a.BaptismDate >= @ThisMonthStart
		AND a.BaptismDate <= @ThisMonthEnd)

DECLARE @BaptismYear FLOAT = (SELECT COUNT(*)
		FROM @Attender AS a
		WHERE a.BaptismDate >= @ThisMinistryYearStart
		AND a.BaptismDate <= @ThisMinistryYearEnd)

DECLARE @ConnectionCardConversionMonth_subA FLOAT = (SELECT COUNT(*)
		FROM @Attender AS a
		WHERE a.ConnectionCardDate >= @ThisMonthStart
		AND a.ConnectionCardDate <= @ThisMonthEnd
		AND a.DiscoverMoreDate IS NOT NULL)

DECLARE @ConnectionCardConversionYear_subA FLOAT = (SELECT COUNT(*)
		FROM @Attender AS a
		WHERE a.ConnectionCardDate >= @ThisMinistryYearStart
		AND a.ConnectionCardDate <= @ThisMinistryYearEnd
		AND a.DiscoverMoreDate IS NOT NULL)

DECLARE @ServantMinisterMonth_subA FLOAT = (SELECT COUNT(*)
		FROM @Attender AS a
		WHERE a.DiscoverMoreDate >= @ThisMonthStart
		AND a.DiscoverMoreDate <= @ThisMonthEnd
		AND a.ServingDate IS NOT NULL)

DECLARE @ServantMinisterYear_subA FLOAT = (SELECT COUNT(*)
		FROM @Attender AS a
		WHERE a.DiscoverMoreDate >= @ThisMinistryYearStart
		AND a.DiscoverMoreDate <= @ThisMinistryYearEnd
		AND a.ServingDate IS NOT NULL)

DECLARE @LifeGroupMonth_subA FLOAT = (SELECT COUNT(*)
		FROM @Attender AS a
		WHERE a.DiscoverMoreDate >= @ThisMonthStart
		AND a.DiscoverMoreDate <= @ThisMonthEnd
		AND a.LifeGroupDate IS NOT NULL)

DECLARE @LifeGroupYear_subA FLOAT = (SELECT COUNT(*)
		FROM @Attender AS a
		WHERE a.DiscoverMoreDate >= @ThisMinistryYearStart
		AND a.DiscoverMoreDate <= @ThisMinistryYearEnd
		AND a.LifeGroupDate IS NOT NULL)

DECLARE @ConnectionCardConversionMonth FLOAT = (SELECT CASE 
		WHEN (@ConnectionCardMonth != 0 AND @ConnectionCardMonth IS NOT NULL) 
		THEN @ConnectionCardConversionMonth_subA / @ConnectionCardMonth
		ELSE 0 END)

DECLARE @ConnectionCardConversionYear FLOAT = (SELECT CASE 
		WHEN (@ConnectionCardYear != 0 AND @ConnectionCardYear IS NOT NULL) 
		THEN @ConnectionCardConversionYear_subA / @ConnectionCardYear
		ELSE 0 END)

DECLARE @ServantMinisterMonth FLOAT = (SELECT CASE 
		WHEN (@ConnectionCardMonth != 0 AND @ConnectionCardMonth IS NOT NULL) 
		THEN @ServantMinisterMonth_subA / @ConnectionCardMonth
		ELSE 0 END)

DECLARE @ServantMinisterYear FLOAT = (SELECT CASE 
		WHEN (@ConnectionCardYear != 0 AND @ConnectionCardYear IS NOT NULL) 
		THEN @ServantMinisterYear_subA / @ConnectionCardYear
		ELSE 0 END)

DECLARE @LifeGroupMonth FLOAT = (SELECT CASE 
		WHEN (@ConnectionCardMonth != 0 AND @ConnectionCardMonth IS NOT NULL) 
		THEN @LifeGroupMonth_subA / @ConnectionCardMonth
		ELSE 0 END)

DECLARE @LifeGroupYear FLOAT = (SELECT CASE 
		WHEN (@ConnectionCardYear != 0 AND @ConnectionCardYear IS NOT NULL) 
		THEN @LifeGroupYear_subA / @ConnectionCardYear
		ELSE 0 END)

DECLARE @DmCapacityMonth_subA FLOAT = (SELECT COUNT(*)
		FROM @MetricValues AS mv
		WHERE mv.MetricValueDateTime >= @ThisMonthStart
		AND mv.MetricValueDateTime <= @ThisMonthEnd
		AND mv.MetricValueType = 0
		AND MetricCategoryId = 165)

DECLARE @DmCapacityYear_subA FLOAT = (SELECT COUNT(*)
		FROM @MetricValues AS mv
		WHERE mv.MetricValueDateTime >= @ThisMinistryYearStart
		AND mv.MetricValueDateTime <= @ThisMinistryYearEnd
		AND mv.MetricValueType = 0
		AND MetricCategoryId = 165)

DECLARE @DmCapacityMonth FLOAT = (SELECT CASE 
		WHEN (@DmCapacityMonth_subA != 0 AND @DmCapacityMonth_subA IS NOT NULL) 
		THEN @DiscoverMoreMonth / @DmCapacityMonth_subA
		ELSE 0 END)

DECLARE @DmCapacityYear FLOAT = (SELECT CASE 
		WHEN (@DmCapacityYear_subA != 0 AND @DmCapacityYear_subA IS NOT NULL) 
		THEN @DiscoverMoreYear / @DmCapacityYear_subA
		ELSE 0 END)

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
SELECT	@IsCampus AS 'IsCampus', 
		( SELECT Top 1 Name FROM Campus WHERE Id = @CampusId) AS 'CampusName',
		@IsAssociate AS 'IsAssociate',
		@ThisMinistryYearStart AS 'ThisMinistryYearStart',
		@ThisMinistryYearEnd AS 'ThisMinistryYearEnd',
		@ThisMonthStart AS 'ThisMonthStart',
		@ThisMonthEnd AS 'ThisMonthEnd'

----------------------------------------------------------------------------
--  TABLE 2: GRAB THE DATA
----------------------------------------------------------------------------
DECLARE @ScoreCardTable TABLE(
	MetricCategoryId [int],
	MetricName [nvarchar](50),
	RowOrder [int], 
	WeightedValue [float],
	AssociateWeightedValue [float],
	IsPercent [nvarchar](50),

	MonthlyGoal [float],
	MonthlyMeasure [float],
	MonthlyPercentToGoal [float],
	MonthlyRating [float],
	AssociateMonthlyRating [float],

	YearlyGoal [float],
	YearlyMeasure [float],
	YearlyPercentToGoal [float],
	YearlyRating [float],
	AssociateYearlyRating [float]
);

INSERT INTO @ScoreCardTable(MetricCategoryId, MetricName, WeightedValue,AssociateWeightedValue, IsPercent, MonthlyGoal, YearlyGoal)
SELECT referenceTable.MetricCategoryId,
		referenceTable.MetricName,
		referenceTable.WeightedValue,
		referenceTable.AssociateWeightedValue,
		referenceTable.IsPercentage,
		monthlyGoal.Measure,
		fiscalYearGoal.Measure
FROM (
	SELECT DISTINCT
	mv.MetricCategoryId AS 'MetricCategoryId',
	mv.MetricName AS 'MetricName',
	CONVERT(FLOAT,wvAv.Value) AS 'WeightedValue',
	CONVERT(FLOAT,awvAv.Value) AS 'AssociateWeightedValue',
	ipAv.Value AS 'IsPercentage'
	FROM @MetricValues mv
	LEFT JOIN AttributeValue wvAv ON wvAv.EntityId = mv.MetricCategoryId AND wvAv.AttributeId = @WeightedValueAttributeId
	LEFT JOIN AttributeValue awvAv ON awvAv.EntityId = mv.MetricCategoryId AND awvAv.AttributeId = @AssociateWeightedValueAttributeId
	LEFT JOIN AttributeValue ipAv ON ipAv.EntityId = mv.MetricCategoryId AND ipAv.AttributeId = @IsPercentageAttributeId
	) referenceTable
LEFT JOIN 
	(
		SELECT MetricName
		,SUM(YValue) AS 'Measure'
		FROM @MetricValues
		WHERE MetricValueDateTime >= @ThisMonthStart
		AND MetricValueDateTime <= @ThisMonthEnd
		AND MetricValueType = 1
		GROUP BY (MetricName)
	) AS monthlyGoal
	ON referenceTable.MetricName = monthlyGoal.MetricName
LEFT JOIN 
	(
		SELECT MetricName
		,SUM(YValue) AS 'Measure'
		FROM @MetricValues
		WHERE MetricValueDateTime >= @ThisMinistryYearStart
		AND MetricValueDateTime <= @ThisMinistryYearEnd
		AND MetricValueType = 1
		GROUP BY (MetricName)
	) AS fiscalYearGoal
	On fiscalYearGoal.MetricName = referenceTable.MetricName

UPDATE @ScoreCardTable
SET YearlyGoal = YearlyGoal/12
WHERE IsPercent = 'True'

UPDATE @ScoreCardTable
SET MonthlyMeasure = @BaptismMonth,
YearlyMeasure = @BaptismYear,
MonthlyPercentToGoal = (SELECT CASE WHEN (MonthlyGoal != 0 AND MonthlyGoal IS NOT NULL) THEN @BaptismMonth/MonthlyGoal ELSE NULL END),
YearlyPercentToGoal = (SELECT CASE WHEN (YearlyGoal != 0 AND YearlyGoal IS NOT NULL) THEN @BaptismYear/YearlyGoal ELSE NULL END),
MonthlyRating = (SELECT	CASE WHEN (MonthlyGoal != 0 AND MonthlyGoal IS NOT NULL) THEN WeightedValue*(@BaptismMonth / MonthlyGoal) ELSE NULL END),
AssociateMonthlyRating = (SELECT CASE WHEN (MonthlyGoal != 0 AND MonthlyGoal IS NOT NULL) THEN AssociateWeightedValue*(@BaptismMonth / MonthlyGoal) ELSE NULL END),
YearlyRating = (SELECT CASE WHEN (YearlyGoal != 0 AND YearlyGoal IS NOT NULL) THEN WeightedValue*(@BaptismYear / YearlyGoal) ELSE NULL END),
AssociateYearlyRating = (SELECT	CASE WHEN (YearlyGoal != 0 AND YearlyGoal IS NOT NULL) THEN AssociateWeightedValue*(@BaptismYear / YearlyGoal) ELSE NULL END)
WHERE MetricCategoryId = 158

UPDATE @ScoreCardTable
SET MonthlyMeasure = @DiscoverMoreMonth,
YearlyMeasure = @DiscoverMoreYear,
MonthlyPercentToGoal = (SELECT CASE WHEN (MonthlyGoal != 0 AND MonthlyGoal IS NOT NULL) THEN @DiscoverMoreMonth/MonthlyGoal ELSE NULL END),
YearlyPercentToGoal = (SELECT CASE WHEN (YearlyGoal != 0 AND YearlyGoal IS NOT NULL) THEN @DiscoverMoreYear/YearlyGoal ELSE NULL END),
MonthlyRating = (SELECT	CASE WHEN (MonthlyGoal != 0 AND MonthlyGoal IS NOT NULL) THEN WeightedValue*(@DiscoverMoreMonth / MonthlyGoal) ELSE NULL END),
AssociateMonthlyRating = (SELECT CASE WHEN (MonthlyGoal != 0 AND MonthlyGoal IS NOT NULL) THEN AssociateWeightedValue*(@DiscoverMoreMonth / MonthlyGoal) ELSE NULL END),
YearlyRating = (SELECT CASE WHEN (YearlyGoal != 0 AND YearlyGoal IS NOT NULL) THEN WeightedValue*(@DiscoverMoreYear / YearlyGoal) ELSE NULL END),
AssociateYearlyRating = (SELECT	CASE WHEN (YearlyGoal != 0 AND YearlyGoal IS NOT NULL) THEN AssociateWeightedValue*(@DiscoverMoreYear / YearlyGoal) ELSE NULL END)
WHERE MetricCategoryId = 159

UPDATE @ScoreCardTable
SET MonthlyMeasure = @ConnectionCardMonth,
YearlyMeasure = @ConnectionCardYear,
MonthlyPercentToGoal = (SELECT CASE WHEN (MonthlyGoal != 0 AND MonthlyGoal IS NOT NULL) THEN @ConnectionCardMonth/MonthlyGoal ELSE NULL END),
YearlyPercentToGoal = (SELECT CASE WHEN (YearlyGoal != 0 AND YearlyGoal IS NOT NULL) THEN @ConnectionCardYear/YearlyGoal ELSE NULL END),
MonthlyRating = (SELECT	CASE WHEN (MonthlyGoal != 0 AND MonthlyGoal IS NOT NULL) THEN WeightedValue*(@ConnectionCardMonth / MonthlyGoal) ELSE NULL END),
AssociateMonthlyRating = (SELECT CASE WHEN (MonthlyGoal != 0 AND MonthlyGoal IS NOT NULL) THEN AssociateWeightedValue*(@ConnectionCardMonth / MonthlyGoal) ELSE NULL END),
YearlyRating = (SELECT CASE WHEN (YearlyGoal != 0 AND YearlyGoal IS NOT NULL) THEN WeightedValue*(@ConnectionCardYear / YearlyGoal) ELSE NULL END),
AssociateYearlyRating = (SELECT	CASE WHEN (YearlyGoal != 0 AND YearlyGoal IS NOT NULL) THEN AssociateWeightedValue*(@ConnectionCardYear / YearlyGoal) ELSE NULL END)
WHERE MetricCategoryId = 160

UPDATE @ScoreCardTable
SET MonthlyMeasure = @ServantMinisterMonth,
YearlyMeasure = @ServantMinisterYear,
MonthlyPercentToGoal = (SELECT CASE WHEN (MonthlyGoal != 0 AND MonthlyGoal IS NOT NULL) THEN @ServantMinisterMonth/MonthlyGoal ELSE NULL END),
YearlyPercentToGoal = (SELECT CASE WHEN (YearlyGoal != 0 AND YearlyGoal IS NOT NULL) THEN @ServantMinisterYear/YearlyGoal ELSE NULL END),
MonthlyRating = (SELECT	CASE WHEN (MonthlyGoal != 0 AND MonthlyGoal IS NOT NULL) THEN WeightedValue*(@ServantMinisterMonth / MonthlyGoal) ELSE NULL END),
AssociateMonthlyRating = (SELECT CASE WHEN (MonthlyGoal != 0 AND MonthlyGoal IS NOT NULL) THEN AssociateWeightedValue*(@ServantMinisterMonth / MonthlyGoal) ELSE NULL END),
YearlyRating = (SELECT CASE WHEN (YearlyGoal != 0 AND YearlyGoal IS NOT NULL) THEN WeightedValue*(@ServantMinisterYear / YearlyGoal) ELSE NULL END),
AssociateYearlyRating = (SELECT	CASE WHEN (YearlyGoal != 0 AND YearlyGoal IS NOT NULL) THEN AssociateWeightedValue*(@ServantMinisterYear / YearlyGoal) ELSE NULL END)
WHERE MetricCategoryId = 161

UPDATE @ScoreCardTable
SET MonthlyMeasure = @LifeGroupMonth,
YearlyMeasure = @LifeGroupYear,
MonthlyPercentToGoal = (SELECT CASE WHEN (MonthlyGoal != 0 AND MonthlyGoal IS NOT NULL) THEN @LifeGroupMonth/MonthlyGoal ELSE NULL END),
YearlyPercentToGoal = (SELECT CASE WHEN (YearlyGoal != 0 AND YearlyGoal IS NOT NULL) THEN @LifeGroupYear/YearlyGoal ELSE NULL END),
MonthlyRating = (SELECT	CASE WHEN (MonthlyGoal != 0 AND MonthlyGoal IS NOT NULL) THEN WeightedValue*(@LifeGroupMonth / MonthlyGoal) ELSE NULL END),
AssociateMonthlyRating = (SELECT CASE WHEN (MonthlyGoal != 0 AND MonthlyGoal IS NOT NULL) THEN AssociateWeightedValue*(@LifeGroupMonth / MonthlyGoal) ELSE NULL END),
YearlyRating = (SELECT CASE WHEN (YearlyGoal != 0 AND YearlyGoal IS NOT NULL) THEN WeightedValue*(@LifeGroupYear / YearlyGoal) ELSE NULL END),
AssociateYearlyRating = (SELECT	CASE WHEN (YearlyGoal != 0 AND YearlyGoal IS NOT NULL) THEN AssociateWeightedValue*(@LifeGroupYear / YearlyGoal) ELSE NULL END)
WHERE MetricCategoryId = 162

UPDATE @ScoreCardTable
SET MonthlyMeasure = @ConnectionCardConversionMonth,
YearlyMeasure = @ConnectionCardConversionYear,
MonthlyPercentToGoal = (SELECT CASE WHEN (MonthlyGoal != 0 AND MonthlyGoal IS NOT NULL) THEN @ConnectionCardConversionMonth/MonthlyGoal ELSE NULL END),
YearlyPercentToGoal = (SELECT CASE WHEN (YearlyGoal != 0 AND YearlyGoal IS NOT NULL) THEN @ConnectionCardConversionYear/YearlyGoal ELSE NULL END),
MonthlyRating = (SELECT	CASE WHEN (MonthlyGoal != 0 AND MonthlyGoal IS NOT NULL) THEN WeightedValue*(@ConnectionCardConversionMonth / MonthlyGoal) ELSE NULL END),
AssociateMonthlyRating = (SELECT CASE WHEN (MonthlyGoal != 0 AND MonthlyGoal IS NOT NULL) THEN AssociateWeightedValue*(@ConnectionCardConversionMonth / MonthlyGoal) ELSE NULL END),
YearlyRating = (SELECT CASE WHEN (YearlyGoal != 0 AND YearlyGoal IS NOT NULL) THEN WeightedValue*(@ConnectionCardConversionYear / YearlyGoal) ELSE NULL END),
AssociateYearlyRating = (SELECT	CASE WHEN (YearlyGoal != 0 AND YearlyGoal IS NOT NULL) THEN AssociateWeightedValue*(@ConnectionCardConversionYear / YearlyGoal) ELSE NULL END)
WHERE MetricCategoryId = 163

UPDATE @ScoreCardTable
SET MonthlyMeasure = @DmCapacityMonth,
YearlyMeasure = @DmCapacityYear,
MonthlyPercentToGoal = (SELECT CASE WHEN (MonthlyGoal != 0 AND MonthlyGoal IS NOT NULL) THEN @DmCapacityMonth/MonthlyGoal ELSE NULL END),
YearlyPercentToGoal = (SELECT CASE WHEN (YearlyGoal != 0 AND YearlyGoal IS NOT NULL) THEN @DmCapacityYear/YearlyGoal ELSE NULL END),
MonthlyRating = (SELECT	CASE WHEN (MonthlyGoal != 0 AND MonthlyGoal IS NOT NULL) THEN WeightedValue*(@DmCapacityMonth / MonthlyGoal) ELSE NULL END),
AssociateMonthlyRating = (SELECT CASE WHEN (MonthlyGoal != 0 AND MonthlyGoal IS NOT NULL) THEN AssociateWeightedValue*(@DmCapacityMonth / MonthlyGoal) ELSE NULL END),
YearlyRating = (SELECT CASE WHEN (YearlyGoal != 0 AND YearlyGoal IS NOT NULL) THEN WeightedValue*(@DmCapacityYear / YearlyGoal) ELSE NULL END),
AssociateYearlyRating = (SELECT	CASE WHEN (YearlyGoal != 0 AND YearlyGoal IS NOT NULL) THEN AssociateWeightedValue*(@DmCapacityYear / YearlyGoal) ELSE NULL END)
WHERE MetricCategoryId = 165

SELECT	 CASE GROUPING(MetricCategoryId) WHEN 1 THEN 'Total' ELSE MAX(MetricName) END AS 'Name',
		 CASE GROUPING(MetricCategoryId) WHEN 1 THEN 1 ELSE 0 END AS 'Order',
		 CASE @IsAssociate WHEN 0 THEN SUM(WeightedValue) ELSE SUM(AssociateWeightedValue) END AS 'WeightedValue',
		 SUM(MonthlyGoal) AS 'MonthlyGoal',
		 SUM(YearlyGoal) AS 'YearlyGoal',
		 SUM(MonthlyMeasure) AS 'MonthlyMeasure',
		 SUM(YearlyMeasure) AS 'YearlyMeasure',
		 SUM(MonthlyPercentToGoal) AS 'MonthlyPercentToGoal',
		 SUM(YearlyPercentToGoal) AS 'YearlyPercentToGoal',	 
		 CASE @IsAssociate WHEN 0 THEN SUM(MonthlyRating) ELSE SUM(AssociateMonthlyRating) END AS 'MonthlyRating',
		 CASE @IsAssociate WHEN 0 THEN SUM(YearlyRating) ELSE SUM(AssociateYearlyRating) END AS 'YearlyRating',
		 CASE When MAX(IsPercent) = 'True' Then 1 ELSE 0 END AS 'IsPercent'
FROM @ScoreCardTable
WHERE (@IsAssociate = 0 OR AssociateWeightedValue <> '')
GROUP BY ROLLUP(MetricCategoryId)
ORDER BY [Order], WeightedValue DESC
END
GO

