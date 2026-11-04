import sqlite3, sys, datetime as dt
# Phase 5 reminder test data. usage: seed_p5.py <db> <lead-minutes|-> <pill-alert true|false|-> then task specs "list:title:seconds[:date]"
# seconds = due in N seconds (with a time); "date" as the 4th part = date-only task N days ahead (seconds ignored, used as days).
db, lead, pill = sys.argv[1], sys.argv[2], sys.argv[3]
c = sqlite3.connect(db)
now = dt.datetime.utcnow()
loc = dt.datetime.now().replace(microsecond=0)
def iso(d): return d.strftime('%Y-%m-%dT%H:%M:%S.0000000Z')
def due(d, with_time=True): return d.strftime('%Y-%m-%dT%H:%M:%S') if with_time else d.strftime('%Y-%m-%d')
def setting(k, v):
    c.execute("insert into Settings(SettingKey,SettingValue) values(?,?) on conflict(SettingKey) do update set SettingValue=excluded.SettingValue", (k, v))
if lead != '-': setting('ReminderLeadMinutes', lead)
if pill != '-': setting('ReminderPillAlert', pill)
for i, spec in enumerate(sys.argv[4:]):
    parts = spec.split(':')
    lst, title, n = int(parts[0]), parts[1], int(parts[2])
    date_only = len(parts) > 3 and parts[3] == 'date'
    d = (loc + dt.timedelta(days=n)) if date_only else (loc + dt.timedelta(seconds=n))
    c.execute("""insert into Tasks(ListId,Title,Priority,IsCompleted,CreatedAt,UpdatedAt,DueDate,DueHasTime,SortOrder)
                 values(?,?,1,0,?,?,?,?,?)""", (lst, title, iso(now), iso(now), due(d, not date_only), 0 if date_only else 1, i))
c.commit()
print('seeded', len(sys.argv[4:]))
