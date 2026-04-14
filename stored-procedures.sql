CREATE OR ALTER PROCEDURE dbo.sp_get_users
AS
BEGIN
    SET NOCOUNT ON;
    SELECT u.UserId, u.UserName, u.Email, r.RoleName
    FROM dbo.Users u
    INNER JOIN dbo.Roles r ON r.RoleId = u.RoleId
    WHERE u.IsActive = 1
    ORDER BY u.UserName;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_get_categories
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CategoryId, CategoryName
    FROM dbo.ExpenseCategories
    WHERE IsActive = 1
    ORDER BY CategoryName;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_get_statuses
AS
BEGIN
    SET NOCOUNT ON;
    SELECT StatusId, StatusName
    FROM dbo.ExpenseStatus
    ORDER BY StatusId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_get_expenses
    @StatusId INT = NULL,
    @UserId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        e.ExpenseId,
        e.UserId,
        u.UserName,
        e.CategoryId,
        c.CategoryName,
        e.StatusId,
        s.StatusName,
        e.AmountMinor,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.SubmittedAt,
        e.ReviewedAt
    FROM dbo.Expenses e
    INNER JOIN dbo.Users u ON u.UserId = e.UserId
    INNER JOIN dbo.ExpenseCategories c ON c.CategoryId = e.CategoryId
    INNER JOIN dbo.ExpenseStatus s ON s.StatusId = e.StatusId
    WHERE (@StatusId IS NULL OR e.StatusId = @StatusId)
      AND (@UserId IS NULL OR e.UserId = @UserId)
    ORDER BY e.CreatedAt DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_create_expense
    @UserId INT,
    @CategoryId INT,
    @AmountMinor INT,
    @ExpenseDate DATE,
    @Description NVARCHAR(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @DraftStatusId INT = (SELECT TOP(1) StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Draft');

    INSERT INTO dbo.Expenses (UserId, CategoryId, StatusId, AmountMinor, Currency, ExpenseDate, Description, CreatedAt)
    VALUES (@UserId, @CategoryId, @DraftStatusId, @AmountMinor, 'GBP', @ExpenseDate, @Description, SYSUTCDATETIME());

    SELECT CAST(SCOPE_IDENTITY() AS INT) AS ExpenseId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_review_expense
    @ExpenseId INT,
    @ManagerUserId INT,
    @Approve BIT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ApprovedStatusId INT = (SELECT TOP(1) StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Approved');
    DECLARE @RejectedStatusId INT = (SELECT TOP(1) StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Rejected');

    UPDATE dbo.Expenses
    SET StatusId = CASE WHEN @Approve = 1 THEN @ApprovedStatusId ELSE @RejectedStatusId END,
        ReviewedBy = @ManagerUserId,
        ReviewedAt = SYSUTCDATETIME(),
        SubmittedAt = COALESCE(SubmittedAt, SYSUTCDATETIME())
    WHERE ExpenseId = @ExpenseId;
END
GO
