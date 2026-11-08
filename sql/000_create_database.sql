-- SQLCMD mode required. Supply -v DatabaseName=ERPDiscounts (or a dedicated test name).
USE [master];
GO
DECLARE @name sysname = N'$(DatabaseName)';
IF @name = N'' OR @name COLLATE Latin1_General_100_BIN2 LIKE N'%[^A-Za-z0-9_]%' THROW 50000, 'Use a nonempty alphanumeric/underscore database name.', 1;
IF DB_ID(@name) IS NULL
BEGIN
    DECLARE @sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@name);
    EXEC sys.sp_executesql @sql;
END;
GO
