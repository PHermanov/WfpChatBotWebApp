﻿namespace WfpChatBotWebApp.Persistence.Entities;

public class Sticker
{
    public string Name { get; set; } = string.Empty;
    public string StickerSet { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}