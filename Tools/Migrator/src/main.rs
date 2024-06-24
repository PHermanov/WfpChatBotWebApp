use rusqlite::{Connection, Result};

#[derive(Debug)]
struct PlayerSqlite {
    id: i64,
    chat_id: i64,
    user_id: i64,
    user_name: String,
    inactive: bool
}

fn main() -> Result<()> {
    // println!("Hello, world!");

    let conn = Connection::open("game.db")?;

    let mut stmt = conn.prepare("SELECT Id, ChatId, UserId, UserName, Inactive FROM Players")?;

    let players = stmt.query_map([], |row| {
        Ok(PlayerSqlite{
            id: row.get(0)?,
            chat_id: row.get(1)?,
            user_id: row.get(2)?,
            user_name: row.get(3)?,
            inactive: row.get(4)?
        })
    })?;

    for player in players {
        println!("Found player {:?}", player);
    }

    Ok(())
}
