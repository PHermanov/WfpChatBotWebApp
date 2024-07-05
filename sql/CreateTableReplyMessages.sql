CREATE TABLE `replymessages` (
    `key` varchar(15) NOT NULL,
    `value` text NOT NULL,
    PRIMARY KEY (`key`),
    UNIQUE KEY `key_UNIQUE` (`key`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
