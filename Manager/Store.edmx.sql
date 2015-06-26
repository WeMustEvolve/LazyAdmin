
-- --------------------------------------------------
-- Entity Designer DDL Script for SQL Server 2005, 2008, 2012 and Azure
-- --------------------------------------------------
-- Date Created: 05/17/2015 18:09:22
-- Generated from EDMX file: C:\Users\andy_\documents\visual studio 2013\Projects\LazyAdmin\Manager\Store.edmx
-- --------------------------------------------------

SET QUOTED_IDENTIFIER OFF;
GO
USE [LazyAdmin];
GO
IF SCHEMA_ID(N'dbo') IS NULL EXECUTE(N'CREATE SCHEMA [dbo]');
GO

-- --------------------------------------------------
-- Dropping existing FOREIGN KEY constraints
-- --------------------------------------------------

IF OBJECT_ID(N'[dbo].[FK_ItemProperty]', 'F') IS NOT NULL
    ALTER TABLE [dbo].[Properties] DROP CONSTRAINT [FK_ItemProperty];
GO
IF OBJECT_ID(N'[dbo].[FK_ListItem_List]', 'F') IS NOT NULL
    ALTER TABLE [dbo].[ListItem] DROP CONSTRAINT [FK_ListItem_List];
GO
IF OBJECT_ID(N'[dbo].[FK_ListItem_Item]', 'F') IS NOT NULL
    ALTER TABLE [dbo].[ListItem] DROP CONSTRAINT [FK_ListItem_Item];
GO

-- --------------------------------------------------
-- Dropping existing tables
-- --------------------------------------------------

IF OBJECT_ID(N'[dbo].[Lists]', 'U') IS NOT NULL
    DROP TABLE [dbo].[Lists];
GO
IF OBJECT_ID(N'[dbo].[Properties]', 'U') IS NOT NULL
    DROP TABLE [dbo].[Properties];
GO
IF OBJECT_ID(N'[dbo].[Items]', 'U') IS NOT NULL
    DROP TABLE [dbo].[Items];
GO
IF OBJECT_ID(N'[dbo].[ListItem]', 'U') IS NOT NULL
    DROP TABLE [dbo].[ListItem];
GO

-- --------------------------------------------------
-- Creating all tables
-- --------------------------------------------------

-- Creating table 'Lists'
CREATE TABLE [dbo].[Lists] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [Name] nvarchar(30)  NOT NULL,
    [ListType] nvarchar(max)  NULL
);
GO

-- Creating table 'Properties'
CREATE TABLE [dbo].[Properties] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [Name] nvarchar(30)  NOT NULL,
    [PropValue] nvarchar(max)  NULL,
    [ItemId] int  NOT NULL
);
GO

-- Creating table 'Items'
CREATE TABLE [dbo].[Items] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [Name] nvarchar(30)  NOT NULL,
    [StateInfo] nvarchar(max)  NULL
);
GO

-- Creating table 'ListItem'
CREATE TABLE [dbo].[ListItem] (
    [Lists_Id] int  NOT NULL,
    [Items_Id] int  NOT NULL
);
GO

-- --------------------------------------------------
-- Creating all PRIMARY KEY constraints
-- --------------------------------------------------

-- Creating primary key on [Id] in table 'Lists'
ALTER TABLE [dbo].[Lists]
ADD CONSTRAINT [PK_Lists]
    PRIMARY KEY CLUSTERED ([Id] ASC);
GO

-- Creating primary key on [Id] in table 'Properties'
ALTER TABLE [dbo].[Properties]
ADD CONSTRAINT [PK_Properties]
    PRIMARY KEY CLUSTERED ([Id] ASC);
GO

-- Creating primary key on [Id] in table 'Items'
ALTER TABLE [dbo].[Items]
ADD CONSTRAINT [PK_Items]
    PRIMARY KEY CLUSTERED ([Id] ASC);
GO

-- Creating primary key on [Lists_Id], [Items_Id] in table 'ListItem'
ALTER TABLE [dbo].[ListItem]
ADD CONSTRAINT [PK_ListItem]
    PRIMARY KEY CLUSTERED ([Lists_Id], [Items_Id] ASC);
GO

-- --------------------------------------------------
-- Creating all FOREIGN KEY constraints
-- --------------------------------------------------

-- Creating foreign key on [ItemId] in table 'Properties'
ALTER TABLE [dbo].[Properties]
ADD CONSTRAINT [FK_ItemProperty]
    FOREIGN KEY ([ItemId])
    REFERENCES [dbo].[Items]
        ([Id])
    ON DELETE CASCADE ON UPDATE NO ACTION;
GO

-- Creating non-clustered index for FOREIGN KEY 'FK_ItemProperty'
CREATE INDEX [IX_FK_ItemProperty]
ON [dbo].[Properties]
    ([ItemId]);
GO

-- Creating foreign key on [Lists_Id] in table 'ListItem'
ALTER TABLE [dbo].[ListItem]
ADD CONSTRAINT [FK_ListItem_List]
    FOREIGN KEY ([Lists_Id])
    REFERENCES [dbo].[Lists]
        ([Id])
    ON DELETE NO ACTION ON UPDATE NO ACTION;
GO

-- Creating foreign key on [Items_Id] in table 'ListItem'
ALTER TABLE [dbo].[ListItem]
ADD CONSTRAINT [FK_ListItem_Item]
    FOREIGN KEY ([Items_Id])
    REFERENCES [dbo].[Items]
        ([Id])
    ON DELETE NO ACTION ON UPDATE NO ACTION;
GO

-- Creating non-clustered index for FOREIGN KEY 'FK_ListItem_Item'
CREATE INDEX [IX_FK_ListItem_Item]
ON [dbo].[ListItem]
    ([Items_Id]);
GO

-- --------------------------------------------------
-- Script has ended
-- --------------------------------------------------