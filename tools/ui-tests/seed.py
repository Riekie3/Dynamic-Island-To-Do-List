import sqlite3, sys, datetime as dt
db = sys.argv[1]
c = sqlite3.connect(db)
now = dt.datetime.utcnow()
loc = dt.datetime.now()
def iso(d): return d.strftime('%Y-%m-%dT%H:%M:%S.0000000Z')
def due(d, t=True): return d.strftime('%Y-%m-%dT%H:%M:%S') if t else d.strftime('%Y-%m-%dT00:00:00')
c.execute("insert into Lists(Name,ColorHex,SortOrder,CreatedAt,UpdatedAt) values('Project Alpha','#22C1C3',2,?,?)", (iso(now), iso(now)))
rows = [
  # list, title, prio, done, due, hastime
  (2, 'Finish project report', 0, 0, loc.replace(hour=17, minute=0, second=0, microsecond=0) + dt.timedelta(days=0 if loc.hour < 17 else 1), 1),
  (1, 'Renew car insurance', 0, 0, loc - dt.timedelta(days=1), 0),
  (2, 'Prepare slides for Monday', 1, 0, loc + dt.timedelta(days=3), 0),
  (1, 'Call the dentist', 1, 0, None, 0),
  (3, 'Review pull requests', 1, 0, None, 0),
  (1, 'Buy wedding gift', 2, 1, None, 0),
  (1, 'Water the plants', 2, 0, loc + dt.timedelta(days=1), 0),
]
for i, (lst, title, pr, done, d, ht) in enumerate(rows):
    created = now - dt.timedelta(hours=10 - i)
    c.execute("""insert into Tasks(ListId,Title,Priority,IsCompleted,CreatedAt,UpdatedAt,CompletedAt,DueDate,DueHasTime,SortOrder)
                 values(?,?,?,?,?,?,?,?,?,?)""",
              (lst, title, pr, done, iso(created), iso(created), iso(now - dt.timedelta(hours=1)) if done else None,
               due(d, bool(ht)) if d else None, ht, i))
c.commit()
print('seeded')

