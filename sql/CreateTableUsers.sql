CREATE TABLE Users 
(
    Id int NOT NULL IDENTITY(1,1) PRIMARY KEY,
    ChatId BIGINT NOT NULL,
    UserId BIGINT NOT NULL,
    UserName nvarchar(max),
    Inactive bit NOT NULL DEFAULT '0',
)
