-- ============================================================
-- Stored Procedures for Expense Management System
-- ============================================================

-- usp_GetAllExpenses: Get all expenses with user/category/status names
CREATE OR ALTER PROCEDURE dbo.usp_GetAllExpenses
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        e.ExpenseId,
        e.UserId,
        u.UserName,
        u.Email,
        e.CategoryId,
        ec.CategoryName,
        e.StatusId,
        es.StatusName,
        e.AmountMinor,
        CAST(e.AmountMinor / 100.0 AS DECIMAL(10,2)) AS AmountGBP,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        ru.UserName AS ReviewedByName,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    JOIN dbo.Users u ON e.UserId = u.UserId
    JOIN dbo.ExpenseCategories ec ON e.CategoryId = ec.CategoryId
    JOIN dbo.ExpenseStatus es ON e.StatusId = es.StatusId
    LEFT JOIN dbo.Users ru ON e.ReviewedBy = ru.UserId
    ORDER BY e.CreatedAt DESC;
END
GO

-- usp_GetExpensesByStatus: Filter expenses by status name
CREATE OR ALTER PROCEDURE dbo.usp_GetExpensesByStatus
    @StatusName NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        e.ExpenseId,
        e.UserId,
        u.UserName,
        u.Email,
        e.CategoryId,
        ec.CategoryName,
        e.StatusId,
        es.StatusName,
        e.AmountMinor,
        CAST(e.AmountMinor / 100.0 AS DECIMAL(10,2)) AS AmountGBP,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        ru.UserName AS ReviewedByName,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    JOIN dbo.Users u ON e.UserId = u.UserId
    JOIN dbo.ExpenseCategories ec ON e.CategoryId = ec.CategoryId
    JOIN dbo.ExpenseStatus es ON e.StatusId = es.StatusId
    LEFT JOIN dbo.Users ru ON e.ReviewedBy = ru.UserId
    WHERE es.StatusName = @StatusName
    ORDER BY e.CreatedAt DESC;
END
GO

-- usp_GetExpensesByUser: Filter expenses by user id
CREATE OR ALTER PROCEDURE dbo.usp_GetExpensesByUser
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        e.ExpenseId,
        e.UserId,
        u.UserName,
        u.Email,
        e.CategoryId,
        ec.CategoryName,
        e.StatusId,
        es.StatusName,
        e.AmountMinor,
        CAST(e.AmountMinor / 100.0 AS DECIMAL(10,2)) AS AmountGBP,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        ru.UserName AS ReviewedByName,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    JOIN dbo.Users u ON e.UserId = u.UserId
    JOIN dbo.ExpenseCategories ec ON e.CategoryId = ec.CategoryId
    JOIN dbo.ExpenseStatus es ON e.StatusId = es.StatusId
    LEFT JOIN dbo.Users ru ON e.ReviewedBy = ru.UserId
    WHERE e.UserId = @UserId
    ORDER BY e.CreatedAt DESC;
END
GO

-- usp_GetExpenseById: Get a single expense by ID
CREATE OR ALTER PROCEDURE dbo.usp_GetExpenseById
    @ExpenseId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        e.ExpenseId,
        e.UserId,
        u.UserName,
        u.Email,
        e.CategoryId,
        ec.CategoryName,
        e.StatusId,
        es.StatusName,
        e.AmountMinor,
        CAST(e.AmountMinor / 100.0 AS DECIMAL(10,2)) AS AmountGBP,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        ru.UserName AS ReviewedByName,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    JOIN dbo.Users u ON e.UserId = u.UserId
    JOIN dbo.ExpenseCategories ec ON e.CategoryId = ec.CategoryId
    JOIN dbo.ExpenseStatus es ON e.StatusId = es.StatusId
    LEFT JOIN dbo.Users ru ON e.ReviewedBy = ru.UserId
    WHERE e.ExpenseId = @ExpenseId;
END
GO

-- usp_CreateExpense: Insert a new expense
CREATE OR ALTER PROCEDURE dbo.usp_CreateExpense
    @UserId INT,
    @CategoryId INT,
    @AmountMinor INT,
    @Currency NVARCHAR(3) = 'GBP',
    @ExpenseDate DATE,
    @Description NVARCHAR(1000) = NULL,
    @ReceiptFile NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @DraftStatusId INT;
    SELECT @DraftStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Draft';

    INSERT INTO dbo.Expenses (UserId, CategoryId, StatusId, AmountMinor, Currency, ExpenseDate, Description, ReceiptFile, CreatedAt)
    VALUES (@UserId, @CategoryId, @DraftStatusId, @AmountMinor, @Currency, @ExpenseDate, @Description, @ReceiptFile, SYSUTCDATETIME());

    SELECT SCOPE_IDENTITY() AS NewExpenseId;
END
GO

-- usp_UpdateExpenseStatus: Approve or reject an expense
CREATE OR ALTER PROCEDURE dbo.usp_UpdateExpenseStatus
    @ExpenseId INT,
    @StatusName NVARCHAR(50),
    @ReviewedBy INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @StatusId INT;
    SELECT @StatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = @StatusName;

    UPDATE dbo.Expenses
    SET StatusId = @StatusId,
        ReviewedBy = @ReviewedBy,
        ReviewedAt = SYSUTCDATETIME()
    WHERE ExpenseId = @ExpenseId;

    SELECT @@ROWCOUNT AS RowsAffected;
END
GO

-- usp_GetAllUsers: List all active users
CREATE OR ALTER PROCEDURE dbo.usp_GetAllUsers
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        u.UserId,
        u.UserName,
        u.Email,
        u.RoleId,
        r.RoleName,
        u.ManagerId,
        m.UserName AS ManagerName,
        u.IsActive,
        u.CreatedAt
    FROM dbo.Users u
    JOIN dbo.Roles r ON u.RoleId = r.RoleId
    LEFT JOIN dbo.Users m ON u.ManagerId = m.UserId
    WHERE u.IsActive = 1
    ORDER BY u.UserName;
END
GO

-- usp_GetAllCategories: List all active expense categories
CREATE OR ALTER PROCEDURE dbo.usp_GetAllCategories
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CategoryId, CategoryName, IsActive
    FROM dbo.ExpenseCategories
    WHERE IsActive = 1
    ORDER BY CategoryName;
END
GO

-- usp_GetAllStatuses: List all expense statuses
CREATE OR ALTER PROCEDURE dbo.usp_GetAllStatuses
AS
BEGIN
    SET NOCOUNT ON;
    SELECT StatusId, StatusName
    FROM dbo.ExpenseStatus
    ORDER BY StatusId;
END
GO

-- usp_SubmitExpense: Change expense status from Draft to Submitted
CREATE OR ALTER PROCEDURE dbo.usp_SubmitExpense
    @ExpenseId INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @DraftStatusId INT;
    DECLARE @SubmittedStatusId INT;

    SELECT @DraftStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Draft';
    SELECT @SubmittedStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Submitted';

    UPDATE dbo.Expenses
    SET StatusId = @SubmittedStatusId,
        SubmittedAt = SYSUTCDATETIME()
    WHERE ExpenseId = @ExpenseId
      AND StatusId = @DraftStatusId;

    SELECT @@ROWCOUNT AS RowsAffected;
END
GO
