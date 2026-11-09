-- SQLCMD mode required. Supply -v DatabaseName=ERPCustomerCustomFields (or a dedicated test name).
USE [master];

GO
DECLARE @name AS sysname = N'$(DatabaseName)';

IF CONVERT (INT, SERVERPROPERTY('ProductMajorVersion')) <> 16
    THROW 50000, 'SQL Server 2022 (16.x) is required.', 1;

IF @name = N''
   OR @name COLLATE Latin1_General_100_BIN2 LIKE N'%[^A-Za-z0-9_]%'
    THROW 50000, 'Use a nonempty alphanumeric/underscore database name.', 1;

IF DB_ID(@name) IS NOT NULL
    THROW 50000, 'Database already exists. Use a fresh database name or explicit -SeedOnly.', 1;

IF DB_ID(@name) IS NULL
    BEGIN
        DECLARE @sql AS NVARCHAR (MAX) = N'CREATE DATABASE ' + QUOTENAME(@name);
        EXECUTE sys.sp_executesql @sql;
        SET @sql = N'ALTER DATABASE ' + QUOTENAME(@name) + N' SET COMPATIBILITY_LEVEL = 160';
        EXECUTE sys.sp_executesql @sql;
    END
