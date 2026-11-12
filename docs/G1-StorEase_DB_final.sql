/* StorEase - final database script (G1-StorEase)
   Creates the StorEase database with all 24 migrations (36 tables) and loads the starting data:
   5 roles, one demo account per role (password Demo@123), one facility with 6 units,
   2 unit sizes with prices, the company standard price list and 5 support ticket categories.
   Run it once, on a SQL Server that has no StorEase database yet.
   Run in SQL Server Management Studio, or: sqlcmd -S localhost\SQLEXPRESS -E -C -I -i G1-StorEase_DB_final.sql */
IF DB_ID(N'StorEase') IS NULL EXEC(N'CREATE DATABASE [StorEase]');
GO
USE [StorEase];
GO
IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE TABLE [Role] (
        [RoleId] int NOT NULL IDENTITY,
        [RoleName] varchar(50) NOT NULL,
        [Description] varchar(200) NULL,
        CONSTRAINT [PK_Role] PRIMARY KEY ([RoleId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE TABLE [TicketCategory] (
        [CategoryId] int NOT NULL IDENTITY,
        [Name] nvarchar(50) NOT NULL,
        [DefaultPriority] varchar(20) NOT NULL,
        CONSTRAINT [PK_TicketCategory] PRIMARY KEY ([CategoryId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE TABLE [UnitType] (
        [UnitTypeId] int NOT NULL IDENTITY,
        [Code] varchar(10) NOT NULL,
        [Name] nvarchar(50) NOT NULL,
        [AreaM2] decimal(5,2) NOT NULL,
        [Width] decimal(4,2) NOT NULL,
        [Depth] decimal(4,2) NOT NULL,
        [Height] decimal(4,2) NOT NULL,
        [IsClimateControlled] bit NOT NULL,
        CONSTRAINT [PK_UnitType] PRIMARY KEY ([UnitTypeId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE TABLE [User] (
        [UserId] int NOT NULL IDENTITY,
        [RoleId] int NOT NULL,
        [FullName] nvarchar(100) NOT NULL,
        [Email] varchar(100) NOT NULL,
        [PhoneNumber] varchar(20) NOT NULL,
        [PasswordHash] varchar(255) NOT NULL,
        [CitizenId] varchar(20) NULL,
        [DateOfBirth] date NULL,
        [Address] nvarchar(200) NULL,
        [Status] varchar(20) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_User] PRIMARY KEY ([UserId]),
        CONSTRAINT [FK_User_Role_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Role] ([RoleId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE TABLE [Facility] (
        [FacilityId] int NOT NULL IDENTITY,
        [ManagerId] int NULL,
        [Code] varchar(10) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Address] nvarchar(200) NOT NULL,
        [District] nvarchar(50) NOT NULL,
        [Phone] varchar(20) NULL,
        [OpeningHours] varchar(50) NULL,
        [Status] varchar(20) NOT NULL,
        CONSTRAINT [PK_Facility] PRIMARY KEY ([FacilityId]),
        CONSTRAINT [FK_Facility_User_ManagerId] FOREIGN KEY ([ManagerId]) REFERENCES [User] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE TABLE [PricingPolicy] (
        [PricingPolicyId] int NOT NULL IDENTITY,
        [CreatedBy] int NOT NULL,
        [Version] int NOT NULL,
        [EffectiveFrom] date NOT NULL,
        [EffectiveTo] date NULL,
        [DepositMonths] int NOT NULL,
        [GraceDays] int NOT NULL,
        [NoticeDays] int NOT NULL DEFAULT 30,
        [LateFeePercentPerWeek] decimal(5,2) NOT NULL,
        [SuspendAfterDays] int NOT NULL,
        [ClearOutAfterDays] int NOT NULL,
        [CancelFreeHours] int NOT NULL,
        [Status] varchar(20) NOT NULL,
        CONSTRAINT [PK_PricingPolicy] PRIMARY KEY ([PricingPolicyId]),
        CONSTRAINT [FK_PricingPolicy_User_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [User] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE TABLE [ActivityLog] (
        [LogId] bigint NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [FacilityId] int NULL,
        [Action] varchar(50) NOT NULL,
        [EntityName] varchar(50) NULL,
        [EntityId] varchar(30) NULL,
        [IpAddress] varchar(45) NULL,
        [Result] varchar(20) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ActivityLog] PRIMARY KEY ([LogId]),
        CONSTRAINT [FK_ActivityLog_Facility_FacilityId] FOREIGN KEY ([FacilityId]) REFERENCES [Facility] ([FacilityId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ActivityLog_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [User] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE TABLE [Floor] (
        [FloorId] int NOT NULL IDENTITY,
        [FacilityId] int NOT NULL,
        [FloorNumber] int NOT NULL,
        [Name] nvarchar(50) NULL,
        CONSTRAINT [PK_Floor] PRIMARY KEY ([FloorId]),
        CONSTRAINT [FK_Floor_Facility_FacilityId] FOREIGN KEY ([FacilityId]) REFERENCES [Facility] ([FacilityId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE TABLE [Shift] (
        [ShiftId] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [FacilityId] int NOT NULL,
        [ShiftDate] date NOT NULL,
        [StartTime] time NOT NULL,
        [EndTime] time NOT NULL,
        [ShiftType] varchar(20) NOT NULL,
        CONSTRAINT [PK_Shift] PRIMARY KEY ([ShiftId]),
        CONSTRAINT [FK_Shift_Facility_FacilityId] FOREIGN KEY ([FacilityId]) REFERENCES [Facility] ([FacilityId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Shift_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [User] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE TABLE [StaffFacility] (
        [StaffFacilityId] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [FacilityId] int NOT NULL,
        [AssignedDate] date NOT NULL,
        CONSTRAINT [PK_StaffFacility] PRIMARY KEY ([StaffFacilityId]),
        CONSTRAINT [FK_StaffFacility_Facility_FacilityId] FOREIGN KEY ([FacilityId]) REFERENCES [Facility] ([FacilityId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StaffFacility_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [User] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE TABLE [FeeType] (
        [FeeTypeId] int NOT NULL IDENTITY,
        [PricingPolicyId] int NOT NULL,
        [Code] varchar(20) NOT NULL,
        [Name] nvarchar(50) NOT NULL,
        [Amount] decimal(12,2) NOT NULL,
        [IsPercentage] bit NOT NULL,
        CONSTRAINT [PK_FeeType] PRIMARY KEY ([FeeTypeId]),
        CONSTRAINT [FK_FeeType_PricingPolicy_PricingPolicyId] FOREIGN KEY ([PricingPolicyId]) REFERENCES [PricingPolicy] ([PricingPolicyId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE TABLE [UnitPrice] (
        [UnitPriceId] int NOT NULL IDENTITY,
        [PricingPolicyId] int NOT NULL,
        [UnitTypeId] int NOT NULL,
        [BasePricePerMonth] decimal(12,2) NOT NULL,
        [Discount3Month] decimal(5,2) NOT NULL,
        [Discount6Month] decimal(5,2) NOT NULL,
        [Discount12Month] decimal(5,2) NOT NULL,
        CONSTRAINT [PK_UnitPrice] PRIMARY KEY ([UnitPriceId]),
        CONSTRAINT [FK_UnitPrice_PricingPolicy_PricingPolicyId] FOREIGN KEY ([PricingPolicyId]) REFERENCES [PricingPolicy] ([PricingPolicyId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UnitPrice_UnitType_UnitTypeId] FOREIGN KEY ([UnitTypeId]) REFERENCES [UnitType] ([UnitTypeId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE TABLE [StorageUnit] (
        [StorageUnitId] int NOT NULL IDENTITY,
        [FacilityId] int NOT NULL,
        [FloorId] int NOT NULL,
        [UnitTypeId] int NOT NULL,
        [UnitCode] varchar(20) NOT NULL,
        [Zone] varchar(10) NULL,
        [Status] varchar(20) NOT NULL,
        [CurrentPrice] decimal(12,2) NOT NULL,
        [Note] nvarchar(200) NULL,
        CONSTRAINT [PK_StorageUnit] PRIMARY KEY ([StorageUnitId]),
        CONSTRAINT [FK_StorageUnit_Facility_FacilityId] FOREIGN KEY ([FacilityId]) REFERENCES [Facility] ([FacilityId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StorageUnit_Floor_FloorId] FOREIGN KEY ([FloorId]) REFERENCES [Floor] ([FloorId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StorageUnit_UnitType_UnitTypeId] FOREIGN KEY ([UnitTypeId]) REFERENCES [UnitType] ([UnitTypeId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE TABLE [StaffTask] (
        [TaskId] int NOT NULL IDENTITY,
        [ShiftId] int NULL,
        [AssignedTo] int NOT NULL,
        [TaskType] varchar(20) NOT NULL,
        [ReferenceCode] varchar(30) NULL,
        [ScheduledAt] datetime2 NOT NULL,
        [Status] varchar(20) NOT NULL,
        CONSTRAINT [PK_StaffTask] PRIMARY KEY ([TaskId]),
        CONSTRAINT [FK_StaffTask_Shift_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [Shift] ([ShiftId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StaffTask_User_AssignedTo] FOREIGN KEY ([AssignedTo]) REFERENCES [User] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE TABLE [Reservation] (
        [ReservationId] int NOT NULL IDENTITY,
        [CustomerId] int NOT NULL,
        [FacilityId] int NOT NULL,
        [UnitTypeId] int NOT NULL,
        [StorageUnitId] int NULL,
        [CreatedBy] int NULL,
        [ReservationCode] varchar(20) NOT NULL,
        [MoveInDate] date NOT NULL,
        [RentalMonths] int NOT NULL,
        [CheckInSlot] datetime2 NULL,
        [Status] varchar(20) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Reservation] PRIMARY KEY ([ReservationId]),
        CONSTRAINT [FK_Reservation_Facility_FacilityId] FOREIGN KEY ([FacilityId]) REFERENCES [Facility] ([FacilityId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Reservation_StorageUnit_StorageUnitId] FOREIGN KEY ([StorageUnitId]) REFERENCES [StorageUnit] ([StorageUnitId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Reservation_UnitType_UnitTypeId] FOREIGN KEY ([UnitTypeId]) REFERENCES [UnitType] ([UnitTypeId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Reservation_User_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [User] ([UserId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Reservation_User_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [User] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE TABLE [SupportTicket] (
        [TicketId] int NOT NULL IDENTITY,
        [CustomerId] int NOT NULL,
        [StorageUnitId] int NULL,
        [FacilityId] int NOT NULL,
        [CategoryId] int NOT NULL,
        [AssignedTo] int NULL,
        [TicketCode] varchar(20) NOT NULL,
        [Subject] nvarchar(200) NOT NULL,
        [Priority] varchar(20) NOT NULL,
        [Status] varchar(20) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ResolvedAt] datetime2 NULL,
        CONSTRAINT [PK_SupportTicket] PRIMARY KEY ([TicketId]),
        CONSTRAINT [FK_SupportTicket_Facility_FacilityId] FOREIGN KEY ([FacilityId]) REFERENCES [Facility] ([FacilityId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SupportTicket_StorageUnit_StorageUnitId] FOREIGN KEY ([StorageUnitId]) REFERENCES [StorageUnit] ([StorageUnitId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SupportTicket_TicketCategory_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [TicketCategory] ([CategoryId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SupportTicket_User_AssignedTo] FOREIGN KEY ([AssignedTo]) REFERENCES [User] ([UserId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SupportTicket_User_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [User] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE TABLE [Contract] (
        [ContractId] int NOT NULL IDENTITY,
        [ReservationId] int NOT NULL,
        [CustomerId] int NOT NULL,
        [StorageUnitId] int NOT NULL,
        [ContractNumber] varchar(20) NOT NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NOT NULL,
        [MonthlyRent] decimal(12,2) NOT NULL,
        [DepositAmount] decimal(12,2) NOT NULL,
        [PaymentDay] int NOT NULL,
        [SignedAt] datetime2 NULL,
        [Status] varchar(20) NOT NULL,
        CONSTRAINT [PK_Contract] PRIMARY KEY ([ContractId]),
        CONSTRAINT [FK_Contract_Reservation_ReservationId] FOREIGN KEY ([ReservationId]) REFERENCES [Reservation] ([ReservationId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Contract_StorageUnit_StorageUnitId] FOREIGN KEY ([StorageUnitId]) REFERENCES [StorageUnit] ([StorageUnitId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Contract_User_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [User] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE TABLE [MaintenanceOrder] (
        [MaintenanceOrderId] int NOT NULL IDENTITY,
        [StorageUnitId] int NOT NULL,
        [TicketId] int NULL,
        [AssignedTo] int NULL,
        [Description] nvarchar(300) NOT NULL,
        [ScheduledAt] datetime2 NULL,
        [CompletedAt] datetime2 NULL,
        [Cost] decimal(12,2) NULL,
        [Status] varchar(20) NOT NULL,
        CONSTRAINT [PK_MaintenanceOrder] PRIMARY KEY ([MaintenanceOrderId]),
        CONSTRAINT [FK_MaintenanceOrder_StorageUnit_StorageUnitId] FOREIGN KEY ([StorageUnitId]) REFERENCES [StorageUnit] ([StorageUnitId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MaintenanceOrder_SupportTicket_TicketId] FOREIGN KEY ([TicketId]) REFERENCES [SupportTicket] ([TicketId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MaintenanceOrder_User_AssignedTo] FOREIGN KEY ([AssignedTo]) REFERENCES [User] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE TABLE [TicketMessage] (
        [MessageId] int NOT NULL IDENTITY,
        [TicketId] int NOT NULL,
        [SenderId] int NOT NULL,
        [Body] nvarchar(1000) NOT NULL,
        [IsInternalNote] bit NOT NULL,
        [SentAt] datetime2 NOT NULL,
        CONSTRAINT [PK_TicketMessage] PRIMARY KEY ([MessageId]),
        CONSTRAINT [FK_TicketMessage_SupportTicket_TicketId] FOREIGN KEY ([TicketId]) REFERENCES [SupportTicket] ([TicketId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TicketMessage_User_SenderId] FOREIGN KEY ([SenderId]) REFERENCES [User] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE TABLE [AccessCode] (
        [AccessCodeId] int NOT NULL IDENTITY,
        [ContractId] int NOT NULL,
        [CodeHash] varchar(255) NOT NULL,
        [IssuedAt] datetime2 NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [IsActive] bit NOT NULL,
        [SuspendedAt] datetime2 NULL,
        CONSTRAINT [PK_AccessCode] PRIMARY KEY ([AccessCodeId]),
        CONSTRAINT [FK_AccessCode_Contract_ContractId] FOREIGN KEY ([ContractId]) REFERENCES [Contract] ([ContractId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE TABLE [Handover] (
        [HandoverId] int NOT NULL IDENTITY,
        [ReservationId] int NOT NULL,
        [ContractId] int NULL,
        [StorageUnitId] int NOT NULL,
        [StaffId] int NOT NULL,
        [HandoverTime] datetime2 NOT NULL,
        [LockSerial] varchar(30) NULL,
        [CardNumber] varchar(30) NULL,
        [ConditionNote] nvarchar(300) NULL,
        [Status] varchar(20) NOT NULL,
        CONSTRAINT [PK_Handover] PRIMARY KEY ([HandoverId]),
        CONSTRAINT [FK_Handover_Contract_ContractId] FOREIGN KEY ([ContractId]) REFERENCES [Contract] ([ContractId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Handover_Reservation_ReservationId] FOREIGN KEY ([ReservationId]) REFERENCES [Reservation] ([ReservationId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Handover_StorageUnit_StorageUnitId] FOREIGN KEY ([StorageUnitId]) REFERENCES [StorageUnit] ([StorageUnitId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Handover_User_StaffId] FOREIGN KEY ([StaffId]) REFERENCES [User] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE TABLE [Invoice] (
        [InvoiceId] int NOT NULL IDENTITY,
        [ContractId] int NULL,
        [ReservationId] int NULL,
        [CustomerId] int NOT NULL,
        [InvoiceNumber] varchar(20) NOT NULL,
        [IssueDate] date NOT NULL,
        [DueDate] date NOT NULL,
        [TotalAmount] decimal(12,2) NOT NULL,
        [PaidAmount] decimal(12,2) NOT NULL,
        [Status] varchar(20) NOT NULL,
        CONSTRAINT [PK_Invoice] PRIMARY KEY ([InvoiceId]),
        CONSTRAINT [FK_Invoice_Contract_ContractId] FOREIGN KEY ([ContractId]) REFERENCES [Contract] ([ContractId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Invoice_Reservation_ReservationId] FOREIGN KEY ([ReservationId]) REFERENCES [Reservation] ([ReservationId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Invoice_User_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [User] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE TABLE [MoveOutRequest] (
        [MoveOutRequestId] int NOT NULL IDENTITY,
        [ContractId] int NOT NULL,
        [RequestedAt] datetime2 NOT NULL,
        [PlannedMoveOutDate] date NOT NULL,
        [Reason] nvarchar(200) NULL,
        [InspectionSlot] datetime2 NULL,
        [RefundMethod] varchar(20) NULL,
        [RefundAccount] varchar(50) NULL,
        [Status] varchar(20) NOT NULL,
        CONSTRAINT [PK_MoveOutRequest] PRIMARY KEY ([MoveOutRequestId]),
        CONSTRAINT [FK_MoveOutRequest_Contract_ContractId] FOREIGN KEY ([ContractId]) REFERENCES [Contract] ([ContractId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE TABLE [Renewal] (
        [RenewalId] int NOT NULL IDENTITY,
        [ContractId] int NOT NULL,
        [ApprovedBy] int NULL,
        [RequestedAt] datetime2 NOT NULL,
        [ExtendMonths] int NOT NULL,
        [NewEndDate] date NOT NULL,
        [NewMonthlyRent] decimal(12,2) NOT NULL,
        [Status] varchar(20) NOT NULL,
        CONSTRAINT [PK_Renewal] PRIMARY KEY ([RenewalId]),
        CONSTRAINT [FK_Renewal_Contract_ContractId] FOREIGN KEY ([ContractId]) REFERENCES [Contract] ([ContractId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Renewal_User_ApprovedBy] FOREIGN KEY ([ApprovedBy]) REFERENCES [User] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE TABLE [AccessLog] (
        [AccessLogId] bigint NOT NULL IDENTITY,
        [AccessCodeId] int NOT NULL,
        [StorageUnitId] int NOT NULL,
        [AccessTime] datetime2 NOT NULL,
        [Result] varchar(20) NOT NULL,
        [DeviceId] varchar(30) NULL,
        CONSTRAINT [PK_AccessLog] PRIMARY KEY ([AccessLogId]),
        CONSTRAINT [FK_AccessLog_AccessCode_AccessCodeId] FOREIGN KEY ([AccessCodeId]) REFERENCES [AccessCode] ([AccessCodeId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AccessLog_StorageUnit_StorageUnitId] FOREIGN KEY ([StorageUnitId]) REFERENCES [StorageUnit] ([StorageUnitId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE TABLE [InvoiceItem] (
        [InvoiceItemId] int NOT NULL IDENTITY,
        [InvoiceId] int NOT NULL,
        [FeeTypeId] int NULL,
        [Description] nvarchar(200) NOT NULL,
        [Quantity] int NOT NULL,
        [UnitAmount] decimal(12,2) NOT NULL,
        [LineAmount] decimal(12,2) NOT NULL,
        CONSTRAINT [PK_InvoiceItem] PRIMARY KEY ([InvoiceItemId]),
        CONSTRAINT [FK_InvoiceItem_FeeType_FeeTypeId] FOREIGN KEY ([FeeTypeId]) REFERENCES [FeeType] ([FeeTypeId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InvoiceItem_Invoice_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoice] ([InvoiceId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE TABLE [OverdueCase] (
        [OverdueCaseId] int NOT NULL IDENTITY,
        [ContractId] int NOT NULL,
        [InvoiceId] int NULL,
        [OverdueSince] date NOT NULL,
        [DaysOverdue] int NOT NULL,
        [LateFeeAmount] decimal(12,2) NOT NULL,
        [Stage] varchar(30) NOT NULL,
        [AccessSuspendedAt] datetime2 NULL,
        [ResolvedAt] datetime2 NULL,
        CONSTRAINT [PK_OverdueCase] PRIMARY KEY ([OverdueCaseId]),
        CONSTRAINT [FK_OverdueCase_Contract_ContractId] FOREIGN KEY ([ContractId]) REFERENCES [Contract] ([ContractId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OverdueCase_Invoice_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoice] ([InvoiceId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE TABLE [Payment] (
        [PaymentId] int NOT NULL IDENTITY,
        [InvoiceId] int NOT NULL,
        [ReceivedBy] int NULL,
        [PaymentCode] varchar(20) NOT NULL,
        [Method] varchar(20) NOT NULL,
        [Amount] decimal(12,2) NOT NULL,
        [PaidAt] datetime2 NULL,
        [ReferenceNo] varchar(50) NULL,
        [Status] varchar(20) NOT NULL,
        CONSTRAINT [PK_Payment] PRIMARY KEY ([PaymentId]),
        CONSTRAINT [FK_Payment_Invoice_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoice] ([InvoiceId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Payment_User_ReceivedBy] FOREIGN KEY ([ReceivedBy]) REFERENCES [User] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE TABLE [Reminder] (
        [ReminderId] int NOT NULL IDENTITY,
        [InvoiceId] int NOT NULL,
        [Channel] varchar(20) NOT NULL,
        [Template] varchar(50) NOT NULL,
        [SentAt] datetime2 NULL,
        [Status] varchar(20) NOT NULL,
        CONSTRAINT [PK_Reminder] PRIMARY KEY ([ReminderId]),
        CONSTRAINT [FK_Reminder_Invoice_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoice] ([InvoiceId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE TABLE [ReturnInspection] (
        [ReturnInspectionId] int NOT NULL IDENTITY,
        [MoveOutRequestId] int NOT NULL,
        [StorageUnitId] int NOT NULL,
        [StaffId] int NOT NULL,
        [InspectedAt] datetime2 NOT NULL,
        [ConditionResult] varchar(20) NOT NULL,
        [CleaningFee] decimal(12,2) NOT NULL,
        [RepairCharge] decimal(12,2) NOT NULL,
        [OtherDeduction] decimal(12,2) NOT NULL,
        [RefundAmount] decimal(12,2) NOT NULL,
        [Status] varchar(20) NOT NULL,
        CONSTRAINT [PK_ReturnInspection] PRIMARY KEY ([ReturnInspectionId]),
        CONSTRAINT [FK_ReturnInspection_MoveOutRequest_MoveOutRequestId] FOREIGN KEY ([MoveOutRequestId]) REFERENCES [MoveOutRequest] ([MoveOutRequestId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ReturnInspection_StorageUnit_StorageUnitId] FOREIGN KEY ([StorageUnitId]) REFERENCES [StorageUnit] ([StorageUnitId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ReturnInspection_User_StaffId] FOREIGN KEY ([StaffId]) REFERENCES [User] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_AccessCode_ContractId] ON [AccessCode] ([ContractId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_AccessLog_AccessCodeId] ON [AccessLog] ([AccessCodeId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_AccessLog_StorageUnitId] ON [AccessLog] ([StorageUnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ActivityLog_FacilityId] ON [ActivityLog] ([FacilityId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ActivityLog_Result] ON [ActivityLog] ([Result]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ActivityLog_UserId] ON [ActivityLog] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Contract_ContractNumber] ON [Contract] ([ContractNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Contract_CustomerId] ON [Contract] ([CustomerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Contract_ReservationId] ON [Contract] ([ReservationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Contract_Status] ON [Contract] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Contract_StorageUnitId] ON [Contract] ([StorageUnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Facility_ManagerId] ON [Facility] ([ManagerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Facility_Status] ON [Facility] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_FeeType_PricingPolicyId] ON [FeeType] ([PricingPolicyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Floor_FacilityId] ON [Floor] ([FacilityId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Handover_ContractId] ON [Handover] ([ContractId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Handover_ReservationId] ON [Handover] ([ReservationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Handover_StaffId] ON [Handover] ([StaffId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Handover_Status] ON [Handover] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Handover_StorageUnitId] ON [Handover] ([StorageUnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Invoice_ContractId] ON [Invoice] ([ContractId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Invoice_CustomerId] ON [Invoice] ([CustomerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Invoice_DueDate] ON [Invoice] ([DueDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Invoice_InvoiceNumber] ON [Invoice] ([InvoiceNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Invoice_ReservationId] ON [Invoice] ([ReservationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Invoice_Status] ON [Invoice] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_InvoiceItem_FeeTypeId] ON [InvoiceItem] ([FeeTypeId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_InvoiceItem_InvoiceId] ON [InvoiceItem] ([InvoiceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_MaintenanceOrder_AssignedTo] ON [MaintenanceOrder] ([AssignedTo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_MaintenanceOrder_Status] ON [MaintenanceOrder] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_MaintenanceOrder_StorageUnitId] ON [MaintenanceOrder] ([StorageUnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_MaintenanceOrder_TicketId] ON [MaintenanceOrder] ([TicketId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_MoveOutRequest_ContractId] ON [MoveOutRequest] ([ContractId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_MoveOutRequest_Status] ON [MoveOutRequest] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_OverdueCase_ContractId] ON [OverdueCase] ([ContractId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_OverdueCase_InvoiceId] ON [OverdueCase] ([InvoiceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_OverdueCase_Stage] ON [OverdueCase] ([Stage]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Payment_InvoiceId] ON [Payment] ([InvoiceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Payment_PaymentCode] ON [Payment] ([PaymentCode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Payment_ReceivedBy] ON [Payment] ([ReceivedBy]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Payment_Status] ON [Payment] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_PricingPolicy_CreatedBy] ON [PricingPolicy] ([CreatedBy]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_PricingPolicy_Status] ON [PricingPolicy] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Reminder_InvoiceId] ON [Reminder] ([InvoiceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Reminder_Status] ON [Reminder] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Renewal_ApprovedBy] ON [Renewal] ([ApprovedBy]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Renewal_ContractId] ON [Renewal] ([ContractId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Reservation_CreatedBy] ON [Reservation] ([CreatedBy]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Reservation_CustomerId] ON [Reservation] ([CustomerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Reservation_FacilityId] ON [Reservation] ([FacilityId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Reservation_MoveInDate] ON [Reservation] ([MoveInDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Reservation_ReservationCode] ON [Reservation] ([ReservationCode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Reservation_Status] ON [Reservation] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Reservation_StorageUnitId] ON [Reservation] ([StorageUnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Reservation_UnitTypeId] ON [Reservation] ([UnitTypeId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ReturnInspection_MoveOutRequestId] ON [ReturnInspection] ([MoveOutRequestId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ReturnInspection_StaffId] ON [ReturnInspection] ([StaffId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ReturnInspection_Status] ON [ReturnInspection] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ReturnInspection_StorageUnitId] ON [ReturnInspection] ([StorageUnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Shift_FacilityId] ON [Shift] ([FacilityId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Shift_UserId] ON [Shift] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_StaffFacility_FacilityId] ON [StaffFacility] ([FacilityId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_StaffFacility_UserId] ON [StaffFacility] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_StaffTask_AssignedTo] ON [StaffTask] ([AssignedTo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_StaffTask_ShiftId] ON [StaffTask] ([ShiftId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_StaffTask_Status] ON [StaffTask] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StorageUnit_FacilityId_UnitCode] ON [StorageUnit] ([FacilityId], [UnitCode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_StorageUnit_FloorId] ON [StorageUnit] ([FloorId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_StorageUnit_Status] ON [StorageUnit] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_StorageUnit_UnitCode] ON [StorageUnit] ([UnitCode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_StorageUnit_UnitTypeId] ON [StorageUnit] ([UnitTypeId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_SupportTicket_AssignedTo] ON [SupportTicket] ([AssignedTo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_SupportTicket_CategoryId] ON [SupportTicket] ([CategoryId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_SupportTicket_CustomerId] ON [SupportTicket] ([CustomerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_SupportTicket_FacilityId] ON [SupportTicket] ([FacilityId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_SupportTicket_Status] ON [SupportTicket] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_SupportTicket_StorageUnitId] ON [SupportTicket] ([StorageUnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SupportTicket_TicketCode] ON [SupportTicket] ([TicketCode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_TicketMessage_SenderId] ON [TicketMessage] ([SenderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_TicketMessage_TicketId] ON [TicketMessage] ([TicketId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_UnitPrice_PricingPolicyId] ON [UnitPrice] ([PricingPolicyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_UnitPrice_UnitTypeId] ON [UnitPrice] ([UnitTypeId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_User_Email] ON [User] ([Email]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_User_RoleId] ON [User] ([RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_User_Status] ON [User] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009134519_InitialSchema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261009134519_InitialSchema', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011191150_OneOpenContractPerUnit'
)
BEGIN

    UPDATE [Contract] SET [Status] = 'Terminated'
    WHERE [ContractId] IN (
        SELECT [ContractId] FROM (
            SELECT [ContractId],
                   ROW_NUMBER() OVER (
                       PARTITION BY [StorageUnitId]
                       ORDER BY CASE WHEN [Status] = 'Active' THEN 0 ELSE 1 END, [ContractId] DESC) AS rn
            FROM [Contract]
            WHERE [Status] IN ('Active', 'Draft')
        ) ranked
        WHERE rn > 1)
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011191150_OneOpenContractPerUnit'
)
BEGIN
    DROP INDEX [IX_Contract_StorageUnitId] ON [Contract];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011191150_OneOpenContractPerUnit'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Contract_StorageUnitId] ON [Contract] ([StorageUnitId]) WHERE [Status] IN (''Active'', ''Draft'')');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011191150_OneOpenContractPerUnit'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261011191150_OneOpenContractPerUnit', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011191352_AddDueDateToSupportTicket'
)
BEGIN
    ALTER TABLE [SupportTicket] ADD [DueDate] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011191352_AddDueDateToSupportTicket'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261011191352_AddDueDateToSupportTicket', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011191413_AddWorkNoteToMaintenanceOrder'
)
BEGIN
    ALTER TABLE [MaintenanceOrder] ADD [WorkNote] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011191413_AddWorkNoteToMaintenanceOrder'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261011191413_AddWorkNoteToMaintenanceOrder', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011191427_AddUniqueStaffFacilityIndex'
)
BEGIN

    DELETE FROM [StaffFacility]
    WHERE [StaffFacilityId] NOT IN (
        SELECT MIN([StaffFacilityId]) FROM [StaffFacility] GROUP BY [UserId], [FacilityId])
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011191427_AddUniqueStaffFacilityIndex'
)
BEGIN
    DROP INDEX [IX_StaffFacility_UserId] ON [StaffFacility];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011191427_AddUniqueStaffFacilityIndex'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StaffFacility_UserId_FacilityId] ON [StaffFacility] ([UserId], [FacilityId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011191427_AddUniqueStaffFacilityIndex'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261011191427_AddUniqueStaffFacilityIndex', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011191434_AddShiftDateIndexes'
)
BEGIN
    DROP INDEX [IX_Shift_FacilityId] ON [Shift];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011191434_AddShiftDateIndexes'
)
BEGIN
    DROP INDEX [IX_Shift_UserId] ON [Shift];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011191434_AddShiftDateIndexes'
)
BEGIN
    CREATE INDEX [IX_Shift_FacilityId_ShiftDate] ON [Shift] ([FacilityId], [ShiftDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011191434_AddShiftDateIndexes'
)
BEGIN
    CREATE INDEX [IX_Shift_UserId_ShiftDate] ON [Shift] ([UserId], [ShiftDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011191434_AddShiftDateIndexes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261011191434_AddShiftDateIndexes', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011191445_AddNoteToStaffTask'
)
BEGIN
    ALTER TABLE [StaffTask] ADD [Note] nvarchar(200) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011191445_AddNoteToStaffTask'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261011191445_AddNoteToStaffTask', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014112231_AddHandoverPhotos'
)
BEGIN
    CREATE TABLE [HandoverPhoto] (
        [HandoverPhotoId] int NOT NULL IDENTITY,
        [HandoverId] int NOT NULL,
        [UploadedBy] int NOT NULL,
        [FileName] nvarchar(200) NOT NULL,
        [ContentType] varchar(100) NOT NULL,
        [StoredName] varchar(100) NOT NULL,
        [SizeBytes] int NOT NULL,
        [UploadedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_HandoverPhoto] PRIMARY KEY ([HandoverPhotoId]),
        CONSTRAINT [FK_HandoverPhoto_Handover_HandoverId] FOREIGN KEY ([HandoverId]) REFERENCES [Handover] ([HandoverId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_HandoverPhoto_User_UploadedBy] FOREIGN KEY ([UploadedBy]) REFERENCES [User] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014112231_AddHandoverPhotos'
)
BEGIN
    CREATE INDEX [IX_HandoverPhoto_HandoverId] ON [HandoverPhoto] ([HandoverId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014112231_AddHandoverPhotos'
)
BEGIN
    CREATE INDEX [IX_HandoverPhoto_UploadedBy] ON [HandoverPhoto] ([UploadedBy]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014112231_AddHandoverPhotos'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261014112231_AddHandoverPhotos', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014112241_AddNotificationPreferences'
)
BEGIN
    CREATE TABLE [NotificationPreference] (
        [NotificationPreferenceId] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [EmailReminders] bit NOT NULL,
        [SmsReminders] bit NOT NULL,
        [MarketingEmails] bit NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_NotificationPreference] PRIMARY KEY ([NotificationPreferenceId]),
        CONSTRAINT [FK_NotificationPreference_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [User] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014112241_AddNotificationPreferences'
)
BEGIN
    CREATE UNIQUE INDEX [IX_NotificationPreference_UserId] ON [NotificationPreference] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014112241_AddNotificationPreferences'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261014112241_AddNotificationPreferences', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015090000_MergeDisabledUserStatus'
)
BEGIN
    UPDATE [User] SET [Status] = 'Disabled' WHERE [Status] IN ('Suspended', 'Inactive');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015090000_MergeDisabledUserStatus'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261015090000_MergeDisabledUserStatus', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018044919_RenameHandoverToCheckIn'
)
BEGIN
    EXEC sp_rename N'[Handover].[IX_Handover_ContractId]', N'IX_CheckIn_ContractId', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018044919_RenameHandoverToCheckIn'
)
BEGIN
    EXEC sp_rename N'[Handover].[IX_Handover_ReservationId]', N'IX_CheckIn_ReservationId', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018044919_RenameHandoverToCheckIn'
)
BEGIN
    EXEC sp_rename N'[Handover].[IX_Handover_StaffId]', N'IX_CheckIn_StaffId', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018044919_RenameHandoverToCheckIn'
)
BEGIN
    EXEC sp_rename N'[Handover].[IX_Handover_Status]', N'IX_CheckIn_Status', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018044919_RenameHandoverToCheckIn'
)
BEGIN
    EXEC sp_rename N'[Handover].[IX_Handover_StorageUnitId]', N'IX_CheckIn_StorageUnitId', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018044919_RenameHandoverToCheckIn'
)
BEGIN
    EXEC sp_rename N'[HandoverPhoto].[IX_HandoverPhoto_HandoverId]', N'IX_CheckInPhoto_CheckInId', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018044919_RenameHandoverToCheckIn'
)
BEGIN
    EXEC sp_rename N'[HandoverPhoto].[IX_HandoverPhoto_UploadedBy]', N'IX_CheckInPhoto_UploadedBy', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018044919_RenameHandoverToCheckIn'
)
BEGIN
    EXEC sp_rename N'[FK_Handover_Contract_ContractId]', N'FK_CheckIn_Contract_ContractId', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018044919_RenameHandoverToCheckIn'
)
BEGIN
    EXEC sp_rename N'[FK_Handover_Reservation_ReservationId]', N'FK_CheckIn_Reservation_ReservationId', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018044919_RenameHandoverToCheckIn'
)
BEGIN
    EXEC sp_rename N'[FK_Handover_StorageUnit_StorageUnitId]', N'FK_CheckIn_StorageUnit_StorageUnitId', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018044919_RenameHandoverToCheckIn'
)
BEGIN
    EXEC sp_rename N'[FK_Handover_User_StaffId]', N'FK_CheckIn_User_StaffId', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018044919_RenameHandoverToCheckIn'
)
BEGIN
    EXEC sp_rename N'[FK_HandoverPhoto_User_UploadedBy]', N'FK_CheckInPhoto_User_UploadedBy', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018044919_RenameHandoverToCheckIn'
)
BEGIN
    EXEC sp_rename N'[FK_HandoverPhoto_Handover_HandoverId]', N'FK_CheckInPhoto_CheckIn_CheckInId', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018044919_RenameHandoverToCheckIn'
)
BEGIN
    EXEC sp_rename N'[PK_Handover]', N'PK_CheckIn', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018044919_RenameHandoverToCheckIn'
)
BEGIN
    EXEC sp_rename N'[PK_HandoverPhoto]', N'PK_CheckInPhoto', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018044919_RenameHandoverToCheckIn'
)
BEGIN
    EXEC sp_rename N'[HandoverPhoto].[HandoverId]', N'CheckInId', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018044919_RenameHandoverToCheckIn'
)
BEGIN
    EXEC sp_rename N'[HandoverPhoto].[HandoverPhotoId]', N'CheckInPhotoId', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018044919_RenameHandoverToCheckIn'
)
BEGIN
    EXEC sp_rename N'[Handover].[HandoverId]', N'CheckInId', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018044919_RenameHandoverToCheckIn'
)
BEGIN
    EXEC sp_rename N'[Handover].[HandoverTime]', N'CheckInTime', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018044919_RenameHandoverToCheckIn'
)
BEGIN
    EXEC sp_rename N'[HandoverPhoto]', N'CheckInPhoto';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018044919_RenameHandoverToCheckIn'
)
BEGIN
    EXEC sp_rename N'[Handover]', N'CheckIn';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018044919_RenameHandoverToCheckIn'
)
BEGIN
    UPDATE [StaffTask] SET [TaskType] = 'CheckIn' WHERE [TaskType] = 'Handover';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018044919_RenameHandoverToCheckIn'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261018044919_RenameHandoverToCheckIn', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018045522_RenameMoveOutToHandover'
)
BEGIN
    EXEC sp_rename N'[MoveOutRequest].[IX_MoveOutRequest_ContractId]', N'IX_Handover_ContractId', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018045522_RenameMoveOutToHandover'
)
BEGIN
    EXEC sp_rename N'[MoveOutRequest].[IX_MoveOutRequest_Status]', N'IX_Handover_Status', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018045522_RenameMoveOutToHandover'
)
BEGIN
    EXEC sp_rename N'[ReturnInspection].[IX_ReturnInspection_MoveOutRequestId]', N'IX_HandoverInspection_HandoverId', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018045522_RenameMoveOutToHandover'
)
BEGIN
    EXEC sp_rename N'[ReturnInspection].[IX_ReturnInspection_StaffId]', N'IX_HandoverInspection_StaffId', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018045522_RenameMoveOutToHandover'
)
BEGIN
    EXEC sp_rename N'[ReturnInspection].[IX_ReturnInspection_Status]', N'IX_HandoverInspection_Status', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018045522_RenameMoveOutToHandover'
)
BEGIN
    EXEC sp_rename N'[ReturnInspection].[IX_ReturnInspection_StorageUnitId]', N'IX_HandoverInspection_StorageUnitId', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018045522_RenameMoveOutToHandover'
)
BEGIN
    EXEC sp_rename N'[FK_MoveOutRequest_Contract_ContractId]', N'FK_Handover_Contract_ContractId', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018045522_RenameMoveOutToHandover'
)
BEGIN
    EXEC sp_rename N'[FK_ReturnInspection_MoveOutRequest_MoveOutRequestId]', N'FK_HandoverInspection_Handover_HandoverId', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018045522_RenameMoveOutToHandover'
)
BEGIN
    EXEC sp_rename N'[FK_ReturnInspection_StorageUnit_StorageUnitId]', N'FK_HandoverInspection_StorageUnit_StorageUnitId', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018045522_RenameMoveOutToHandover'
)
BEGIN
    EXEC sp_rename N'[FK_ReturnInspection_User_StaffId]', N'FK_HandoverInspection_User_StaffId', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018045522_RenameMoveOutToHandover'
)
BEGIN
    EXEC sp_rename N'[PK_MoveOutRequest]', N'PK_Handover', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018045522_RenameMoveOutToHandover'
)
BEGIN
    EXEC sp_rename N'[PK_ReturnInspection]', N'PK_HandoverInspection', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018045522_RenameMoveOutToHandover'
)
BEGIN
    EXEC sp_rename N'[ReturnInspection].[MoveOutRequestId]', N'HandoverId', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018045522_RenameMoveOutToHandover'
)
BEGIN
    EXEC sp_rename N'[ReturnInspection].[ReturnInspectionId]', N'HandoverInspectionId', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018045522_RenameMoveOutToHandover'
)
BEGIN
    EXEC sp_rename N'[MoveOutRequest].[MoveOutRequestId]', N'HandoverId', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018045522_RenameMoveOutToHandover'
)
BEGIN
    EXEC sp_rename N'[MoveOutRequest].[PlannedMoveOutDate]', N'PlannedHandoverDate', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018045522_RenameMoveOutToHandover'
)
BEGIN
    EXEC sp_rename N'[ReturnInspection]', N'HandoverInspection';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018045522_RenameMoveOutToHandover'
)
BEGIN
    EXEC sp_rename N'[MoveOutRequest]', N'Handover';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018045522_RenameMoveOutToHandover'
)
BEGIN
    UPDATE [Contract] SET [Status] = 'PendingHandover' WHERE [Status] = 'PendingMoveOut';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018045522_RenameMoveOutToHandover'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261018045522_RenameMoveOutToHandover', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018050015_AddEarlyTerminationPercentToPricingPolicy'
)
BEGIN
    ALTER TABLE [PricingPolicy] ADD [EarlyTerminationPercent] decimal(5,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018050015_AddEarlyTerminationPercentToPricingPolicy'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261018050015_AddEarlyTerminationPercentToPricingPolicy', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018050401_AddUnitVisit'
)
BEGIN
    CREATE TABLE [UnitVisit] (
        [UnitVisitId] int NOT NULL IDENTITY,
        [ContractId] int NOT NULL,
        [StorageUnitId] int NOT NULL,
        [RecordedBy] int NOT NULL,
        [CheckInAt] datetime2 NOT NULL,
        [CheckOutAt] datetime2 NULL,
        [Note] nvarchar(200) NULL,
        CONSTRAINT [PK_UnitVisit] PRIMARY KEY ([UnitVisitId]),
        CONSTRAINT [FK_UnitVisit_Contract_ContractId] FOREIGN KEY ([ContractId]) REFERENCES [Contract] ([ContractId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UnitVisit_StorageUnit_StorageUnitId] FOREIGN KEY ([StorageUnitId]) REFERENCES [StorageUnit] ([StorageUnitId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UnitVisit_User_RecordedBy] FOREIGN KEY ([RecordedBy]) REFERENCES [User] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018050401_AddUnitVisit'
)
BEGIN
    CREATE INDEX [IX_UnitVisit_CheckInAt] ON [UnitVisit] ([CheckInAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018050401_AddUnitVisit'
)
BEGIN
    CREATE INDEX [IX_UnitVisit_ContractId] ON [UnitVisit] ([ContractId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018050401_AddUnitVisit'
)
BEGIN
    CREATE INDEX [IX_UnitVisit_RecordedBy] ON [UnitVisit] ([RecordedBy]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018050401_AddUnitVisit'
)
BEGIN
    CREATE INDEX [IX_UnitVisit_StorageUnitId] ON [UnitVisit] ([StorageUnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018050401_AddUnitVisit'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261018050401_AddUnitVisit', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022030652_AddDepositRefund'
)
BEGIN
    CREATE TABLE [DepositRefund] (
        [DepositRefundId] int NOT NULL IDENTITY,
        [CustomerId] int NOT NULL,
        [FacilityId] int NOT NULL,
        [ReservationId] int NOT NULL,
        [HandoverId] int NULL,
        [Source] varchar(20) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Method] varchar(20) NULL,
        [RefundAccount] nvarchar(100) NULL,
        [Status] varchar(20) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [PaidAt] datetime2 NULL,
        [PaidBy] int NULL,
        [ReferenceNo] nvarchar(50) NULL,
        CONSTRAINT [PK_DepositRefund] PRIMARY KEY ([DepositRefundId]),
        CONSTRAINT [FK_DepositRefund_Facility_FacilityId] FOREIGN KEY ([FacilityId]) REFERENCES [Facility] ([FacilityId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_DepositRefund_Handover_HandoverId] FOREIGN KEY ([HandoverId]) REFERENCES [Handover] ([HandoverId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_DepositRefund_Reservation_ReservationId] FOREIGN KEY ([ReservationId]) REFERENCES [Reservation] ([ReservationId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_DepositRefund_User_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [User] ([UserId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_DepositRefund_User_PaidBy] FOREIGN KEY ([PaidBy]) REFERENCES [User] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022030652_AddDepositRefund'
)
BEGIN
    CREATE INDEX [IX_DepositRefund_CustomerId] ON [DepositRefund] ([CustomerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022030652_AddDepositRefund'
)
BEGIN
    CREATE INDEX [IX_DepositRefund_FacilityId] ON [DepositRefund] ([FacilityId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022030652_AddDepositRefund'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_DepositRefund_HandoverId] ON [DepositRefund] ([HandoverId]) WHERE [HandoverId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022030652_AddDepositRefund'
)
BEGIN
    CREATE INDEX [IX_DepositRefund_PaidBy] ON [DepositRefund] ([PaidBy]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022030652_AddDepositRefund'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_DepositRefund_ReservationId_Source] ON [DepositRefund] ([ReservationId], [Source]) WHERE [Source] = ''Cancellation''');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022030652_AddDepositRefund'
)
BEGIN
    CREATE INDEX [IX_DepositRefund_Status] ON [DepositRefund] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022030652_AddDepositRefund'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261022030652_AddDepositRefund', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022042831_PinPricingPolicyToRentals'
)
BEGIN
    ALTER TABLE [Reservation] ADD [PricingPolicyId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022042831_PinPricingPolicyToRentals'
)
BEGIN
    ALTER TABLE [Contract] ADD [PricingPolicyId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022042831_PinPricingPolicyToRentals'
)
BEGIN
    CREATE INDEX [IX_Reservation_PricingPolicyId] ON [Reservation] ([PricingPolicyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022042831_PinPricingPolicyToRentals'
)
BEGIN
    CREATE INDEX [IX_Contract_PricingPolicyId] ON [Contract] ([PricingPolicyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022042831_PinPricingPolicyToRentals'
)
BEGIN
    ALTER TABLE [Contract] ADD CONSTRAINT [FK_Contract_PricingPolicy_PricingPolicyId] FOREIGN KEY ([PricingPolicyId]) REFERENCES [PricingPolicy] ([PricingPolicyId]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022042831_PinPricingPolicyToRentals'
)
BEGIN
    ALTER TABLE [Reservation] ADD CONSTRAINT [FK_Reservation_PricingPolicy_PricingPolicyId] FOREIGN KEY ([PricingPolicyId]) REFERENCES [PricingPolicy] ([PricingPolicyId]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022042831_PinPricingPolicyToRentals'
)
BEGIN

    UPDATE r SET PricingPolicyId = (
        SELECT TOP 1 p.PricingPolicyId FROM PricingPolicy p
        WHERE p.Status IN ('Active', 'Superseded') AND p.EffectiveFrom <= r.MoveInDate
          AND (p.EffectiveTo IS NULL OR p.EffectiveTo >= r.MoveInDate)
        ORDER BY p.EffectiveFrom DESC)
    FROM Reservation r;

    UPDATE c SET PricingPolicyId = COALESCE(r.PricingPolicyId, (
        SELECT TOP 1 p.PricingPolicyId FROM PricingPolicy p
        WHERE p.Status IN ('Active', 'Superseded') AND p.EffectiveFrom <= c.StartDate
          AND (p.EffectiveTo IS NULL OR p.EffectiveTo >= c.StartDate)
        ORDER BY p.EffectiveFrom DESC))
    FROM Contract c JOIN Reservation r ON r.ReservationId = c.ReservationId;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022042831_PinPricingPolicyToRentals'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261022042831_PinPricingPolicyToRentals', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    DROP INDEX [IX_DepositRefund_HandoverId] ON [DepositRefund];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[Handover].[IX_Handover_ContractId]', N'IX_UnitReturn_ContractId', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[Handover].[IX_Handover_Status]', N'IX_UnitReturn_Status', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[FK_Handover_Contract_ContractId]', N'FK_UnitReturn_Contract_ContractId', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[PK_Handover]', N'PK_UnitReturn', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[Handover].[HandoverId]', N'UnitReturnId', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[Handover].[PlannedHandoverDate]', N'PlannedReturnDate', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[Handover]', N'UnitReturn';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[HandoverInspection].[IX_HandoverInspection_HandoverId]', N'IX_ReturnInspection_UnitReturnId', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[HandoverInspection].[IX_HandoverInspection_StaffId]', N'IX_ReturnInspection_StaffId', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[HandoverInspection].[IX_HandoverInspection_Status]', N'IX_ReturnInspection_Status', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[HandoverInspection].[IX_HandoverInspection_StorageUnitId]', N'IX_ReturnInspection_StorageUnitId', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[FK_HandoverInspection_Handover_HandoverId]', N'FK_ReturnInspection_UnitReturn_UnitReturnId', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[FK_HandoverInspection_StorageUnit_StorageUnitId]', N'FK_ReturnInspection_StorageUnit_StorageUnitId', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[FK_HandoverInspection_User_StaffId]', N'FK_ReturnInspection_User_StaffId', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[PK_HandoverInspection]', N'PK_ReturnInspection', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[HandoverInspection].[HandoverInspectionId]', N'ReturnInspectionId', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[HandoverInspection].[HandoverId]', N'UnitReturnId', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[HandoverInspection]', N'ReturnInspection';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[FK_DepositRefund_Handover_HandoverId]', N'FK_DepositRefund_UnitReturn_UnitReturnId', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[DepositRefund].[HandoverId]', N'UnitReturnId', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[CheckIn].[IX_CheckIn_ContractId]', N'IX_Handover_ContractId', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[CheckIn].[IX_CheckIn_ReservationId]', N'IX_Handover_ReservationId', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[CheckIn].[IX_CheckIn_StaffId]', N'IX_Handover_StaffId', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[CheckIn].[IX_CheckIn_Status]', N'IX_Handover_Status', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[CheckIn].[IX_CheckIn_StorageUnitId]', N'IX_Handover_StorageUnitId', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[FK_CheckIn_Contract_ContractId]', N'FK_Handover_Contract_ContractId', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[FK_CheckIn_Reservation_ReservationId]', N'FK_Handover_Reservation_ReservationId', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[FK_CheckIn_StorageUnit_StorageUnitId]', N'FK_Handover_StorageUnit_StorageUnitId', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[FK_CheckIn_User_StaffId]', N'FK_Handover_User_StaffId', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[PK_CheckIn]', N'PK_Handover', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[CheckIn].[CheckInId]', N'HandoverId', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[CheckIn].[CheckInTime]', N'HandoverTime', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[CheckIn]', N'Handover';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[CheckInPhoto].[IX_CheckInPhoto_CheckInId]', N'IX_HandoverPhoto_HandoverId', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[CheckInPhoto].[IX_CheckInPhoto_UploadedBy]', N'IX_HandoverPhoto_UploadedBy', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[FK_CheckInPhoto_CheckIn_CheckInId]', N'FK_HandoverPhoto_Handover_HandoverId', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[FK_CheckInPhoto_User_UploadedBy]', N'FK_HandoverPhoto_User_UploadedBy', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[PK_CheckInPhoto]', N'PK_HandoverPhoto', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[CheckInPhoto].[CheckInPhotoId]', N'HandoverPhotoId', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[CheckInPhoto].[CheckInId]', N'HandoverId', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[CheckInPhoto]', N'HandoverPhoto';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[Reservation].[CheckInSlot]', N'HandoverSlot', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[UnitVisit].[IX_UnitVisit_CheckInAt]', N'IX_CheckIn_CheckInAt', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[UnitVisit].[IX_UnitVisit_ContractId]', N'IX_CheckIn_ContractId', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[UnitVisit].[IX_UnitVisit_RecordedBy]', N'IX_CheckIn_RecordedBy', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[UnitVisit].[IX_UnitVisit_StorageUnitId]', N'IX_CheckIn_StorageUnitId', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[FK_UnitVisit_Contract_ContractId]', N'FK_CheckIn_Contract_ContractId', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[FK_UnitVisit_StorageUnit_StorageUnitId]', N'FK_CheckIn_StorageUnit_StorageUnitId', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[FK_UnitVisit_User_RecordedBy]', N'FK_CheckIn_User_RecordedBy', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[PK_UnitVisit]', N'PK_CheckIn', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[UnitVisit].[UnitVisitId]', N'CheckInId', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC sp_rename N'[UnitVisit]', N'CheckIn';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_DepositRefund_UnitReturnId] ON [DepositRefund] ([UnitReturnId]) WHERE [UnitReturnId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    UPDATE [Contract] SET [Status] = 'PendingReturn' WHERE [Status] = 'PendingHandover';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    UPDATE [Reservation] SET [Status] = 'HandedOver' WHERE [Status] = 'CheckedIn';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    UPDATE [DepositRefund] SET [Source] = 'Return' WHERE [Source] = 'Handover';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    UPDATE [StaffTask] SET [TaskType] = 'Handover' WHERE [TaskType] = 'CheckIn';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081524_RenameRentalVocabulary'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261022081524_RenameRentalVocabulary', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081832_AddReturnMoveOutDeadline'
)
BEGIN
    ALTER TABLE [UnitReturn] ADD [AccessLockedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081832_AddReturnMoveOutDeadline'
)
BEGIN
    ALTER TABLE [UnitReturn] ADD [MoveOutDeadline] date NOT NULL DEFAULT '0001-01-01';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081832_AddReturnMoveOutDeadline'
)
BEGIN
    UPDATE [UnitReturn] SET [MoveOutDeadline] = DATEADD(day, 15, CAST([RequestedAt] AS date));
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022081832_AddReturnMoveOutDeadline'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261022081832_AddReturnMoveOutDeadline', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022095013_AddFloorStatus'
)
BEGIN
    ALTER TABLE [Floor] ADD [Status] varchar(20) NOT NULL DEFAULT 'Open';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022095013_AddFloorStatus'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261022095013_AddFloorStatus', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025174723_KeepAccessCodeEncrypted'
)
BEGIN
    ALTER TABLE [AccessCode] ADD [CodeCipher] varchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025174723_KeepAccessCodeEncrypted'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261025174723_KeepAccessCodeEncrypted', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025174954_RecordHowVisitsEnd'
)
BEGIN
    ALTER TABLE [CheckIn] ADD [CheckOutMethod] varchar(20) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025174954_RecordHowVisitsEnd'
)
BEGIN
    ALTER TABLE [CheckIn] ADD [CheckOutReason] nvarchar(200) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025174954_RecordHowVisitsEnd'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261025174954_RecordHowVisitsEnd', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026051311_SeedTicketCategories'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [TicketCategory])
    INSERT INTO [TicketCategory] ([Name], [DefaultPriority]) VALUES
        (N'Ra vào / khoá kho', 'High'),
        (N'Hư hỏng / sửa chữa', 'Normal'),
        (N'Vệ sinh / côn trùng', 'Normal'),
        (N'Thanh toán / hoá đơn', 'Normal'),
        (N'Khác', 'Low');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026051311_SeedTicketCategories'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261026051311_SeedTicketCategories', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108150412_AddFacilityUnitPrices'
)
BEGIN
    DROP INDEX [IX_UnitPrice_PricingPolicyId] ON [UnitPrice];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108150412_AddFacilityUnitPrices'
)
BEGIN
    ALTER TABLE [UnitPrice] ADD [FacilityId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108150412_AddFacilityUnitPrices'
)
BEGIN
    CREATE INDEX [IX_UnitPrice_FacilityId] ON [UnitPrice] ([FacilityId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108150412_AddFacilityUnitPrices'
)
BEGIN
    CREATE INDEX [IX_UnitPrice_PricingPolicyId_UnitTypeId_FacilityId] ON [UnitPrice] ([PricingPolicyId], [UnitTypeId], [FacilityId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108150412_AddFacilityUnitPrices'
)
BEGIN
    ALTER TABLE [UnitPrice] ADD CONSTRAINT [FK_UnitPrice_Facility_FacilityId] FOREIGN KEY ([FacilityId]) REFERENCES [Facility] ([FacilityId]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108150412_AddFacilityUnitPrices'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261108150412_AddFacilityUnitPrices', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108153948_FacilityPriceVersions'
)
BEGIN
    ALTER TABLE [Reservation] ADD [FacilityPriceVersionId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108153948_FacilityPriceVersions'
)
BEGIN
    CREATE TABLE [FacilityPriceVersion] (
        [FacilityPriceVersionId] int NOT NULL IDENTITY,
        [FacilityId] int NOT NULL,
        [Version] int NOT NULL,
        [EffectiveFrom] date NOT NULL,
        [EffectiveTo] date NULL,
        [Status] varchar(20) NOT NULL,
        [CreatedBy] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_FacilityPriceVersion] PRIMARY KEY ([FacilityPriceVersionId]),
        CONSTRAINT [FK_FacilityPriceVersion_Facility_FacilityId] FOREIGN KEY ([FacilityId]) REFERENCES [Facility] ([FacilityId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FacilityPriceVersion_User_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [User] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108153948_FacilityPriceVersions'
)
BEGIN
    CREATE TABLE [FacilityUnitPrice] (
        [FacilityUnitPriceId] int NOT NULL IDENTITY,
        [FacilityPriceVersionId] int NOT NULL,
        [UnitTypeId] int NOT NULL,
        [BasePricePerMonth] decimal(12,2) NOT NULL,
        [Discount3Month] decimal(5,2) NOT NULL,
        [Discount6Month] decimal(5,2) NOT NULL,
        [Discount12Month] decimal(5,2) NOT NULL,
        CONSTRAINT [PK_FacilityUnitPrice] PRIMARY KEY ([FacilityUnitPriceId]),
        CONSTRAINT [FK_FacilityUnitPrice_FacilityPriceVersion_FacilityPriceVersionId] FOREIGN KEY ([FacilityPriceVersionId]) REFERENCES [FacilityPriceVersion] ([FacilityPriceVersionId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FacilityUnitPrice_UnitType_UnitTypeId] FOREIGN KEY ([UnitTypeId]) REFERENCES [UnitType] ([UnitTypeId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108153948_FacilityPriceVersions'
)
BEGIN
    CREATE INDEX [IX_UnitPrice_PricingPolicyId_UnitTypeId] ON [UnitPrice] ([PricingPolicyId], [UnitTypeId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108153948_FacilityPriceVersions'
)
BEGIN
    CREATE INDEX [IX_Reservation_FacilityPriceVersionId] ON [Reservation] ([FacilityPriceVersionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108153948_FacilityPriceVersions'
)
BEGIN
    CREATE INDEX [IX_FacilityPriceVersion_CreatedBy] ON [FacilityPriceVersion] ([CreatedBy]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108153948_FacilityPriceVersions'
)
BEGIN
    CREATE UNIQUE INDEX [IX_FacilityPriceVersion_FacilityId_Version] ON [FacilityPriceVersion] ([FacilityId], [Version]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108153948_FacilityPriceVersions'
)
BEGIN
    CREATE UNIQUE INDEX [IX_FacilityUnitPrice_FacilityPriceVersionId_UnitTypeId] ON [FacilityUnitPrice] ([FacilityPriceVersionId], [UnitTypeId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108153948_FacilityPriceVersions'
)
BEGIN
    CREATE INDEX [IX_FacilityUnitPrice_UnitTypeId] ON [FacilityUnitPrice] ([UnitTypeId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108153948_FacilityPriceVersions'
)
BEGIN
    ALTER TABLE [Reservation] ADD CONSTRAINT [FK_Reservation_FacilityPriceVersion_FacilityPriceVersionId] FOREIGN KEY ([FacilityPriceVersionId]) REFERENCES [FacilityPriceVersion] ([FacilityPriceVersionId]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108153948_FacilityPriceVersions'
)
BEGIN

    INSERT INTO FacilityPriceVersion (FacilityId, Version, EffectiveFrom, EffectiveTo, Status, CreatedBy, CreatedAt)
    SELECT up.FacilityId, 1, CAST(GETDATE() AS date), NULL, 'Active', MIN(p.CreatedBy), SYSUTCDATETIME()
    FROM UnitPrice up JOIN PricingPolicy p ON p.PricingPolicyId = up.PricingPolicyId
    WHERE up.FacilityId IS NOT NULL AND p.Status = 'Active'
    GROUP BY up.FacilityId;

    INSERT INTO FacilityUnitPrice (FacilityPriceVersionId, UnitTypeId, BasePricePerMonth, Discount3Month, Discount6Month, Discount12Month)
    SELECT v.FacilityPriceVersionId, up.UnitTypeId, up.BasePricePerMonth, up.Discount3Month, up.Discount6Month, up.Discount12Month
    FROM UnitPrice up
    JOIN PricingPolicy p ON p.PricingPolicyId = up.PricingPolicyId
    JOIN FacilityPriceVersion v ON v.FacilityId = up.FacilityId AND v.Version = 1
    WHERE up.FacilityId IS NOT NULL AND p.Status = 'Active';

    DELETE FROM UnitPrice WHERE FacilityId IS NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108153948_FacilityPriceVersions'
)
BEGIN
    ALTER TABLE [UnitPrice] DROP CONSTRAINT [FK_UnitPrice_Facility_FacilityId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108153948_FacilityPriceVersions'
)
BEGIN
    DROP INDEX [IX_UnitPrice_FacilityId] ON [UnitPrice];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108153948_FacilityPriceVersions'
)
BEGIN
    DROP INDEX [IX_UnitPrice_PricingPolicyId_UnitTypeId_FacilityId] ON [UnitPrice];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108153948_FacilityPriceVersions'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[UnitPrice]') AND [c].[name] = N'FacilityId');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [UnitPrice] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [UnitPrice] DROP COLUMN [FacilityId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108153948_FacilityPriceVersions'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261108153948_FacilityPriceVersions', N'8.0.11');
END;
GO

COMMIT;
GO


-- Demo data: roles, the 5 staff accounts (password Demo@123), one facility with its units, sizes, prices and ticket categories.
ALTER TABLE [Facility] NOCHECK CONSTRAINT ALL;
ALTER TABLE [Floor] NOCHECK CONSTRAINT ALL;
ALTER TABLE [PricingPolicy] NOCHECK CONSTRAINT ALL;
ALTER TABLE [Role] NOCHECK CONSTRAINT ALL;
ALTER TABLE [StaffFacility] NOCHECK CONSTRAINT ALL;
ALTER TABLE [StorageUnit] NOCHECK CONSTRAINT ALL;
ALTER TABLE [TicketCategory] NOCHECK CONSTRAINT ALL;
ALTER TABLE [UnitPrice] NOCHECK CONSTRAINT ALL;
ALTER TABLE [UnitType] NOCHECK CONSTRAINT ALL;
ALTER TABLE [User] NOCHECK CONSTRAINT ALL;
GO
IF NOT EXISTS (SELECT 1 FROM [Facility])
BEGIN
SET IDENTITY_INSERT [Facility] ON;
INSERT INTO [Facility] ([FacilityId], [ManagerId], [Code], [Name], [Address], [District], [Phone], [OpeningHours], [Status]) VALUES (1, 3, N'GV', N'StorEase Gò Vấp', N'1 Đường Demo, Phường 10', N'Gò Vấp', N'0281234567', N'06:00-22:00', N'Active');
SET IDENTITY_INSERT [Facility] OFF;
END
GO
IF NOT EXISTS (SELECT 1 FROM [Floor])
BEGIN
SET IDENTITY_INSERT [Floor] ON;
INSERT INTO [Floor] ([FloorId], [FacilityId], [FloorNumber], [Name], [Status]) VALUES (1, 1, 1, N'Tầng 1', N'Open');
SET IDENTITY_INSERT [Floor] OFF;
END
GO
IF NOT EXISTS (SELECT 1 FROM [PricingPolicy])
BEGIN
SET IDENTITY_INSERT [PricingPolicy] ON;
INSERT INTO [PricingPolicy] ([PricingPolicyId], [CreatedBy], [Version], [EffectiveFrom], [EffectiveTo], [DepositMonths], [GraceDays], [NoticeDays], [LateFeePercentPerWeek], [SuspendAfterDays], [ClearOutAfterDays], [CancelFreeHours], [Status], [EarlyTerminationPercent]) VALUES (1, 4, 1, '2026-10-01T00:00:00.0000000', NULL, 1, 5, 30, 2.00, 14, 60, 24, N'Active', 0.00);
SET IDENTITY_INSERT [PricingPolicy] OFF;
END
GO
IF NOT EXISTS (SELECT 1 FROM [Role])
BEGIN
SET IDENTITY_INSERT [Role] ON;
INSERT INTO [Role] ([RoleId], [RoleName], [Description]) VALUES (1, N'StorageCustomer', NULL);
INSERT INTO [Role] ([RoleId], [RoleName], [Description]) VALUES (2, N'FacilityStaff', NULL);
INSERT INTO [Role] ([RoleId], [RoleName], [Description]) VALUES (3, N'FacilityManager', NULL);
INSERT INTO [Role] ([RoleId], [RoleName], [Description]) VALUES (4, N'BusinessOperationsManager', NULL);
INSERT INTO [Role] ([RoleId], [RoleName], [Description]) VALUES (5, N'SystemAdministrator', NULL);
SET IDENTITY_INSERT [Role] OFF;
END
GO
IF NOT EXISTS (SELECT 1 FROM [StaffFacility])
BEGIN
SET IDENTITY_INSERT [StaffFacility] ON;
INSERT INTO [StaffFacility] ([StaffFacilityId], [UserId], [FacilityId], [AssignedDate]) VALUES (1, 2, 1, '2026-10-01T00:00:00.0000000');
INSERT INTO [StaffFacility] ([StaffFacilityId], [UserId], [FacilityId], [AssignedDate]) VALUES (2, 3, 1, '2026-10-01T00:00:00.0000000');
SET IDENTITY_INSERT [StaffFacility] OFF;
END
GO
IF NOT EXISTS (SELECT 1 FROM [StorageUnit])
BEGIN
SET IDENTITY_INSERT [StorageUnit] ON;
INSERT INTO [StorageUnit] ([StorageUnitId], [FacilityId], [FloorId], [UnitTypeId], [UnitCode], [Zone], [Status], [CurrentPrice], [Note]) VALUES (1, 1, 1, 1, N'A-101', N'A', N'Available', 900000.00, NULL);
INSERT INTO [StorageUnit] ([StorageUnitId], [FacilityId], [FloorId], [UnitTypeId], [UnitCode], [Zone], [Status], [CurrentPrice], [Note]) VALUES (2, 1, 1, 1, N'A-102', N'A', N'Available', 900000.00, NULL);
INSERT INTO [StorageUnit] ([StorageUnitId], [FacilityId], [FloorId], [UnitTypeId], [UnitCode], [Zone], [Status], [CurrentPrice], [Note]) VALUES (3, 1, 1, 1, N'A-103', N'A', N'Available', 900000.00, NULL);
INSERT INTO [StorageUnit] ([StorageUnitId], [FacilityId], [FloorId], [UnitTypeId], [UnitCode], [Zone], [Status], [CurrentPrice], [Note]) VALUES (4, 1, 1, 2, N'A-104', N'A', N'Available', 1600000.00, NULL);
INSERT INTO [StorageUnit] ([StorageUnitId], [FacilityId], [FloorId], [UnitTypeId], [UnitCode], [Zone], [Status], [CurrentPrice], [Note]) VALUES (5, 1, 1, 2, N'A-105', N'A', N'Available', 1600000.00, NULL);
INSERT INTO [StorageUnit] ([StorageUnitId], [FacilityId], [FloorId], [UnitTypeId], [UnitCode], [Zone], [Status], [CurrentPrice], [Note]) VALUES (6, 1, 1, 2, N'A-106', N'A', N'Available', 1600000.00, NULL);
SET IDENTITY_INSERT [StorageUnit] OFF;
END
GO
IF NOT EXISTS (SELECT 1 FROM [TicketCategory])
BEGIN
SET IDENTITY_INSERT [TicketCategory] ON;
INSERT INTO [TicketCategory] ([CategoryId], [Name], [DefaultPriority]) VALUES (1, N'Ra vào / khoá kho', N'High');
INSERT INTO [TicketCategory] ([CategoryId], [Name], [DefaultPriority]) VALUES (2, N'Hư hỏng / sửa chữa', N'Normal');
INSERT INTO [TicketCategory] ([CategoryId], [Name], [DefaultPriority]) VALUES (3, N'Vệ sinh / côn trùng', N'Normal');
INSERT INTO [TicketCategory] ([CategoryId], [Name], [DefaultPriority]) VALUES (4, N'Thanh toán / hoá đơn', N'Normal');
INSERT INTO [TicketCategory] ([CategoryId], [Name], [DefaultPriority]) VALUES (5, N'Khác', N'Low');
SET IDENTITY_INSERT [TicketCategory] OFF;
END
GO
IF NOT EXISTS (SELECT 1 FROM [UnitPrice])
BEGIN
SET IDENTITY_INSERT [UnitPrice] ON;
INSERT INTO [UnitPrice] ([UnitPriceId], [PricingPolicyId], [UnitTypeId], [BasePricePerMonth], [Discount3Month], [Discount6Month], [Discount12Month]) VALUES (1, 1, 1, 900000.00, 5.00, 10.00, 15.00);
INSERT INTO [UnitPrice] ([UnitPriceId], [PricingPolicyId], [UnitTypeId], [BasePricePerMonth], [Discount3Month], [Discount6Month], [Discount12Month]) VALUES (2, 1, 2, 1600000.00, 5.00, 10.00, 15.00);
SET IDENTITY_INSERT [UnitPrice] OFF;
END
GO
IF NOT EXISTS (SELECT 1 FROM [UnitType])
BEGIN
SET IDENTITY_INSERT [UnitType] ON;
INSERT INTO [UnitType] ([UnitTypeId], [Code], [Name], [AreaM2], [Width], [Depth], [Height], [IsClimateControlled]) VALUES (1, N'S5', N'5 m²', 5.00, 2.00, 2.50, 2.40, 0);
INSERT INTO [UnitType] ([UnitTypeId], [Code], [Name], [AreaM2], [Width], [Depth], [Height], [IsClimateControlled]) VALUES (2, N'M10', N'10 m²', 10.00, 2.50, 4.00, 2.40, 0);
SET IDENTITY_INSERT [UnitType] OFF;
END
GO
IF NOT EXISTS (SELECT 1 FROM [User])
BEGIN
SET IDENTITY_INSERT [User] ON;
INSERT INTO [User] ([UserId], [RoleId], [FullName], [Email], [PhoneNumber], [PasswordHash], [CitizenId], [DateOfBirth], [Address], [Status], [CreatedAt]) VALUES (1, 1, N'Khách Demo', N'customer@storease.vn', N'0900000001', N'PBKDF2-SHA256$100000$7SE1n+Tk1yoyh0tQKWUOWw==$mFss1r/fLlyArhLozsyAQN7CfcNriP/edgw6GHS8/hI=', N'079000000001', NULL, NULL, N'Active', '2026-11-12T07:07:59.7999477');
INSERT INTO [User] ([UserId], [RoleId], [FullName], [Email], [PhoneNumber], [PasswordHash], [CitizenId], [DateOfBirth], [Address], [Status], [CreatedAt]) VALUES (2, 2, N'Nhân viên Demo', N'staff@storease.vn', N'0900000002', N'PBKDF2-SHA256$100000$JLVubZMxWHmKC1w8oz3iCQ==$pUdW8Jgp6PEAP4VLVOX4imfWDexrC8PrZzcFSWo5uPQ=', NULL, NULL, NULL, N'Active', '2026-11-12T07:07:59.9024012');
INSERT INTO [User] ([UserId], [RoleId], [FullName], [Email], [PhoneNumber], [PasswordHash], [CitizenId], [DateOfBirth], [Address], [Status], [CreatedAt]) VALUES (3, 3, N'Quản lý Demo', N'manager@storease.vn', N'0900000003', N'PBKDF2-SHA256$100000$jAoPccvRBBKOgFS6XSH3wg==$4029rnYzW2m8l9L6A7lS9mj1V+inXuscBWsuij/N/ok=', NULL, NULL, NULL, N'Active', '2026-11-12T07:07:59.9274159');
INSERT INTO [User] ([UserId], [RoleId], [FullName], [Email], [PhoneNumber], [PasswordHash], [CitizenId], [DateOfBirth], [Address], [Status], [CreatedAt]) VALUES (4, 4, N'Vận hành Demo', N'ops@storease.vn', N'0900000004', N'PBKDF2-SHA256$100000$MXZrspY9bsJvoolnWOmoSg==$mvuBaXvcXinEEuEyxYvKCK6xQbOB/8rJMPiTYARsHFE=', NULL, NULL, NULL, N'Active', '2026-11-12T07:07:59.9547453');
INSERT INTO [User] ([UserId], [RoleId], [FullName], [Email], [PhoneNumber], [PasswordHash], [CitizenId], [DateOfBirth], [Address], [Status], [CreatedAt]) VALUES (5, 5, N'Quản trị Demo', N'admin@storease.vn', N'0900000005', N'PBKDF2-SHA256$100000$nWcCFPTE+knByGUzEavNUQ==$Ik7fQCVxsMWcg8bXAMbDVjh3rJS29JlaN0q1VeGOyW4=', NULL, NULL, NULL, N'Active', '2026-11-12T07:07:59.9797721');
SET IDENTITY_INSERT [User] OFF;
END
GO
ALTER TABLE [Facility] WITH CHECK CHECK CONSTRAINT ALL;
ALTER TABLE [Floor] WITH CHECK CHECK CONSTRAINT ALL;
ALTER TABLE [PricingPolicy] WITH CHECK CHECK CONSTRAINT ALL;
ALTER TABLE [Role] WITH CHECK CHECK CONSTRAINT ALL;
ALTER TABLE [StaffFacility] WITH CHECK CHECK CONSTRAINT ALL;
ALTER TABLE [StorageUnit] WITH CHECK CHECK CONSTRAINT ALL;
ALTER TABLE [TicketCategory] WITH CHECK CHECK CONSTRAINT ALL;
ALTER TABLE [UnitPrice] WITH CHECK CHECK CONSTRAINT ALL;
ALTER TABLE [UnitType] WITH CHECK CHECK CONSTRAINT ALL;
ALTER TABLE [User] WITH CHECK CHECK CONSTRAINT ALL;
GO
