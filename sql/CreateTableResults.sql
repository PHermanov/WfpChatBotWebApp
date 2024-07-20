CREATE TABLE `results` (
  `chatid` BIGINT NOT NULL,
  `userid` BIGINT NOT NULL,
  `playdate` date NOT NULL,
  `id` int NOT NULL AUTO_INCREMENT,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
