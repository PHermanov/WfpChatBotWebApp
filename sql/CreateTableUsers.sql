CREATE TABLE `users` (
  `chatid` int NOT NULL,
  `userid` int NOT NULL,
  `username` text,
  `inactive` tinyint NOT NULL DEFAULT '0',
  `id` int NOT NULL AUTO_INCREMENT,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=4 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
