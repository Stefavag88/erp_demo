USE [$(DatabaseName)];
GO
-- Application error codes: 51000 validation, 51001 missing resource, 51002 conflict.
CREATE PROCEDURE dbo.CustomField_Create
 @FieldKey nvarchar(max),@Label nvarchar(max),@FieldType tinyint,@ActorUserId int,@OptionsJson nvarchar(max)
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @FieldKey IS NULL OR LEN(LTRIM(RTRIM(@FieldKey)))=0 OR DATALENGTH(@FieldKey)>200 OR @Label IS NULL OR LEN(LTRIM(RTRIM(@Label)))=0 OR DATALENGTH(@Label)>400 OR @FieldType IS NULL OR @FieldType NOT IN(1,2)
  THROW 51000,'Invalid field key, label or type.',1;
 IF @OptionsJson IS NULL OR ISJSON(@OptionsJson,ARRAY)<>1 THROW 51000,'Options must be an array.',1;
 IF @FieldType=1 AND EXISTS(SELECT 1 FROM OPENJSON(@OptionsJson)) THROW 51000,'Text fields cannot have options.',1;
 IF EXISTS(SELECT 1 FROM OPENJSON(@OptionsJson) WHERE type<>5) THROW 51000,'Each option must be an object.',1;
 IF EXISTS(SELECT 1 FROM OPENJSON(@OptionsJson) j WHERE JSON_VALUE(j.value,'$.label') IS NULL OR LEN(LTRIM(RTRIM(JSON_VALUE(j.value,'$.label'))))=0 OR DATALENGTH(JSON_VALUE(j.value,'$.label'))>400 OR TRY_CONVERT(int,JSON_VALUE(j.value,'$.sortOrder')) IS NULL)
  THROW 51000,'Invalid option label or sort order.',1;
 BEGIN TRY
  BEGIN TRANSACTION;
  IF NOT EXISTS(SELECT 1 FROM dbo.AppUser WHERE UserId=@ActorUserId) THROW 51001,'Actor not found.',1;
  IF EXISTS(SELECT 1 FROM dbo.CustomFieldDefinition WITH(UPDLOCK,HOLDLOCK) WHERE FieldKey=@FieldKey) THROW 51002,'Field key already exists.',1;
  INSERT dbo.CustomFieldDefinition(FieldKey,Label,FieldType,CreatedByUserId) VALUES(@FieldKey,@Label,@FieldType,@ActorUserId);
  DECLARE @FieldId int=CONVERT(int,SCOPE_IDENTITY());
  INSERT dbo.CustomFieldOption(FieldId,Label,SortOrder) SELECT @FieldId,JSON_VALUE(value,'$.label'),CONVERT(int,JSON_VALUE(value,'$.sortOrder')) FROM OPENJSON(@OptionsJson);
  COMMIT;
  SELECT FieldId,FieldKey,Label,FieldType,IsActive FROM dbo.CustomFieldDefinition WHERE FieldId=@FieldId;
  SELECT OptionId,FieldId,Label,SortOrder,IsActive FROM dbo.CustomFieldOption WHERE FieldId=@FieldId ORDER BY SortOrder,OptionId;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE PROCEDURE dbo.CustomField_List @IncludeInactive bit=0
AS
BEGIN
 SET NOCOUNT ON;
 SELECT FieldId,FieldKey,Label,FieldType,IsActive FROM dbo.CustomFieldDefinition WHERE @IncludeInactive=1 OR IsActive=1 ORDER BY FieldId;
 SELECT o.OptionId,o.FieldId,o.Label,o.SortOrder,o.IsActive FROM dbo.CustomFieldOption o JOIN dbo.CustomFieldDefinition f ON f.FieldId=o.FieldId WHERE @IncludeInactive=1 OR f.IsActive=1 ORDER BY o.SortOrder,o.OptionId;
END;
GO
CREATE PROCEDURE dbo.CustomField_AddOption @FieldId int,@Label nvarchar(max),@SortOrder int
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @Label IS NULL OR LEN(LTRIM(RTRIM(@Label)))=0 OR DATALENGTH(@Label)>400 OR @SortOrder IS NULL THROW 51000,'Invalid option label or sort order.',1;
 BEGIN TRY
  BEGIN TRANSACTION;
  DECLARE @Type tinyint,@Active bit;
  SELECT @Type=FieldType,@Active=IsActive FROM dbo.CustomFieldDefinition WITH(UPDLOCK,HOLDLOCK) WHERE FieldId=@FieldId;
  IF @Type IS NULL THROW 51001,'Field not found.',1;
  IF @Type<>2 OR @Active<>1 THROW 51000,'Only active multi-select fields accept options.',1;
  INSERT dbo.CustomFieldOption(FieldId,Label,SortOrder) VALUES(@FieldId,@Label,@SortOrder);
  DECLARE @Id int=CONVERT(int,SCOPE_IDENTITY());
  COMMIT;
  SELECT OptionId,FieldId,Label,SortOrder,IsActive FROM dbo.CustomFieldOption WHERE OptionId=@Id;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE PROCEDURE dbo.CustomField_SetActive @FieldId int,@IsActive bit
AS
BEGIN
 SET NOCOUNT ON;
 IF @IsActive IS NULL THROW 51000,'Active state is required.',1;
 UPDATE dbo.CustomFieldDefinition SET IsActive=@IsActive WHERE FieldId=@FieldId;
 IF @@ROWCOUNT=0 THROW 51001,'Field not found.',1;
END;
GO
CREATE PROCEDURE dbo.CustomField_SetOptionActive @FieldId int,@OptionId int,@IsActive bit
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @IsActive IS NULL THROW 51000,'Active state is required.',1;
 BEGIN TRY
  BEGIN TRANSACTION;
  DECLARE @Found int;
  SELECT @Found=FieldId FROM dbo.CustomFieldDefinition WITH(UPDLOCK,HOLDLOCK) WHERE FieldId=@FieldId;
  IF @Found IS NULL THROW 51001,'Field not found.',1;
  UPDATE dbo.CustomFieldOption SET IsActive=@IsActive WHERE FieldId=@FieldId AND OptionId=@OptionId;
  IF @@ROWCOUNT=0 THROW 51001,'Option not found.',1;
  COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
