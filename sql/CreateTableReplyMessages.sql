CREATE TABLE dbo.ReplyMessages
(
    MessageKey nvarchar(15) NOT NULL,
    MessageValue nvarchar(max) NOT NULL,
    PRIMARY KEY (MessageKey),
    UNIQUE CLUSTERED (MessageKey)
)
