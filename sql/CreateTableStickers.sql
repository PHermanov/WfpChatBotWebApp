CREATE TABLE `stickers` (
    `name` varchar(45) NOT NULL,
    `sticker_set` varchar(10) NOT NULL,
    `url` varchar (200) NOT NULL,
    PRIMARY KEY (`name`),
    UNIQUE KEY `name_UNIQUE` (`name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;