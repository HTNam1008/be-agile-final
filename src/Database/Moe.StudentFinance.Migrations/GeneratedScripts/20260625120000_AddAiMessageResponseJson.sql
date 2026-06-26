-- Migration: Add ResponseJson column to ai.Message
-- Generated: 2026-06-25
-- Run after: 20260624172220_AddAiCopilotConversations

ALTER TABLE [ai].[Message] ADD [ResponseJson] nvarchar(8000) NULL;
GO
