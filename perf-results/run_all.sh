cd /d/Rie/Claude/TaskIsland/tools/ui-tests
R=/d/Rie/Claude/TaskIsland/perf-results
powershell -NoProfile -ExecutionPolicy Bypass -File perf.ps1 -Mode cycles -Companion Off > $R/cycles_default.txt 2>&1
DOTNET_TieredCompilation=0 powershell -NoProfile -ExecutionPolicy Bypass -File perf.ps1 -Mode cycles -Companion Off > $R/cycles_tc0.txt 2>&1
powershell -NoProfile -ExecutionPolicy Bypass -File p6a.ps1 > $R/p6a.txt 2>&1
powershell -NoProfile -ExecutionPolicy Bypass -File perf.ps1 -Mode big -Companion Off -Seconds 1800 > $R/big30.txt 2>&1
echo ALLDONE > $R/done.txt
