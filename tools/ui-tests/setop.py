import sqlite3,sys
c=sqlite3.connect(sys.argv[1])
c.execute("insert into Settings(SettingKey,SettingValue) values('PanelOpacity',?) on conflict(SettingKey) do update set SettingValue=excluded.SettingValue",(sys.argv[2],))
c.commit()
