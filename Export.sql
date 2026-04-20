USE [master]
GO
/****** Object:  Database [halik]    Script Date: 20.04.2026 22:16:09 ******/
CREATE DATABASE [halik]
 CONTAINMENT = NONE
 ON  PRIMARY 
( NAME = N'halik', FILENAME = N'C:\Program Files\Microsoft SQL Server\MSSQL15.SQLEXPRESS2019\MSSQL\DATA\halik.mdf' , SIZE = 8192KB , MAXSIZE = UNLIMITED, FILEGROWTH = 65536KB )
 LOG ON 
( NAME = N'halik_log', FILENAME = N'C:\Program Files\Microsoft SQL Server\MSSQL15.SQLEXPRESS2019\MSSQL\DATA\halik_log.ldf' , SIZE = 8192KB , MAXSIZE = 2048GB , FILEGROWTH = 65536KB )
 WITH CATALOG_COLLATION = DATABASE_DEFAULT
GO
ALTER DATABASE [halik] SET COMPATIBILITY_LEVEL = 150
GO
IF (1 = FULLTEXTSERVICEPROPERTY('IsFullTextInstalled'))
begin
EXEC [halik].[dbo].[sp_fulltext_database] @action = 'enable'
end
GO
ALTER DATABASE [halik] SET ANSI_NULL_DEFAULT OFF 
GO
ALTER DATABASE [halik] SET ANSI_NULLS OFF 
GO
ALTER DATABASE [halik] SET ANSI_PADDING OFF 
GO
ALTER DATABASE [halik] SET ANSI_WARNINGS OFF 
GO
ALTER DATABASE [halik] SET ARITHABORT OFF 
GO
ALTER DATABASE [halik] SET AUTO_CLOSE ON 
GO
ALTER DATABASE [halik] SET AUTO_SHRINK OFF 
GO
ALTER DATABASE [halik] SET AUTO_UPDATE_STATISTICS ON 
GO
ALTER DATABASE [halik] SET CURSOR_CLOSE_ON_COMMIT OFF 
GO
ALTER DATABASE [halik] SET CURSOR_DEFAULT  GLOBAL 
GO
ALTER DATABASE [halik] SET CONCAT_NULL_YIELDS_NULL OFF 
GO
ALTER DATABASE [halik] SET NUMERIC_ROUNDABORT OFF 
GO
ALTER DATABASE [halik] SET QUOTED_IDENTIFIER OFF 
GO
ALTER DATABASE [halik] SET RECURSIVE_TRIGGERS OFF 
GO
ALTER DATABASE [halik] SET  ENABLE_BROKER 
GO
ALTER DATABASE [halik] SET AUTO_UPDATE_STATISTICS_ASYNC OFF 
GO
ALTER DATABASE [halik] SET DATE_CORRELATION_OPTIMIZATION OFF 
GO
ALTER DATABASE [halik] SET TRUSTWORTHY OFF 
GO
ALTER DATABASE [halik] SET ALLOW_SNAPSHOT_ISOLATION OFF 
GO
ALTER DATABASE [halik] SET PARAMETERIZATION SIMPLE 
GO
ALTER DATABASE [halik] SET READ_COMMITTED_SNAPSHOT OFF 
GO
ALTER DATABASE [halik] SET HONOR_BROKER_PRIORITY OFF 
GO
ALTER DATABASE [halik] SET RECOVERY SIMPLE 
GO
ALTER DATABASE [halik] SET  MULTI_USER 
GO
ALTER DATABASE [halik] SET PAGE_VERIFY CHECKSUM  
GO
ALTER DATABASE [halik] SET DB_CHAINING OFF 
GO
ALTER DATABASE [halik] SET FILESTREAM( NON_TRANSACTED_ACCESS = OFF ) 
GO
ALTER DATABASE [halik] SET TARGET_RECOVERY_TIME = 60 SECONDS 
GO
ALTER DATABASE [halik] SET DELAYED_DURABILITY = DISABLED 
GO
ALTER DATABASE [halik] SET ACCELERATED_DATABASE_RECOVERY = OFF  
GO
ALTER DATABASE [halik] SET QUERY_STORE = OFF
GO
USE [halik]
GO
USE [halik]
GO
/****** Object:  Sequence [dbo].[account_number_sequence]    Script Date: 20.04.2026 22:16:10 ******/
CREATE SEQUENCE [dbo].[account_number_sequence] 
 AS [int]
 START WITH 10000
 INCREMENT BY 1
 MINVALUE 10000
 MAXVALUE 99999
 CACHE 
GO
/****** Object:  View [dbo].[authorLoanStats]    Script Date: 20.04.2026 22:16:10 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE VIEW [dbo].[authorLoanStats] AS
SELECT
    a.firstName + ' ' + a.lastName AS AuthorName,
    COUNT(l.id) AS LoanCount,
    MIN(l.loanDate) AS FirstLoan,
    MAX(l.loanDate) AS LastLoan
FROM Author a
JOIN Book b ON b.authorId = a.id
JOIN Loan l ON l.bookId = b.id
GROUP BY a.firstName, a.lastName;
GO
/****** Object:  View [dbo].[bookLoanStats]    Script Date: 20.04.2026 22:16:10 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE VIEW [dbo].[bookLoanStats] AS
SELECT
    b.title AS BookTitle,
    c.name AS Category,
    COUNT(l.id) AS LoanCount,
    MIN(l.loanDate) AS FirstLoan,
    MAX(l.returnDate) AS LastReturn
FROM Book b
JOIN Category c ON b.categoryId = c.id
LEFT JOIN Loan l ON l.bookId = b.id
GROUP BY b.title, c.name;
GO
/****** Object:  View [dbo].[view_active_accounts]    Script Date: 20.04.2026 22:16:10 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
create view [dbo].[view_active_accounts]
as
select account_number, balance, created_at_date
from account
where is_active = 1;
GO
/****** Object:  View [dbo].[view_available_account_numbers]    Script Date: 20.04.2026 22:16:10 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- View for available account numbers (available for recyclation)
create view [dbo].[view_available_account_numbers]
as
select account_number, closed_at_date
from account
where is_active = 0;
GO
/****** Object:  View [dbo].[view_bank_client_count]    Script Date: 20.04.2026 22:16:10 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
create view [dbo].[view_bank_client_count]
as
select count(*) as client_count
from account
where is_active = 1;
GO
/****** Object:  View [dbo].[view_bank_total_amount]    Script Date: 20.04.2026 22:16:10 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
create view [dbo].[view_bank_total_amount]
as
select sum(balance) as total_amount
from account
where is_active = 1;
GO
/****** Object:  View [dbo].[view_closed_accounts]    Script Date: 20.04.2026 22:16:10 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- View for closed accounts
create view [dbo].[view_closed_accounts]
as
select account_number, balance, created_at_date, closed_at_date
from account
where is_active = 0;
GO
/****** Object:  View [dbo].[View_SalesSummary]    Script Date: 20.04.2026 22:16:10 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE VIEW [dbo].[View_SalesSummary] AS
SELECT 
    c.CustomerID,
    c.FirstName + ' ' + c.LastName AS CustomerName,
    c.Email,
    COUNT(DISTINCT o.OrderID) AS TotalOrders,
    COUNT(oi.OrderItemID) AS TotalItems,
    SUM(oi.TotalPrice) AS TotalSpent,
    AVG(oi.TotalPrice) AS AvgItemPrice,
    MAX(o.OrderDate) AS LastOrderDate
FROM Customers c
LEFT JOIN Orders o ON c.CustomerID = o.CustomerID
LEFT JOIN OrderItems oi ON o.OrderID = oi.OrderID
GROUP BY c.CustomerID, c.FirstName, c.LastName, c.Email;
GO
/****** Object:  View [dbo].[view_ui_client_activity]    Script Date: 20.04.2026 22:16:10 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
create view [dbo].[view_ui_client_activity] as
select
    client_ip,
    command,
    arguments,
    executed_at,
    result_status
from client_command_log;
GO
/****** Object:  Table [dbo].[CustomQuizQuestions]    Script Date: 20.04.2026 22:16:10 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[CustomQuizQuestions](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[QuizId] [int] NOT NULL,
	[QuestionText] [nvarchar](500) NOT NULL,
	[Answer1] [nvarchar](250) NOT NULL,
	[Answer2] [nvarchar](250) NOT NULL,
	[Answer3] [nvarchar](250) NULL,
	[Answer4] [nvarchar](250) NULL,
	[CorrectAnswer] [nvarchar](250) NOT NULL,
	[QuestionOrder] [int] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[CustomQuizzes]    Script Date: 20.04.2026 22:16:10 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[CustomQuizzes](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[UserId] [int] NOT NULL,
	[Title] [nvarchar](120) NOT NULL,
	[TimePerQuestion] [int] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[RankedMatches]    Script Date: 20.04.2026 22:16:10 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[RankedMatches](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Mode] [nvarchar](20) NOT NULL,
	[PlayedAt] [datetime] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[RankedMatchResults]    Script Date: 20.04.2026 22:16:10 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[RankedMatchResults](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[MatchId] [int] NOT NULL,
	[UserId] [int] NOT NULL,
	[Score] [int] NOT NULL,
	[Placement] [int] NOT NULL,
	[EloBefore] [int] NOT NULL,
	[EloAfter] [int] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[UserRankings]    Script Date: 20.04.2026 22:16:10 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[UserRankings](
	[UserId] [int] NOT NULL,
	[SingleElo] [int] NOT NULL,
	[MultiElo] [int] NOT NULL,
	[SingleRankedPlayed] [int] NOT NULL,
	[SingleRankedWins] [int] NOT NULL,
	[MultiRankedPlayed] [int] NOT NULL,
	[MultiRankedWins] [int] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Users]    Script Date: 20.04.2026 22:16:10 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Users](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Username] [nvarchar](50) NOT NULL,
	[PasswordHash] [nvarchar](255) NOT NULL,
	[CreatedAt] [datetime] NULL,
	[AvatarKey] [nvarchar](50) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[Username] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Users_Username_CI]    Script Date: 20.04.2026 22:16:10 ******/
CREATE UNIQUE NONCLUSTERED INDEX [IX_Users_Username_CI] ON [dbo].[Users]
(
	[Username] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
ALTER TABLE [dbo].[CustomQuizzes] ADD  DEFAULT ((15)) FOR [TimePerQuestion]
GO
ALTER TABLE [dbo].[CustomQuizzes] ADD  DEFAULT (getutcdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[RankedMatches] ADD  DEFAULT (getdate()) FOR [PlayedAt]
GO
ALTER TABLE [dbo].[UserRankings] ADD  DEFAULT ((1000)) FOR [SingleElo]
GO
ALTER TABLE [dbo].[UserRankings] ADD  DEFAULT ((1000)) FOR [MultiElo]
GO
ALTER TABLE [dbo].[UserRankings] ADD  DEFAULT ((0)) FOR [SingleRankedPlayed]
GO
ALTER TABLE [dbo].[UserRankings] ADD  DEFAULT ((0)) FOR [SingleRankedWins]
GO
ALTER TABLE [dbo].[UserRankings] ADD  DEFAULT ((0)) FOR [MultiRankedPlayed]
GO
ALTER TABLE [dbo].[UserRankings] ADD  DEFAULT ((0)) FOR [MultiRankedWins]
GO
ALTER TABLE [dbo].[Users] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Users] ADD  CONSTRAINT [DF_Users_AvatarKey]  DEFAULT ('default_1') FOR [AvatarKey]
GO
ALTER TABLE [dbo].[CustomQuizQuestions]  WITH CHECK ADD  CONSTRAINT [FK_CustomQuizQuestions_CustomQuizzes] FOREIGN KEY([QuizId])
REFERENCES [dbo].[CustomQuizzes] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[CustomQuizQuestions] CHECK CONSTRAINT [FK_CustomQuizQuestions_CustomQuizzes]
GO
ALTER TABLE [dbo].[RankedMatchResults]  WITH CHECK ADD FOREIGN KEY([MatchId])
REFERENCES [dbo].[RankedMatches] ([Id])
GO
ALTER TABLE [dbo].[RankedMatchResults]  WITH CHECK ADD FOREIGN KEY([UserId])
REFERENCES [dbo].[Users] ([Id])
GO
ALTER TABLE [dbo].[UserRankings]  WITH CHECK ADD FOREIGN KEY([UserId])
REFERENCES [dbo].[Users] ([Id])
GO
/****** Object:  StoredProcedure [dbo].[create_account]    Script Date: 20.04.2026 22:16:10 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
create procedure [dbo].[create_account]
as
begin
    set xact_abort on;

    declare @account_number int;

    begin transaction;

    begin try
        set @account_number = next value for account_number_sequence;

        insert into account (account_number)
        values (@account_number);
    end try
    begin catch
        select top 1 @account_number = account_number
        from account with (updlock, readpast)
        where is_active = 0
        order by closed_at_date;

        if @account_number is null
        begin
            raiserror('No available account numbers.', 16, 1);
            rollback;
            return;
        end

        update account
        set is_active = 1, balance = 0, closed_at_date = null
        where account_number = @account_number and is_active = 0;
    end catch;

    commit;

    select @account_number as account_number;
end;
GO
/****** Object:  StoredProcedure [dbo].[deposit_account]    Script Date: 20.04.2026 22:16:10 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
create procedure [dbo].[deposit_account] @account_number int, @amount bigint
as
begin
    set xact_abort on;

    if @amount <= 0
    begin
        raiserror('Invalid amount.', 16, 1);
        return;
    end

    update account set balance = balance + @amount
    where account_number = @account_number and is_active = 1;

    if @@rowcount = 0
	begin
        raiserror('Account not found.', 16, 1);
	end
end;
GO
/****** Object:  StoredProcedure [dbo].[get_account_balance]    Script Date: 20.04.2026 22:16:10 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
create procedure [dbo].[get_account_balance] @account_number int
as
begin
    select balance
    from account
    where account_number = @account_number and is_active = 1;

    if @@rowcount = 0
	begin
        raiserror('Account not found.', 16, 1);
	end
end;
GO
/****** Object:  StoredProcedure [dbo].[remove_account]    Script Date: 20.04.2026 22:16:10 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
create procedure [dbo].[remove_account] @account_number int
as
begin
    set xact_abort on;

    update account set is_active = 0, closed_at_date = sysutcdatetime()
    where account_number = @account_number and is_active = 1 and balance = 0;

    if @@rowcount = 0
	begin
        raiserror('Account cannot be removed.', 16, 1);
	end
end;
GO
/****** Object:  StoredProcedure [dbo].[withdraw_account]    Script Date: 20.04.2026 22:16:10 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
create procedure [dbo].[withdraw_account] @account_number int, @amount bigint
as
begin
    set xact_abort on;

    if @amount <= 0
    begin
        raiserror('Invalid amount.', 16, 1);
        return;
    end

    update account set balance = balance - @amount
    where account_number = @account_number and is_active = 1 and balance >= @amount;

    if @@rowcount = 0
	begin
        raiserror('Insufficient funds or account not found.', 16, 1);
	end
end;
GO
USE [master]
GO
ALTER DATABASE [halik] SET  READ_WRITE 
GO
