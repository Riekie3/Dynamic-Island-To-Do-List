import sqlite3, sys
# Adds one task to a TaskIsland database and writes a consistent backup copy of it.
src, dest = sys.argv[1], sys.argv[2]
c = sqlite3.connect(src)
c.execute("insert into Tasks(ListId,Title,Priority,IsCompleted,CreatedAt,UpdatedAt,SortOrder) "
          "values(1,'Kept task',1,0,'2026-11-04T00:00:00.0000000Z','2026-11-04T00:00:00.0000000Z',0)")
c.commit()
b = sqlite3.connect(dest)
c.backup(b)
b.close()
c.close()
print('backup written')
