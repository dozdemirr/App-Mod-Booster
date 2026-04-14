-- Drop and recreate the managed identity user with correct SID
IF EXISTS (SELECT * FROM sys.database_principals WHERE name = 'mid-AppModAssist-14-16-40')
BEGIN
    DROP USER [mid-AppModAssist-14-16-40];
END

CREATE USER [mid-AppModAssist-14-16-40] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [mid-AppModAssist-14-16-40];
ALTER ROLE db_datawriter ADD MEMBER [mid-AppModAssist-14-16-40];
GRANT EXECUTE TO [mid-AppModAssist-14-16-40];
