DECLARE @StartDate datetime = '2018-07-01'
DECLARE @Months int = 24

DECLARE @MetricTbl TABLE ( MetricGuid uniqueidentifier NOT NULL, MetricCode varchar(2) NOT NULL )
INSERT INTO @MetricTbl VALUES
	( 'BBF8148D-84A2-4FCD-8768-A154B951A986', 'DM' ),	--	Discover More Attendance
	( '2340CC55-FDF6-4F87-9013-E4918C3D83C7', 'CC' ),	--	Connection Cards (FTG)
	( '35CCF658-25AE-4DD5-88A4-83F3C3DDAAB2', 'CR' ),	--	Connection Card Conversion
	( '8E502D63-9485-4332-A412-94EAC686E91B', 'RC' ),	--	DM Room Capacity Utilization
	( '92BAE802-FA3C-41C2-A551-960A492B800E', 'BP' ),	--	Baptisms
	( '156C80A4-33CF-4E6D-920E-30FC56BE7801', 'SM' ),	--	New Servant Ministers
	( 'A22B3072-1A68-4034-A3E6-7B331894BC6E', 'LG' )	--	New Life Group Members

DECLARE @GoalsTbl TABLE ( MetricCode varchar(2) NOT NULL, CampusName varchar(50), Goal decimal(18,2), Value decimal(18,2), GrowthRate decimal(5,4) )
INSERT INTO @GoalsTbl VALUES

--	Discover More Attendance
	( 'DM', 'Gilbert',		214,	0,		1.0833 ),	
	( 'DM', 'Ahwatukee',	54,		0,		1.0833 ),	
	( 'DM', 'Glendale',		104,	0,		1.0833 ),	
	( 'DM', 'Mesa',			329,	0,		1.0833 ),	
	( 'DM', 'Queen Creek',	80,		0,		1.0833 ),	

--	Connection Cards				
	( 'CC', 'Gilbert',		881,	0,		1.0833 ),	
	( 'CC', 'Ahwatukee',	139,	0,		1.0833 ),	
	( 'CC', 'Glendale',		575,	0,		1.0833 ),	
	( 'CC', 'Mesa',			620,	0,		1.0833 ),	
	( 'CC', 'Queen Creek',	176,	0,		1.0833 ),	

--	Connection Card Conversion		
	( 'CR', 'Gilbert',		.25,	0,		1 ),	
	( 'CR', 'Ahwatukee',	.39,	0,		1 ),	
	( 'CR', 'Glendale',		.18,	0,		1 ),	
	( 'CR', 'Mesa',			.53,	0,		1 ),	
	( 'CR', 'Queen Creek',	.46,	0,		1 ),	

--	DM Room Capacity Utilization	
	( 'RC', 'Gilbert',		.40,	535,	1 ),	
	( 'RC', 'Ahwatukee',	.20,	270,	1 ),	
	( 'RC', 'Glendale',		.24,	430,	1 ),	
	( 'RC', 'Mesa',			.70,	470,	1 ),	
	( 'RC', 'Queen Creek',	.15,	535,    1 ),	

--	Baptisms				
	( 'BP', 'Gilbert',		225,	0,		1.0833 ),	
	( 'BP', 'Ahwatukee',	46,		0,		1.0833 ),	
	( 'BP', 'Glendale',		74,		0,		1.0833 ),	
	( 'BP', 'Mesa',			152,	0,		1.0833 ),	
	( 'BP', 'Queen Creek',	49,		0,		1.0833 ),	

--	New Servent Ministers	
	( 'SM', 'Gilbert',		.10,	0,		1 ),	
	( 'SM', 'Ahwatukee',	.10,	0,		1 ),	
	( 'SM', 'Glendale',		.10,	0,		1 ),	
	( 'SM', 'Mesa',			.10,	0,		1 ),	
	( 'SM', 'Queen Creek',	.10,	0,		1 ),	

--	New Life Group Members 
	( 'LG', 'Gilbert',		.10,	0,		1 ),	
	( 'LG', 'Ahwatukee',	.10,	0,		1 ),	
	( 'LG', 'Glendale',		.10,	0,		1 ),	
	( 'LG', 'Mesa',			.10,	0,		1 ),	
	( 'LG', 'Queen Creek',	.10,	0,		1 )

DECLARE @Month datetime = @StartDate
DECLARE @LastMonth datetime
DECLARE @EndDate datetime = DATEADD( m, @Months, @StartDate )
DECLARE @CampusEntityTypeId INT = ( SELECT TOP 1 Id FROM EntityType WHERE Name='Rock.Model.Campus' )

---- Delete existing values
DELETE P
FROM @MetricTbl T
INNER JOIN [Metric] M ON M.[Guid] = T.[MetricGuid]
INNER JOIN [MetricValue] V ON V.[MetricId] = M.[Id]
INNER JOIN [MetricValuePartition] P ON P.[MetricValueId] = V.[Id]

DELETE V
FROM @MetricTbl T
INNER JOIN [Metric] M ON M.[Guid] = T.[MetricGuid]
INNER JOIN [MetricValue] V ON V.[MetricId] = M.[Id]

DECLARE @CampusId int
DECLARE @MetricId int
DECLARE @PartitionId int
DECLARE @Goal decimal(18,2)
DECLARE @Value decimal(18,2)
DECLARE @GrowthRate decimal(5,4)

DECLARE @MetricValueId int
DECLARE @LastValue decimal(18,2)

WHILE @Month < @EndDate
BEGIN
	
	DECLARE MetricCursor CURSOR FOR
	SELECT C.[Id], M.[Id], P.[Id], GT.[Goal], GT.[Value], GT.[GrowthRate]
	FROM @GoalsTbl GT
	INNER JOIN @MetricTbl MT ON MT.[MetricCode] = GT.[MetricCode]
	INNER JOIN [Metric] M ON M.[Guid] = MT.[MetricGuid]
	INNER JOIN [MetricPartition] P ON P.[MetricId] = M.[Id] AND P.[EntityTypeId] = @CampusEntityTypeId
	INNER JOIN [Campus] C ON C.[Name] = GT.[CampusName]

	OPEN MetricCursor
	FETCH NEXT FROM MetricCursor
	INTO @CampusId, @MetricId, @PartitionId, @Goal, @Value, @GrowthRate

	WHILE (@@FETCH_STATUS <> -1)
	BEGIN

		IF (@@FETCH_STATUS = 0)
		BEGIN

			SET @LastValue = ( 
				SELECT TOP 1 MV.[YValue]
				FROM [MetricPartition] MP
				INNER JOIN [MetricValuePartition] VP ON VP.[MetricPartitionId] = MP.[Id] AND VP.[EntityId] = @CampusId
				INNER JOIN [MetricValue] MV ON MV.[Id] = VP.[MetricValueId]
				WHERE MP.[MetricId] = @MetricId 
				AND MP.[EntityTypeId] = @CampusEntityTypeId 
				AND MV.[MetricValueDateTime] = @LastMonth
			)

			-- Create Goal
			INSERT INTO [MetricValue] ( [MetricValueType], [XValue], [YValue], [MetricId], [Note], [MetricValueDateTime], [Guid] )
			VALUES ( 1, '', 
                CASE WHEN @GrowthRate = 1 
                    THEN @Goal
                    ELSE 
                    CASE WHEN @LastValue IS NULL 
                        THEN CAST ( @Goal AS INT )
                        ELSE CAST ( @LastValue * @GrowthRate AS INT )
                    END
                END, 
				@MetricId, '', @Month, NEWID() )
			SET @MetricValueId = SCOPE_IDENTITY()

			-- Add Partition
			INSERT INTO [MetricValuePartition] ( [MetricPartitionId], [MetricValueId], [EntityId], [Guid] )
			VALUES ( @PartitionId, @MetricValueId, @CampusId, NEWID() )
		
			IF @Value IS NOT NULL AND @Value > 0 
			BEGIN

				-- Insert Value
				INSERT INTO [MetricValue] ( [MetricValueType], [XValue], [YValue], [MetricId], [Note], [MetricValueDateTime], [Guid] )
				VALUES ( 0, '', @Value, @MetricId, '', @Month, NEWID() )
				SET @MetricValueId = SCOPE_IDENTITY()

				-- Add Partition
				INSERT INTO [MetricValuePartition] ( [MetricPartitionId], [MetricValueId], [EntityId], [Guid] )
				VALUES ( @PartitionId, @MetricValueId, @CampusId, NEWID() )

			END

			FETCH NEXT FROM MetricCursor
			INTO @CampusId, @MetricId, @PartitionId, @Goal, @Value, @GrowthRate

		END
	
	END

	CLOSE MetricCursor
	DEALLOCATE MetricCursor
		
	SET @LastMonth = @Month
	SET @Month = DATEADD( m, 1, @Month) 

END

