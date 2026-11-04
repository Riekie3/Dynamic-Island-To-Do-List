import sqlite3, sys, datetime as dt
# Phase 4 reminder test data: two tasks overdue (different lists), one due in N seconds, one due tomorrow.
db, soon = sys.argv[1], int(sys.argv[2])
c = sqlite3.connect(db)
now = dt.datetime.utcnow()
loc = dt.datetime.now().replace(microsecond=0)
def iso(d): return d.strftime('%Y-%m-%dT%H:%M:%S.0000000Z')
def due(d): return d.strftime('%Y-%m-%dT%H:%M:%S')
rows = [
  (1, 'Overdue one', loc - dt.timedelta(hours=2)),
  (2, 'Overdue two', loc - dt.timedelta(minutes=30)),
  (1, 'Soon task', loc + dt.timedelta(seconds=soon)),
  (2, 'Tomorrow task', loc + dt.timedelta(days=1)),
]
for i, (lst, title, d) in enumerate(rows):
    c.execute("""insert into Tasks(ListId,Title,Priority,IsCompleted,CreatedAt,UpdatedAt,DueDate,DueHasTime,SortOrder)
                 values(?,?,1,0,?,?,?,1,?)""", (lst, title, iso(now), iso(now), due(d), i))
c.commit()
print('seeded')
