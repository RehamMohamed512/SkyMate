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
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103100516_InitialIdentityAndTravelSchema'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103100516_InitialIdentityAndTravelSchema'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] int NOT NULL IDENTITY,
        [FirstName] nvarchar(100) NOT NULL,
        [LastName] nvarchar(100) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103100516_InitialIdentityAndTravelSchema'
)
BEGIN
    CREATE TABLE [Interests] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) COLLATE Latin1_General_100_CI_AS NOT NULL,
        CONSTRAINT [PK_Interests] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Interests_Name_Trimmed] CHECK (LEN([Name]) > 0 AND DATALENGTH([Name]) = DATALENGTH(LTRIM(RTRIM([Name]))))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103100516_InitialIdentityAndTravelSchema'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] int NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103100516_InitialIdentityAndTravelSchema'
)
BEGIN
    CREATE TABLE [AiConversations] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [Title] nvarchar(200) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_AiConversations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AiConversations_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103100516_InitialIdentityAndTravelSchema'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103100516_InitialIdentityAndTravelSchema'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] int NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103100516_InitialIdentityAndTravelSchema'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] int NOT NULL,
        [RoleId] int NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103100516_InitialIdentityAndTravelSchema'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] int NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103100516_InitialIdentityAndTravelSchema'
)
BEGIN
    CREATE TABLE [Trips] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [Destination] nvarchar(200) NOT NULL,
        [Budget] decimal(18,2) NOT NULL,
        [Currency] char(3) NOT NULL DEFAULT 'USD',
        [DaysNumber] int NOT NULL,
        [TravellersCount] int NOT NULL,
        [StartDate] date NOT NULL,
        [Pace] nvarchar(20) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Trips] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Trips_Budget_NonNegative] CHECK ([Budget] >= 0),
        CONSTRAINT [CK_Trips_Currency_Length] CHECK (LEN([Currency]) = 3),
        CONSTRAINT [CK_Trips_DaysNumber_Range] CHECK ([DaysNumber] BETWEEN 1 AND 30),
        CONSTRAINT [CK_Trips_Pace_Values] CHECK ([Pace] IN ('Relaxed', 'Balanced', 'Packed')),
        CONSTRAINT [CK_Trips_TravellersCount_Minimum] CHECK ([TravellersCount] >= 1),
        CONSTRAINT [FK_Trips_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103100516_InitialIdentityAndTravelSchema'
)
BEGIN
    CREATE TABLE [UserPhones] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [PhoneNumber] nvarchar(20) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_UserPhones] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserPhones_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103100516_InitialIdentityAndTravelSchema'
)
BEGIN
    CREATE TABLE [UserPreferences] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [Type] nvarchar(50) NOT NULL,
        [Interest] nvarchar(100) NOT NULL,
        [Pace] nvarchar(20) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_UserPreferences] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserPreferences_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103100516_InitialIdentityAndTravelSchema'
)
BEGIN
    CREATE TABLE [AiConversationMessages] (
        [Id] int NOT NULL IDENTITY,
        [ConversationId] int NOT NULL,
        [Role] nvarchar(20) NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AiConversationMessages] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_AiConversationMessages_Role_Values] CHECK ([Role] IN ('User', 'Assistant')),
        CONSTRAINT [FK_AiConversationMessages_AiConversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [AiConversations] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103100516_InitialIdentityAndTravelSchema'
)
BEGIN
    CREATE TABLE [TripDays] (
        [Id] int NOT NULL IDENTITY,
        [TripId] int NOT NULL,
        [DayNumber] int NOT NULL,
        [Date] date NOT NULL,
        [WeekDay] nvarchar(20) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_TripDays] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_TripDays_DayNumber_Minimum] CHECK ([DayNumber] >= 1),
        CONSTRAINT [FK_TripDays_Trips_TripId] FOREIGN KEY ([TripId]) REFERENCES [Trips] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103100516_InitialIdentityAndTravelSchema'
)
BEGIN
    CREATE TABLE [TripInterests] (
        [TripId] int NOT NULL,
        [InterestId] int NOT NULL,
        CONSTRAINT [PK_TripInterests] PRIMARY KEY ([TripId], [InterestId]),
        CONSTRAINT [FK_TripInterests_Interests_InterestId] FOREIGN KEY ([InterestId]) REFERENCES [Interests] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TripInterests_Trips_TripId] FOREIGN KEY ([TripId]) REFERENCES [Trips] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103100516_InitialIdentityAndTravelSchema'
)
BEGIN
    CREATE TABLE [Activities] (
        [Id] int NOT NULL IDENTITY,
        [DayId] int NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(1000) NULL,
        [LocationName] nvarchar(200) NULL,
        [ActivityType] nvarchar(30) NOT NULL,
        [EstimatedCost] decimal(18,2) NOT NULL,
        [CostBasis] nvarchar(20) NOT NULL,
        [StartTime] time NULL,
        [EndTime] time NULL,
        [ActivityOrder] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Activities] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Activities_ActivityOrder_Minimum] CHECK ([ActivityOrder] >= 1),
        CONSTRAINT [CK_Activities_ActivityType_Values] CHECK ([ActivityType] IN ('Sightseeing', 'Food', 'Shopping', 'Nature', 'Culture', 'Adventure', 'Transport', 'Accommodation', 'Other')),
        CONSTRAINT [CK_Activities_CostBasis_Values] CHECK ([CostBasis] IN ('PerPerson', 'PerGroup', 'Free')),
        CONSTRAINT [CK_Activities_EstimatedCost_NonNegative] CHECK ([EstimatedCost] >= 0),
        CONSTRAINT [CK_Activities_Time_Order] CHECK ([StartTime] IS NULL OR [EndTime] IS NULL OR [EndTime] > [StartTime]),
        CONSTRAINT [FK_Activities_TripDays_DayId] FOREIGN KEY ([DayId]) REFERENCES [TripDays] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103100516_InitialIdentityAndTravelSchema'
)
BEGIN
    CREATE INDEX [IX_Activities_DayId_ActivityOrder] ON [Activities] ([DayId], [ActivityOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103100516_InitialIdentityAndTravelSchema'
)
BEGIN
    CREATE INDEX [IX_AiConversationMessages_ConversationId_CreatedAt] ON [AiConversationMessages] ([ConversationId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103100516_InitialIdentityAndTravelSchema'
)
BEGIN
    CREATE INDEX [IX_AiConversations_UserId_CreatedAt] ON [AiConversations] ([UserId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103100516_InitialIdentityAndTravelSchema'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103100516_InitialIdentityAndTravelSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103100516_InitialIdentityAndTravelSchema'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103100516_InitialIdentityAndTravelSchema'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103100516_InitialIdentityAndTravelSchema'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103100516_InitialIdentityAndTravelSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]) WHERE [NormalizedEmail] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103100516_InitialIdentityAndTravelSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103100516_InitialIdentityAndTravelSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Interests_Name] ON [Interests] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103100516_InitialIdentityAndTravelSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TripDays_TripId_DayNumber] ON [TripDays] ([TripId], [DayNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103100516_InitialIdentityAndTravelSchema'
)
BEGIN
    CREATE INDEX [IX_TripInterests_InterestId] ON [TripInterests] ([InterestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103100516_InitialIdentityAndTravelSchema'
)
BEGIN
    CREATE INDEX [IX_Trips_UserId_StartDate] ON [Trips] ([UserId], [StartDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103100516_InitialIdentityAndTravelSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserPhones_UserId_PhoneNumber] ON [UserPhones] ([UserId], [PhoneNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103100516_InitialIdentityAndTravelSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserPreferences_UserId_Type_Interest] ON [UserPreferences] ([UserId], [Type], [Interest]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103100516_InitialIdentityAndTravelSchema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261103100516_InitialIdentityAndTravelSchema', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116130808_ChatbotConversationTripLifecycle'
)
BEGIN
    IF EXISTS (SELECT 1 FROM [AiConversations]) THROW 51000, 'ChatbotConversationTripLifecycle requires each existing conversation to be assigned to its correct trip before migration.', 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116130808_ChatbotConversationTripLifecycle'
)
BEGIN
    ALTER TABLE [AiConversations] ADD [TripId] int NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116130808_ChatbotConversationTripLifecycle'
)
BEGIN
    ALTER TABLE [Trips] ADD CONSTRAINT [AK_Trips_Id_UserId] UNIQUE ([Id], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116130808_ChatbotConversationTripLifecycle'
)
BEGIN
    CREATE INDEX [IX_AiConversations_TripId_UserId_CreatedAt] ON [AiConversations] ([TripId], [UserId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116130808_ChatbotConversationTripLifecycle'
)
BEGIN
    ALTER TABLE [AiConversations] ADD CONSTRAINT [FK_AiConversations_Trips_TripId_UserId] FOREIGN KEY ([TripId], [UserId]) REFERENCES [Trips] ([Id], [UserId]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116130808_ChatbotConversationTripLifecycle'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261116130808_ChatbotConversationTripLifecycle', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116191355_EnforceTripAndActivityIntegrity'
)
BEGIN
    IF EXISTS (SELECT 1 FROM [TripDays] GROUP BY [TripId], [Date] HAVING COUNT(*) > 1) THROW 51001, 'Cannot enforce unique trip dates while duplicate TripDays dates exist.', 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116191355_EnforceTripAndActivityIntegrity'
)
BEGIN
    IF EXISTS (SELECT 1 FROM [Activities] GROUP BY [DayId], [ActivityOrder] HAVING COUNT(*) > 1) THROW 51002, 'Cannot enforce unique activity order while duplicate orders exist within a trip day.', 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116191355_EnforceTripAndActivityIntegrity'
)
BEGIN
    IF EXISTS (SELECT 1 FROM [UserPhones]) THROW 51003, 'Cannot drop UserPhones while phone records exist; migrate them first.', 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116191355_EnforceTripAndActivityIntegrity'
)
BEGIN
    ALTER TABLE [AiConversations] DROP CONSTRAINT [FK_AiConversations_AspNetUsers_UserId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116191355_EnforceTripAndActivityIntegrity'
)
BEGIN
    ALTER TABLE [Trips] DROP CONSTRAINT [FK_Trips_AspNetUsers_UserId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116191355_EnforceTripAndActivityIntegrity'
)
BEGIN
    DROP TABLE [UserPhones];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116191355_EnforceTripAndActivityIntegrity'
)
BEGIN
    DROP INDEX [IX_Activities_DayId_ActivityOrder] ON [Activities];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116191355_EnforceTripAndActivityIntegrity'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TripDays_TripId_Date] ON [TripDays] ([TripId], [Date]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116191355_EnforceTripAndActivityIntegrity'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Activities_DayId_ActivityOrder] ON [Activities] ([DayId], [ActivityOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116191355_EnforceTripAndActivityIntegrity'
)
BEGIN
    ALTER TABLE [Trips] ADD CONSTRAINT [FK_Trips_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116191355_EnforceTripAndActivityIntegrity'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261116191355_EnforceTripAndActivityIntegrity', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123191257_DecoupleAiConversationsFromTrips'
)
BEGIN
    ALTER TABLE [AiConversations] DROP CONSTRAINT [FK_AiConversations_Trips_TripId_UserId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123191257_DecoupleAiConversationsFromTrips'
)
BEGIN
    ALTER TABLE [Trips] DROP CONSTRAINT [AK_Trips_Id_UserId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123191257_DecoupleAiConversationsFromTrips'
)
BEGIN
    DROP INDEX [IX_AiConversations_TripId_UserId_CreatedAt] ON [AiConversations];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123191257_DecoupleAiConversationsFromTrips'
)
BEGIN
    DROP INDEX [IX_AiConversations_UserId_CreatedAt] ON [AiConversations];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123191257_DecoupleAiConversationsFromTrips'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AiConversations]') AND [c].[name] = N'TripId');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [AiConversations] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [AiConversations] DROP COLUMN [TripId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123191257_DecoupleAiConversationsFromTrips'
)
BEGIN
    CREATE INDEX [IX_AiConversations_UserId_UpdatedAt] ON [AiConversations] ([UserId], [UpdatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123191257_DecoupleAiConversationsFromTrips'
)
BEGIN
    ALTER TABLE [AiConversations] ADD CONSTRAINT [FK_AiConversations_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123191257_DecoupleAiConversationsFromTrips'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261123191257_DecoupleAiConversationsFromTrips', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261124132100_AddAssistantKnowledgeBaseAndProviderContext'
)
BEGIN
    ALTER TABLE [AiConversationMessages] ADD [ProviderContextJson] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261124132100_AddAssistantKnowledgeBaseAndProviderContext'
)
BEGIN
    CREATE TABLE [KnowledgeBaseEntries] (
        [Id] int NOT NULL IDENTITY,
        [SeedKey] nvarchar(205) NULL,
        [Title] nvarchar(200) NOT NULL,
        [Content] nvarchar(4000) NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        CONSTRAINT [PK_KnowledgeBaseEntries] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261124132100_AddAssistantKnowledgeBaseAndProviderContext'
)
BEGIN
    CREATE INDEX [IX_KnowledgeBaseEntries_IsActive_Id] ON [KnowledgeBaseEntries] ([IsActive], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261124132100_AddAssistantKnowledgeBaseAndProviderContext'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_KnowledgeBaseEntries_SeedKey] ON [KnowledgeBaseEntries] ([SeedKey]) WHERE [SeedKey] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261124132100_AddAssistantKnowledgeBaseAndProviderContext'
)
BEGIN
    CREATE UNIQUE INDEX [IX_KnowledgeBaseEntries_Title] ON [KnowledgeBaseEntries] ([Title]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261124132100_AddAssistantKnowledgeBaseAndProviderContext'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261124132100_AddAssistantKnowledgeBaseAndProviderContext', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127133031_RestoreConversationTripAssociation'
)
BEGIN
    ALTER TABLE [AiConversations] ADD [TripId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127133031_RestoreConversationTripAssociation'
)
BEGIN
    ALTER TABLE [Trips] ADD CONSTRAINT [AK_Trips_Id_UserId] UNIQUE ([Id], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127133031_RestoreConversationTripAssociation'
)
BEGIN
    CREATE INDEX [IX_AiConversations_TripId_UserId_CreatedAt] ON [AiConversations] ([TripId], [UserId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127133031_RestoreConversationTripAssociation'
)
BEGIN
    ALTER TABLE [AiConversations] ADD CONSTRAINT [FK_AiConversations_Trips_TripId_UserId] FOREIGN KEY ([TripId], [UserId]) REFERENCES [Trips] ([Id], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127133031_RestoreConversationTripAssociation'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261127133031_RestoreConversationTripAssociation', N'10.0.14');
END;

COMMIT;
GO

