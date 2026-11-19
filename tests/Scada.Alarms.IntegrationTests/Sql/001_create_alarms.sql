-- Simplified version of the Action.Net dbo.Alarms table:
-- only the columns this project reads, no secondary indexes.
CREATE TABLE [dbo].[Alarms](
	[ID] [bigint] IDENTITY(1,1) NOT NULL PRIMARY KEY,
	[Level] [nvarchar](256) NULL,
	[TagName] [nvarchar](1024) NULL,
	[Value] [nvarchar](1024) NULL,
	[ActiveTime_Ticks] [bigint] NOT NULL
)
GO
