$psql = "C:\Program Files\PostgreSQL\16\bin\psql.exe"
$env:PGPASSWORD = "postgrade"
& $psql -h localhost -U postgres -c "\l" 2>&1
