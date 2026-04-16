-- stored-procedures.sql
-- All stored procedures for the Expense Management System
-- App code uses ONLY these stored procedures - no direct T-SQL in application code
-- Use CREATE OR ALTER to safely re-run

-- ============================================================
-- usp_GetExpenses: Get all expenses with optional text filter
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.usp_GetExpenses
    @Filter NVARCHAR(200) = NULL,
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
        CAST(e.AmountMinor / 100.0 AS DECIMAL(10,2)) AS AmountGBP,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    JOIN dbo.Users u ON e.UserId = u.UserId
    JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    WHERE
        (@UserId IS NULL OR e.UserId = @UserId)
        AND (
            @Filter IS NULL
            OR c.CategoryName LIKE '%' + @Filter + '%'
            OR s.StatusName LIKE '%' + @Filter + '%'
            OR e.Description LIKE '%' + @Filter + '%'
            OR u.UserName LIKE '%' + @Filter + '%'
        )
    ORDER BY e.CreatedAt DESC;
END;
GO

-- ============================================================
-- usp_GetExpenseById: Get a single expense by ID
-- ============================================================
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
        e.AmountMinor,
        CAST(e.AmountMinor / 100.0 AS DECIMAL(10,2)) AS AmountGBP,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    JOIN dbo.Users u ON e.UserId = u.UserId
    JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    WHERE e.ExpenseId = @ExpenseId;
END;
GO

-- ============================================================
-- usp_CreateExpense: Create a new expense (as Draft)
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.usp_CreateExpense
    @UserId INT,
    @CategoryId INT,
    @AmountMinor INT,           -- amount in pence e.g. £12.34 = 1234
    @ExpenseDate DATE,
    @Description NVARCHAR(1000) = NULL,
    @ReceiptFile NVARCHAR(500) = NULL,
    @ExpenseId INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @DraftStatusId INT;
    SELECT @DraftStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Draft';

    INSERT INTO dbo.Expenses (UserId, CategoryId, StatusId, AmountMinor, Currency, ExpenseDate, Description, ReceiptFile, CreatedAt)
    VALUES (@UserId, @CategoryId, @DraftStatusId, @AmountMinor, 'GBP', @ExpenseDate, @Description, @ReceiptFile, SYSUTCDATETIME());

    SET @ExpenseId = SCOPE_IDENTITY();
END;
GO

-- ============================================================
-- usp_SubmitExpense: Submit a Draft expense for approval
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.usp_SubmitExpense
    @ExpenseId INT,
    @UserId INT               -- for ownership check
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @SubmittedStatusId INT, @DraftStatusId INT;
    SELECT @SubmittedStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Submitted';
    SELECT @DraftStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Draft';

    UPDATE dbo.Expenses
    SET StatusId = @SubmittedStatusId,
        SubmittedAt = SYSUTCDATETIME()
    WHERE ExpenseId = @ExpenseId
      AND UserId = @UserId
      AND StatusId = @DraftStatusId;

    SELECT @@ROWCOUNT AS RowsAffected;
END;
GO

-- ============================================================
-- usp_ApproveExpense: Manager approves a Submitted expense
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.usp_ApproveExpense
    @ExpenseId INT,
    @ReviewerId INT           -- manager's UserId
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ApprovedStatusId INT, @SubmittedStatusId INT;
    SELECT @ApprovedStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Approved';
    SELECT @SubmittedStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Submitted';

    UPDATE dbo.Expenses
    SET StatusId = @ApprovedStatusId,
        ReviewedBy = @ReviewerId,
        ReviewedAt = SYSUTCDATETIME()
    WHERE ExpenseId = @ExpenseId
      AND StatusId = @SubmittedStatusId;

    SELECT @@ROWCOUNT AS RowsAffected;
END;
GO

-- ============================================================
-- usp_RejectExpense: Manager rejects a Submitted expense
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.usp_RejectExpense
    @ExpenseId INT,
    @ReviewerId INT,          -- manager's UserId
    @RejectionReason NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @RejectedStatusId INT, @SubmittedStatusId INT;
    SELECT @RejectedStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Rejected';
    SELECT @SubmittedStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Submitted';

    UPDATE dbo.Expenses
    SET StatusId = @RejectedStatusId,
        ReviewedBy = @ReviewerId,
        ReviewedAt = SYSUTCDATETIME(),
        Description = CASE WHEN @RejectionReason IS NOT NULL
                      THEN ISNULL(Description, '') + ' [Rejected: ' + @RejectionReason + ']'
                      ELSE Description END
    WHERE ExpenseId = @ExpenseId
      AND StatusId = @SubmittedStatusId;

    SELECT @@ROWCOUNT AS RowsAffected;
END;
GO

-- ============================================================
-- usp_GetPendingExpenses: Get all Submitted expenses (for manager)
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.usp_GetPendingExpenses
    @Filter NVARCHAR(200) = NULL
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
        CAST(e.AmountMinor / 100.0 AS DECIMAL(10,2)) AS AmountGBP,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    JOIN dbo.Users u ON e.UserId = u.UserId
    JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    WHERE s.StatusName = 'Submitted'
      AND (
          @Filter IS NULL
          OR c.CategoryName LIKE '%' + @Filter + '%'
          OR u.UserName LIKE '%' + @Filter + '%'
          OR e.Description LIKE '%' + @Filter + '%'
      )
    ORDER BY e.SubmittedAt ASC;
END;
GO

-- ============================================================
-- usp_GetCategories: Get all active expense categories
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.usp_GetCategories
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CategoryId, CategoryName
    FROM dbo.ExpenseCategories
    WHERE IsActive = 1
    ORDER BY CategoryName;
END;
GO

-- ============================================================
-- usp_GetUsers: Get all active users
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.usp_GetUsers
AS
BEGIN
    SET NOCOUNT ON;
    SELECT u.UserId, u.UserName, u.Email, r.RoleName, u.ManagerId, u.IsActive
    FROM dbo.Users u
    JOIN dbo.Roles r ON u.RoleId = r.RoleId
    WHERE u.IsActive = 1
    ORDER BY u.UserName;
END;
GO

-- ============================================================
-- usp_GetExpensesByUserId: Get expenses for a specific user
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.usp_GetExpensesByUserId
    @UserId INT,
    @Filter NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.usp_GetExpenses @Filter = @Filter, @UserId = @UserId;
END;
GO

-- ============================================================
-- usp_DeleteExpense: Delete a Draft expense (employee only)
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.usp_DeleteExpense
    @ExpenseId INT,
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @DraftStatusId INT;
    SELECT @DraftStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Draft';

    DELETE FROM dbo.Expenses
    WHERE ExpenseId = @ExpenseId
      AND UserId = @UserId
      AND StatusId = @DraftStatusId;

    SELECT @@ROWCOUNT AS RowsAffected;
END;
GO

-- ============================================================
-- usp_GetExpenseSummary: Summary statistics for dashboard
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.usp_GetExpenseSummary
    @UserId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        s.StatusName,
        COUNT(*) AS ExpenseCount,
        CAST(SUM(e.AmountMinor) / 100.0 AS DECIMAL(10,2)) AS TotalAmountGBP
    FROM dbo.Expenses e
    JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    WHERE (@UserId IS NULL OR e.UserId = @UserId)
    GROUP BY s.StatusName, s.StatusId
    ORDER BY s.StatusId;
END;
GO
