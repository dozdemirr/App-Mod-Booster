
/*
  expenses_system.sql
  SQL Server style schema and sample data for Expense Management System
  Roles: Employee (can submit), Manager (can approve)
  Currency: GBP
  Generated: 2025-11-04
*/

SET NOCOUNT ON;
GO

-- Drop existing objects if they exist (safe for development)
IF OBJECT_ID('dbo.Expenses', 'U') IS NOT NULL DROP TABLE dbo.Expenses;
IF OBJECT_ID('dbo.ExpenseStatus', 'U') IS NOT NULL DROP TABLE dbo.ExpenseStatus;
IF OBJECT_ID('dbo.ExpenseCategories', 'U') IS NOT NULL DROP TABLE dbo.ExpenseCategories;
IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL DROP TABLE dbo.Users;
IF OBJECT_ID('dbo.Roles', 'U') IS NOT NULL DROP TABLE dbo.Roles;
GO

-- Roles
CREATE TABLE dbo.Roles
(
    RoleId      INT IDENTITY(1,1) PRIMARY KEY,
    RoleName    NVARCHAR(50) NOT NULL UNIQUE, -- 'Employee', 'Manager'
    Description NVARCHAR(250) NULL
);
GO

-- Users
CREATE TABLE dbo.Users
(
    UserId       INT IDENTITY(1,1) PRIMARY KEY,
    UserName     NVARCHAR(100) NOT NULL,
    Email        NVARCHAR(255) NOT NULL UNIQUE,
    RoleId       INT NOT NULL REFERENCES dbo.Roles(RoleId),
    ManagerId    INT NULL, -- self-referencing to another user who is the manager
    IsActive     BIT NOT NULL DEFAULT 1,
    CreatedAt    DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
GO

ALTER TABLE dbo.Users
ADD CONSTRAINT FK_Users_Manager FOREIGN KEY (ManagerId) REFERENCES dbo.Users(UserId);
GO

-- Expense Categories
CREATE TABLE dbo.ExpenseCategories
(
    CategoryId INT IDENTITY(1,1) PRIMARY KEY,
    CategoryName NVARCHAR(100) NOT NULL UNIQUE,
    IsActive BIT NOT NULL DEFAULT 1
);
GO

-- Expense Status (lookup)
CREATE TABLE dbo.ExpenseStatus
(
    StatusId INT IDENTITY(1,1) PRIMARY KEY,
    StatusName NVARCHAR(50) NOT NULL UNIQUE -- 'Draft', 'Submitted', 'Approved', 'Rejected'
);
GO

-- Expenses
CREATE TABLE dbo.Expenses
(
    ExpenseId      INT IDENTITY(1,1) PRIMARY KEY,
    UserId         INT NOT NULL REFERENCES dbo.Users(UserId),
    CategoryId     INT NOT NULL REFERENCES dbo.ExpenseCategories(CategoryId),
    StatusId       INT NOT NULL REFERENCES dbo.ExpenseStatus(StatusId),
    AmountMinor    INT NOT NULL, -- amount stored in minor units (pence) to avoid floating point issues (e.g., £12.34 -> 1234)
    Currency       NVARCHAR(3) NOT NULL DEFAULT 'GBP',
    ExpenseDate    DATE NOT NULL,
    Description    NVARCHAR(1000) NULL,
    ReceiptFile    NVARCHAR(500) NULL, -- path or blob reference depending on implementation
    SubmittedAt    DATETIME2 NULL,
    ReviewedBy     INT NULL REFERENCES dbo.Users(UserId), -- manager who approved/rejected
    ReviewedAt     DATETIME2 NULL,
    CreatedAt      DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
GO

-- Indexes to help common queries
CREATE INDEX IX_Expenses_UserId_StatusId ON dbo.Expenses(UserId, StatusId);
CREATE INDEX IX_Expenses_SubmittedAt ON dbo.Expenses(SubmittedAt);
GO

-- Seed lookup data
INSERT INTO dbo.Roles (RoleName, Description) VALUES
('Employee', 'Regular employee who can submit expenses'),
('Manager', 'Can view and approve/reject submitted expenses');

INSERT INTO dbo.ExpenseCategories (CategoryName) VALUES
('Travel'),
('Meals'),
('Supplies'),
('Accommodation'),
('Other');

INSERT INTO dbo.ExpenseStatus (StatusName) VALUES
('Draft'),
('Submitted'),
('Approved'),
('Rejected');

-- Seed sample users
INSERT INTO dbo.Users (UserName, Email, RoleId, ManagerId)
VALUES
('Alice Example', 'alice@example.co.uk', (SELECT RoleId FROM dbo.Roles WHERE RoleName = 'Employee'), NULL),
('Bob Manager', 'bob.manager@example.co.uk', (SELECT RoleId FROM dbo.Roles WHERE RoleName = 'Manager'), NULL);

-- Make Alice report to Bob (set manager relationship)
UPDATE dbo.Users
SET ManagerId = (SELECT UserId FROM dbo.Users WHERE Email = 'bob.manager@example.co.uk')
WHERE Email = 'alice@example.co.uk';

-- Seed sample expenses (amounts in pence)
INSERT INTO dbo.Expenses (UserId, CategoryId, StatusId, AmountMinor, Currency, ExpenseDate, Description, ReceiptFile, SubmittedAt, CreatedAt)
VALUES
(
    (SELECT UserId FROM dbo.Users WHERE Email = 'alice@example.co.uk'),
    (SELECT CategoryId FROM dbo.ExpenseCategories WHERE CategoryName = 'Travel'),
    (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Submitted'),
    2540, -- £25.40
    'GBP',
    '2025-10-20',
    'Taxi from airport to client site',
    '/receipts/alice/taxi_oct20.jpg',
    SYSUTCDATETIME(),
    SYSUTCDATETIME()
),
(
    (SELECT UserId FROM dbo.Users WHERE Email = 'alice@example.co.uk'),
    (SELECT CategoryId FROM dbo.ExpenseCategories WHERE CategoryName = 'Meals'),
    (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Approved'),
    1425, -- £14.25
    'GBP',
    '2025-09-15',
    'Client lunch meeting',
    '/receipts/alice/lunch_sep15.jpg',
    '2025-09-16T10:15:00',
    '2025-09-15T18:30:00'
),
(
    (SELECT UserId FROM dbo.Users WHERE Email = 'alice@example.co.uk'),
    (SELECT CategoryId FROM dbo.ExpenseCategories WHERE CategoryName = 'Supplies'),
    (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Draft'),
    799, -- £7.99
    'GBP',
    '2025-11-01',
    'Office stationery',
    NULL,
    NULL,
    SYSUTCDATETIME()
);

-- Example of approving an expense (manager action)
INSERT INTO dbo.Expenses (UserId, CategoryId, StatusId, AmountMinor, Currency, ExpenseDate, Description, ReceiptFile, SubmittedAt, ReviewedBy, ReviewedAt, CreatedAt)
VALUES
(
    (SELECT UserId FROM dbo.Users WHERE Email = 'alice@example.co.uk'),
    (SELECT CategoryId FROM dbo.ExpenseCategories WHERE CategoryName = 'Accommodation'),
    (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Approved'),
    12300, -- £123.00
    'GBP',
    '2025-08-10',
    'Hotel during client visit',
    '/receipts/alice/hotel_aug10.jpg',
    '2025-08-11T09:00:00',
    (SELECT UserId FROM dbo.Users WHERE Email = 'bob.manager@example.co.uk'),
    '2025-08-12T14:30:00',
    '2025-08-10T20:00:00'
);

GO

-- Sample query: List pending submitted expenses for managers to review
-- SELECT e.ExpenseId, u.UserName, c.CategoryName, CAST(e.AmountMinor/100.0 AS DECIMAL(10,2)) AS AmountGBP, s.StatusName, e.SubmittedAt
-- FROM dbo.Expenses e
-- JOIN dbo.Users u ON e.UserId = u.UserId
-- JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
-- JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
-- WHERE s.StatusName = 'Submitted'
-- ORDER BY e.SubmittedAt ASC;

