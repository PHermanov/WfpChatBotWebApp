CREATE TABLE Stickers 
(
    Name nvarchar(45) NOT NULL PRIMARY KEY,
    StickerSet nvarchar(10) NOT NULL,
    Url nvarchar (200) NOT NULL,
    UNIQUE CLUSTERED (Name)
)