/* StorEase database schema + demo seed
   Generated from the current EF Core migrations on branch demo.
   Run in SQL Server Management Studio or Azure Data Studio. */

IF DB_ID(N'StorEase') IS NULL
BEGIN
    EXEC(N'CREATE DATABASE [StorEase]');
END;
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



GO

/* Demo seed data. Safe to run repeatedly: rows are keyed by stable codes/emails. */
DECLARE @StorageCustomerRoleId int, @FacilityStaffRoleId int, @FacilityManagerRoleId int, @BusinessOpsRoleId int, @AdminRoleId int;

IF NOT EXISTS (SELECT 1 FROM [Role] WHERE [RoleName] = 'StorageCustomer')
    INSERT INTO [Role] ([RoleName], [Description]) VALUES ('StorageCustomer', 'Customer account');
IF NOT EXISTS (SELECT 1 FROM [Role] WHERE [RoleName] = 'FacilityStaff')
    INSERT INTO [Role] ([RoleName], [Description]) VALUES ('FacilityStaff', 'Facility staff account');
IF NOT EXISTS (SELECT 1 FROM [Role] WHERE [RoleName] = 'FacilityManager')
    INSERT INTO [Role] ([RoleName], [Description]) VALUES ('FacilityManager', 'Facility manager account');
IF NOT EXISTS (SELECT 1 FROM [Role] WHERE [RoleName] = 'BusinessOperationsManager')
    INSERT INTO [Role] ([RoleName], [Description]) VALUES ('BusinessOperationsManager', 'Pricing and operations account');
IF NOT EXISTS (SELECT 1 FROM [Role] WHERE [RoleName] = 'SystemAdministrator')
    INSERT INTO [Role] ([RoleName], [Description]) VALUES ('SystemAdministrator', 'System administrator account');

SELECT @StorageCustomerRoleId = [RoleId] FROM [Role] WHERE [RoleName] = 'StorageCustomer';
SELECT @FacilityStaffRoleId = [RoleId] FROM [Role] WHERE [RoleName] = 'FacilityStaff';
SELECT @FacilityManagerRoleId = [RoleId] FROM [Role] WHERE [RoleName] = 'FacilityManager';
SELECT @BusinessOpsRoleId = [RoleId] FROM [Role] WHERE [RoleName] = 'BusinessOperationsManager';
SELECT @AdminRoleId = [RoleId] FROM [Role] WHERE [RoleName] = 'SystemAdministrator';

DECLARE @DemoPasswordHash varchar(255) = 'PBKDF2-SHA256$100000$UsafiILgFf1I0rfQ9p0iNw==$8miB3x3HSLJwHVVAtMh3WdFArk6SjWgDa3omN5rC5pc=';

IF NOT EXISTS (SELECT 1 FROM [User] WHERE [Email] = 'customer@storease.vn')
    INSERT INTO [User] ([RoleId],[FullName],[Email],[PhoneNumber],[PasswordHash],[Status],[CreatedAt])
    VALUES (@StorageCustomerRoleId,'Demo Customer','customer@storease.vn','0900000000',@DemoPasswordHash,'Active',SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM [User] WHERE [Email] = 'staff@storease.vn')
    INSERT INTO [User] ([RoleId],[FullName],[Email],[PhoneNumber],[PasswordHash],[Status],[CreatedAt])
    VALUES (@FacilityStaffRoleId,'Demo Staff','staff@storease.vn','0900000000',@DemoPasswordHash,'Active',SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM [User] WHERE [Email] = 'manager@storease.vn')
    INSERT INTO [User] ([RoleId],[FullName],[Email],[PhoneNumber],[PasswordHash],[Status],[CreatedAt])
    VALUES (@FacilityManagerRoleId,'Demo Facility Manager','manager@storease.vn','0900000000',@DemoPasswordHash,'Active',SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM [User] WHERE [Email] = 'ops@storease.vn')
    INSERT INTO [User] ([RoleId],[FullName],[Email],[PhoneNumber],[PasswordHash],[Status],[CreatedAt])
    VALUES (@BusinessOpsRoleId,'Demo Operations Manager','ops@storease.vn','0900000000',@DemoPasswordHash,'Active',SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM [User] WHERE [Email] = 'admin@storease.vn')
    INSERT INTO [User] ([RoleId],[FullName],[Email],[PhoneNumber],[PasswordHash],[Status],[CreatedAt])
    VALUES (@AdminRoleId,'Demo Administrator','admin@storease.vn','0900000000',@DemoPasswordHash,'Active',SYSUTCDATETIME());

DECLARE @CustomerId int = (SELECT [UserId] FROM [User] WHERE [Email] = 'customer@storease.vn');
DECLARE @StaffId int = (SELECT [UserId] FROM [User] WHERE [Email] = 'staff@storease.vn');
DECLARE @ManagerId int = (SELECT [UserId] FROM [User] WHERE [Email] = 'manager@storease.vn');
DECLARE @OpsId int = (SELECT [UserId] FROM [User] WHERE [Email] = 'ops@storease.vn');

IF NOT EXISTS (SELECT 1 FROM [Facility] WHERE [Code] = 'DEMO-GV')
    INSERT INTO [Facility] ([ManagerId],[Code],[Name],[Address],[District],[Phone],[OpeningHours],[Status])
    VALUES (@ManagerId,'DEMO-GV','StorEase Demo Go Vap','123 Demo Street','Go Vap','0900000000','08:00-20:00','Active');
DECLARE @FacilityId int = (SELECT [FacilityId] FROM [Facility] WHERE [Code] = 'DEMO-GV');

IF NOT EXISTS (SELECT 1 FROM [Floor] WHERE [FacilityId] = @FacilityId AND [FloorNumber] = 1)
    INSERT INTO [Floor] ([FacilityId],[FloorNumber],[Name]) VALUES (@FacilityId,1,'Ground Floor');
DECLARE @FloorId int = (SELECT [FloorId] FROM [Floor] WHERE [FacilityId] = @FacilityId AND [FloorNumber] = 1);

IF NOT EXISTS (SELECT 1 FROM [UnitType] WHERE [Code] = 'S5')
    INSERT INTO [UnitType] ([Code],[Name],[AreaM2],[Width],[Depth],[Height],[IsClimateControlled])
    VALUES ('S5','Small 5m2',5,2,2.5,2.2,0);
IF NOT EXISTS (SELECT 1 FROM [UnitType] WHERE [Code] = 'M10')
    INSERT INTO [UnitType] ([Code],[Name],[AreaM2],[Width],[Depth],[Height],[IsClimateControlled])
    VALUES ('M10','Medium 10m2',10,2.5,4,2.2,1);
DECLARE @SmallTypeId int = (SELECT [UnitTypeId] FROM [UnitType] WHERE [Code] = 'S5');
DECLARE @MediumTypeId int = (SELECT [UnitTypeId] FROM [UnitType] WHERE [Code] = 'M10');

IF NOT EXISTS (SELECT 1 FROM [StorageUnit] WHERE [FacilityId] = @FacilityId AND [UnitCode] = 'A-101')
    INSERT INTO [StorageUnit] ([FacilityId],[FloorId],[UnitTypeId],[UnitCode],[Zone],[Status],[CurrentPrice])
    VALUES (@FacilityId,@FloorId,@SmallTypeId,'A-101','A','Available',900000);
IF NOT EXISTS (SELECT 1 FROM [StorageUnit] WHERE [FacilityId] = @FacilityId AND [UnitCode] = 'A-102')
    INSERT INTO [StorageUnit] ([FacilityId],[FloorId],[UnitTypeId],[UnitCode],[Zone],[Status],[CurrentPrice])
    VALUES (@FacilityId,@FloorId,@SmallTypeId,'A-102','A','Available',900000);
IF NOT EXISTS (SELECT 1 FROM [StorageUnit] WHERE [FacilityId] = @FacilityId AND [UnitCode] = 'A-103')
    INSERT INTO [StorageUnit] ([FacilityId],[FloorId],[UnitTypeId],[UnitCode],[Zone],[Status],[CurrentPrice])
    VALUES (@FacilityId,@FloorId,@MediumTypeId,'A-103','A','Available',1500000);
IF NOT EXISTS (SELECT 1 FROM [StorageUnit] WHERE [FacilityId] = @FacilityId AND [UnitCode] = 'A-104')
    INSERT INTO [StorageUnit] ([FacilityId],[FloorId],[UnitTypeId],[UnitCode],[Zone],[Status],[CurrentPrice])
    VALUES (@FacilityId,@FloorId,@MediumTypeId,'A-104','A','Available',1500000);

IF NOT EXISTS (SELECT 1 FROM [PricingPolicy] WHERE [Version] = 1)
    INSERT INTO [PricingPolicy] ([CreatedBy],[Version],[EffectiveFrom],[DepositMonths],[GraceDays],[NoticeDays],[LateFeePercentPerWeek],[SuspendAfterDays],[ClearOutAfterDays],[CancelFreeHours],[Status])
    VALUES (@OpsId,1,'2026-01-01',1,3,30,2,14,60,24,'Active');
DECLARE @PolicyId int = (SELECT [PricingPolicyId] FROM [PricingPolicy] WHERE [Version] = 1);

IF NOT EXISTS (SELECT 1 FROM [UnitPrice] WHERE [PricingPolicyId] = @PolicyId AND [UnitTypeId] = @SmallTypeId)
    INSERT INTO [UnitPrice] ([PricingPolicyId],[UnitTypeId],[BasePricePerMonth],[Discount3Month],[Discount6Month],[Discount12Month])
    VALUES (@PolicyId,@SmallTypeId,900000,5,10,15);
IF NOT EXISTS (SELECT 1 FROM [UnitPrice] WHERE [PricingPolicyId] = @PolicyId AND [UnitTypeId] = @MediumTypeId)
    INSERT INTO [UnitPrice] ([PricingPolicyId],[UnitTypeId],[BasePricePerMonth],[Discount3Month],[Discount6Month],[Discount12Month])
    VALUES (@PolicyId,@MediumTypeId,1500000,5,10,15);
IF NOT EXISTS (SELECT 1 FROM [FeeType] WHERE [PricingPolicyId] = @PolicyId AND [Code] = 'SETUP')
    INSERT INTO [FeeType] ([PricingPolicyId],[Code],[Name],[Amount],[IsPercentage])
    VALUES (@PolicyId,'SETUP','Setup fee',100000,0);

IF NOT EXISTS (SELECT 1 FROM [Invoice] WHERE [InvoiceNumber] = 'INV-DEMO-001')
BEGIN
    INSERT INTO [Invoice] ([CustomerId],[InvoiceNumber],[IssueDate],[DueDate],[TotalAmount],[PaidAmount],[Status])
    VALUES (@CustomerId,'INV-DEMO-001',CONVERT(date,SYSUTCDATETIME()),DATEADD(day,7,CONVERT(date,SYSUTCDATETIME())),1000000,0,'Issued');
    DECLARE @InvoiceId int = SCOPE_IDENTITY();
    INSERT INTO [InvoiceItem] ([InvoiceId],[Description],[Quantity],[UnitAmount],[LineAmount])
    VALUES (@InvoiceId,'Demo monthly rent',1,1000000,1000000);
END;

IF NOT EXISTS (SELECT 1 FROM [StaffFacility] WHERE [UserId] = @StaffId AND [FacilityId] = @FacilityId)
    INSERT INTO [StaffFacility] ([UserId],[FacilityId],[AssignedDate])
    VALUES (@StaffId,@FacilityId,CONVERT(date,SYSUTCDATETIME()));
IF NOT EXISTS (SELECT 1 FROM [StaffFacility] WHERE [UserId] = @ManagerId AND [FacilityId] = @FacilityId)
    INSERT INTO [StaffFacility] ([UserId],[FacilityId],[AssignedDate])
    VALUES (@ManagerId,@FacilityId,CONVERT(date,SYSUTCDATETIME()));

PRINT 'StorEase schema and demo data are ready.';

