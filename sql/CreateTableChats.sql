CREATE TABLE Chats
(
    Id int NOT NULL IDENTITY(1,1) PRIMARY KEY,
    ChatId BIGINT NOT NULL,
    GameEnabled bit NOT NULL,
    Comment nvarchar(max) NULL
)
