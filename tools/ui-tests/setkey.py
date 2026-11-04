import sqlite3, sys
# usage: setkey.py <db> <key> <value>   (upserts one row of the Settings table)
c = sqlite3.connect(sys.argv[1])
c.execute("insert into Settings(SettingKey,SettingValue) values(?,?) on conflict(SettingKey) do update set SettingValue=excluded.SettingValue", (sys.argv[2], sys.argv[3]))
c.commit()
