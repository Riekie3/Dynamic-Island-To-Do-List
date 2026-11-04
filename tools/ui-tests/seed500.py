import sqlite3, sys, datetime as dt
c = sqlite3.connect(sys.argv[1])
now = dt.datetime.utcnow()
for i in range(500):
    created = (now - dt.timedelta(minutes=i)).strftime('%Y-%m-%dT%H:%M:%S.0000000Z')
    c.execute("insert into Tasks(ListId,Title,Priority,IsCompleted,CreatedAt,UpdatedAt,SortOrder) values(?,?,?,0,?,?,?)",
              (1 + i % 2, 'Task %03d' % i, i % 3, created, created, i))
c.commit()
print(c.execute('select count(*) from Tasks').fetchone()[0], 'tasks')

