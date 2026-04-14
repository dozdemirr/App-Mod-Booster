CREATE OR ALTER PROCEDURE dbo.usp_get_expenses
    @UserId INT = NULL,
    @CategoryId INT = NULL,
    @StatusId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        e.ExpenseId,
        u.UserName,
        u.Email,
        c.CategoryName,
        s.StatusName,
        CAST(e.AmountMinor / 100.0 AS DECIMAL(10,2)) AS AmountGbp,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedAt
    FROM dbo.Expenses e
    INNER JOIN dbo.Users u ON u.UserId = e.UserId
    INNER JOIN dbo.ExpenseCategories c ON c.CategoryId = e.CategoryId
    INNER JOIN dbo.ExpenseStatus s ON s.StatusId = e.StatusId
    WHERE (@UserId IS NULL OR e.UserId = @UserId)
      AND (@CategoryId IS NULL OR e.CategoryId = @CategoryId)
      AND (@StatusId IS NULL OR e.StatusId = @StatusId)
    ORDER BY e.CreatedAt DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_get_lookup_users
AS
BEGIN
    SET NOCOUNT ON;
    SELECT UserId AS Id, UserName AS Name
    FROM dbo.Users
    WHERE IsActive = 1
    ORDER BY UserName;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_get_lookup_categories
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CategoryId AS Id, CategoryName AS Name
    FROM dbo.ExpenseCategories
    WHERE IsActive = 1
    ORDER BY CategoryName;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_get_lookup_statuses
AS
BEGIN
    SET NOCOUNT ON;
    SELECT StatusId AS Id, StatusName AS Name
    FROM dbo.ExpenseStatus
    ORDER BY StatusId;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_create_expense
    @UserId INT,
    @CategoryId INT,
    @AmountMinor INT,
    @ExpenseDate DATE,
    @Description NVARCHAR(1000) = NULL,
    @ReceiptFile NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @DraftStatusId INT = (SELECT TOP 1 StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Draft');

    INSERT INTO dbo.Expenses (UserId, CategoryId, StatusId, AmountMinor, Currency, ExpenseDate, Description, ReceiptFile, CreatedAt)
    VALUES (@UserId, @CategoryId, @DraftStatusId, @AmountMinor, 'GBP', @ExpenseDate, @Description, @ReceiptFile, SYSUTCDATETIME());

    SELECT SCOPE_IDENTITY() AS ExpenseId;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_submit_expense
    @ExpenseId INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @SubmittedStatusId INT = (SELECT TOP 1 StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Submitted');

    UPDATE dbo.Expenses
    SET StatusId = @SubmittedStatusId,
        SubmittedAt = SYSUTCDATETIME()
    WHERE ExpenseId = @ExpenseId;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_review_expense
    @ExpenseId INT,
    @ReviewedByUserId INT,
    @Approve BIT,
    @Notes NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ApprovedStatusId INT = (SELECT TOP 1 StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Approved');
    DECLARE @RejectedStatusId INT = (SELECT TOP 1 StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Rejected');

    UPDATE dbo.Expenses
    SET StatusId = CASE WHEN @Approve = 1 THEN @ApprovedStatusId ELSE @RejectedStatusId END,
        ReviewedBy = @ReviewedByUserId,
        ReviewedAt = SYSUTCDATETIME(),
        Description = CONCAT(ISNULL(Description, ''), CASE WHEN @Notes IS NULL THEN '' ELSE CONCAT(' | Review Notes: ', @Notes) END)
    WHERE ExpenseId = @ExpenseId;
END
GO
