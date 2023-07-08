CREATE TABLE `users` (
  `chatid` int NOT NULL,
  `userid` int NOT NULL,
  `username` text,
  `inactive` tinyint NOT NULL DEFAULT '0'
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
