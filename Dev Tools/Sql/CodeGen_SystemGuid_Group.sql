/* Helps code generate Rock\SystemGuid\Group.cs */
SELECT CONCAT('
/// <summary>
/// ', [Name] , ' Group Guid
/// ', [Description], '
/// </summary>
public const string ', 
REPLACE(
    REPLACE(UPPER([ConstName]), ' ', '_'), 
    '-', '_'), ' = "', [Guid], '";')
  FROM 
  ( select Name, [Description], 'GROUP_' + REPLACE(REPLACE(REPLACE(REPLACE(Name, 'WEB - ', ''), 'RSR - ', ''), 'APP - ', ''), ' & ', ' and ') [ConstName], 
  [Guid] from [Group] where IsSecurityRole = 1
  ) g

ORDER BY g.ConstName