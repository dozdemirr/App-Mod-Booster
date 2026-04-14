-- =============================================
-- Stored Procedures for Expense Management System
-- All amounts stored in pence (minor units); returned as decimal GBP
-- =============================================

-- =============================================
-- usp_GetAllExpenses
-- List all expenses with joined user, category, and status
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_GetAllExpenses
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
        CAST(e.AmountMinor / 100.0 AS DECIMAL(10, 2)) AS Amount,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        rev.UserName AS ReviewedByName,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    JOIN dbo.Users u ON e.UserId = u.UserId
    JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    LEFT JOIN dbo.Users rev ON e.ReviewedBy = rev.UserId
    ORDER BY e.CreatedAt DESC;
END;
GO

-- =============================================
-- usp_GetExpenseById
-- Get a single expense by ExpenseId
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_GetExpenseById
    @ExpenseId INT
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
        CAST(e.AmountMinor / 100.0 AS DECIMAL(10, 2)) AS Amount,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        rev.UserName AS ReviewedByName,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    JOIN dbo.Users u ON e.UserId = u.UserId
    JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    LEFT JOIN dbo.Users rev ON e.ReviewedBy = rev.UserId
    WHERE e.ExpenseId = @ExpenseId;
END;
GO

-- =============================================
-- usp_GetExpensesByUser
-- Get all expenses for a specific user
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_GetExpensesByUser
    @UserId INT
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
        CAST(e.AmountMinor / 100.0 AS DECIMAL(10, 2)) AS Amount,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        rev.UserName AS ReviewedByName,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    JOIN dbo.Users u ON e.UserId = u.UserId
    JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    LEFT JOIN dbo.Users rev ON e.ReviewedBy = rev.UserId
    WHERE e.UserId = @UserId
    ORDER BY e.CreatedAt DESC;
END;
GO

-- =============================================
-- usp_GetExpensesByStatus
-- Get all expenses filtered by status name
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_GetExpensesByStatus
    @StatusName NVARCHAR(50)
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
        CAST(e.AmountMinor / 100.0 AS DECIMAL(10, 2)) AS Amount,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        rev.UserName AS ReviewedByName,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    JOIN dbo.Users u ON e.UserId = u.UserId
    JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    LEFT JOIN dbo.Users rev ON e.ReviewedBy = rev.UserId
    WHERE s.StatusName = @StatusName
    ORDER BY e.SubmittedAt ASC;
END;
GO

-- =============================================
-- usp_CreateExpense
-- Create a new expense in Draft status
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_CreateExpense
    @UserId      INT,
    @CategoryId  INT,
    @AmountMinor INT,
    @Currency    NVARCHAR(3) = 'GBP',
    @ExpenseDate DATE,
    @Description NVARCHAR(1000) = NULL,
    @ReceiptFile NVARCHAR(500)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @DraftStatusId INT;
    SELECT @DraftStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Draft';

    INSERT INTO dbo.Expenses
        (UserId, CategoryId, StatusId, AmountMinor, Currency, ExpenseDate, Description, ReceiptFile)
    VALUES
        (@UserId, @CategoryId, @DraftStatusId, @AmountMinor, @Currency, @ExpenseDate, @Description, @ReceiptFile);

    DECLARE @NewId INT = SCOPE_IDENTITY();

    EXEC dbo.usp_GetExpenseById @ExpenseId = @NewId;
END;
GO

-- =============================================
-- usp_UpdateExpense
-- Update editable fields of an existing expense (Draft only)
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_UpdateExpense
    @ExpenseId   INT,
    @CategoryId  INT,
    @AmountMinor INT,
    @Currency    NVARCHAR(3) = 'GBP',
    @ExpenseDate DATE,
    @Description NVARCHAR(1000) = NULL,
    @ReceiptFile NVARCHAR(500)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Expenses
    SET
        CategoryId  = @CategoryId,
        AmountMinor = @AmountMinor,
        Currency    = @Currency,
        ExpenseDate = @ExpenseDate,
        Description = @Description,
        ReceiptFile = @ReceiptFile
    WHERE ExpenseId = @ExpenseId;

    EXEC dbo.usp_GetExpenseById @ExpenseId = @ExpenseId;
END;
GO

-- =============================================
-- usp_DeleteExpense
-- Delete an expense record by ID
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_DeleteExpense
    @ExpenseId INT
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM dbo.Expenses
    WHERE ExpenseId = @ExpenseId;

    SELECT @@ROWCOUNT AS RowsDeleted;
END;
GO

-- =============================================
-- usp_SubmitExpense
-- Submit a Draft expense for approval
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_SubmitExpense
    @ExpenseId INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @SubmittedStatusId INT;
    SELECT @SubmittedStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Submitted';

    UPDATE dbo.Expenses
    SET
        StatusId    = @SubmittedStatusId,
        SubmittedAt = SYSUTCDATETIME()
    WHERE ExpenseId = @ExpenseId
      AND StatusId  = (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Draft');

    EXEC dbo.usp_GetExpenseById @ExpenseId = @ExpenseId;
END;
GO

-- =============================================
-- usp_ApproveExpense
-- Approve a submitted expense
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_ApproveExpense
    @ExpenseId   INT,
    @ReviewedBy  INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ApprovedStatusId INT;
    SELECT @ApprovedStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Approved';

    UPDATE dbo.Expenses
    SET
        StatusId   = @ApprovedStatusId,
        ReviewedBy = @ReviewedBy,
        ReviewedAt = SYSUTCDATETIME()
    WHERE ExpenseId = @ExpenseId
      AND StatusId  = (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Submitted');

    EXEC dbo.usp_GetExpenseById @ExpenseId = @ExpenseId;
END;
GO

-- =============================================
-- usp_RejectExpense
-- Reject a submitted expense
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_RejectExpense
    @ExpenseId  INT,
    @ReviewedBy INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @RejectedStatusId INT;
    SELECT @RejectedStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Rejected';

    UPDATE dbo.Expenses
    SET
        StatusId   = @RejectedStatusId,
        ReviewedBy = @ReviewedBy,
        ReviewedAt = SYSUTCDATETIME()
    WHERE ExpenseId = @ExpenseId
      AND StatusId  = (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Submitted');

    EXEC dbo.usp_GetExpenseById @ExpenseId = @ExpenseId;
END;
GO

-- =============================================
-- usp_GetAllUsers
-- List all users with their role name
-- =============================================
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
        mgr.UserName AS ManagerName,
        u.IsActive,
        u.CreatedAt
    FROM dbo.Users u
    JOIN dbo.Roles r ON u.RoleId = r.RoleId
    LEFT JOIN dbo.Users mgr ON u.ManagerId = mgr.UserId
    ORDER BY u.UserName;
END;
GO

-- =============================================
-- usp_GetUserById
-- Get a user by UserId
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_GetUserById
    @UserId INT
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
        mgr.UserName AS ManagerName,
        u.IsActive,
        u.CreatedAt
    FROM dbo.Users u
    JOIN dbo.Roles r ON u.RoleId = r.RoleId
    LEFT JOIN dbo.Users mgr ON u.ManagerId = mgr.UserId
    WHERE u.UserId = @UserId;
END;
GO

-- =============================================
-- usp_CreateUser
-- Create a new user
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_CreateUser
    @UserName  NVARCHAR(100),
    @Email     NVARCHAR(255),
    @RoleId    INT,
    @ManagerId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.Users (UserName, Email, RoleId, ManagerId)
    VALUES (@UserName, @Email, @RoleId, @ManagerId);

    DECLARE @NewId INT = SCOPE_IDENTITY();

    EXEC dbo.usp_GetUserById @UserId = @NewId;
END;
GO

-- =============================================
-- usp_GetAllCategories
-- List all active expense categories
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_GetAllCategories
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        CategoryId,
        CategoryName,
        IsActive
    FROM dbo.ExpenseCategories
    WHERE IsActive = 1
    ORDER BY CategoryName;
END;
GO

-- =============================================
-- usp_GetExpenseSummary
-- Summary stats: total amount and count by status
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_GetExpenseSummary
AS
BEGIN
    SET NOCOUNT ON;

    -- Totals are calculated per (StatusName, Currency) to avoid mixing currencies
    SELECT
        s.StatusName,
        e.Currency,
        COUNT(e.ExpenseId)                                  AS ExpenseCount,
        CAST(SUM(e.AmountMinor) / 100.0 AS DECIMAL(12, 2)) AS TotalAmount
    FROM dbo.Expenses e
    JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    GROUP BY s.StatusName, e.Currency
    ORDER BY s.StatusName, e.Currency;
END;
GO
