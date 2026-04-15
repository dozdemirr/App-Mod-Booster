CREATE OR ALTER PROCEDURE dbo.usp_expenses_get
    @status NVARCHAR(50) = NULL,
    @userId INT = NULL,
    @categoryId INT = NULL
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
        e.CreatedAt
    FROM dbo.Expenses e
    INNER JOIN dbo.Users u ON u.UserId = e.UserId
    INNER JOIN dbo.ExpenseCategories c ON c.CategoryId = e.CategoryId
    INNER JOIN dbo.ExpenseStatus s ON s.StatusId = e.StatusId
    WHERE (@status IS NULL OR s.StatusName = @status)
      AND (@userId IS NULL OR e.UserId = @userId)
      AND (@categoryId IS NULL OR e.CategoryId = @categoryId)
    ORDER BY e.CreatedAt DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_expenses_create
    @userId INT,
    @categoryId INT,
    @amountMinor INT,
    @expenseDate DATE,
    @description NVARCHAR(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @statusId INT = (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Submitted');

    INSERT INTO dbo.Expenses
    (
        UserId,
        CategoryId,
        StatusId,
        AmountMinor,
        Currency,
        ExpenseDate,
        Description,
        SubmittedAt,
        CreatedAt
    )
    VALUES
    (
        @userId,
        @categoryId,
        ISNULL(@statusId, 2),
        @amountMinor,
        'GBP',
        @expenseDate,
        @description,
        SYSUTCDATETIME(),
        SYSUTCDATETIME()
    );

    SELECT CAST(SCOPE_IDENTITY() AS INT) AS ExpenseId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_expenses_update_status
    @expenseId INT,
    @newStatus NVARCHAR(50),
    @reviewedByUserId INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @statusId INT = (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = @newStatus);

    UPDATE dbo.Expenses
    SET
        StatusId = ISNULL(@statusId, StatusId),
        ReviewedBy = @reviewedByUserId,
        ReviewedAt = CASE WHEN @newStatus IN ('Approved', 'Rejected') THEN SYSUTCDATETIME() ELSE ReviewedAt END,
        SubmittedAt = CASE WHEN @newStatus = 'Submitted' THEN SYSUTCDATETIME() ELSE SubmittedAt END
    WHERE ExpenseId = @expenseId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_categories_get
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CategoryId, CategoryName FROM dbo.ExpenseCategories WHERE IsActive = 1 ORDER BY CategoryName;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_statuses_get
AS
BEGIN
    SET NOCOUNT ON;
    SELECT StatusId, StatusName FROM dbo.ExpenseStatus ORDER BY StatusId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_users_get
AS
BEGIN
    SET NOCOUNT ON;
    SELECT UserId, UserName FROM dbo.Users WHERE IsActive = 1 ORDER BY UserName;
END;
GO
