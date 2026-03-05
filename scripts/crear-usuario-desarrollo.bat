@echo off
echo Creando usuario de desarrollo en la base de datos...
echo.
echo Por favor ingresa la contraseña de PostgreSQL cuando se solicite
echo.

"C:\Program Files\PostgreSQL\16\bin\psql.exe" -U postgres -d sincopos -f "%~dp0crear-usuario-desarrollo.sql"

echo.
echo Presiona cualquier tecla para continuar...
pause > nul
