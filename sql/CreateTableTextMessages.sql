CREATE TABLE TextMessages 
(
  Name nvarchar(45) NOT NULL PRIMARY KEY,
  Text nvarchar(max) NOT NULL,
  UNIQUE CLUSTERED (Name)
)
