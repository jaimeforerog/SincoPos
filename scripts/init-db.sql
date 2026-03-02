-- =============================================================
-- Script de inicialización para PostgreSQL local (desarrollo)
-- Ejecutar con: psql -U postgres -f init-db.sql
-- =============================================================

-- Crear la base de datos si no existe
SELECT 'CREATE DATABASE sincopos_dev'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'sincopos_dev')\gexec

-- Conectar a la base de datos
\c sincopos_dev

-- Crear extensiones
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";

-- Crear schema para Marten Event Store
CREATE SCHEMA IF NOT EXISTS events;

-- Permisos (public ya existe por defecto en PostgreSQL)
GRANT ALL PRIVILEGES ON SCHEMA events TO posuser;
GRANT ALL PRIVILEGES ON SCHEMA public TO posuser;
ALTER DEFAULT PRIVILEGES IN SCHEMA events GRANT ALL ON TABLES TO posuser;
ALTER DEFAULT PRIVILEGES IN SCHEMA events GRANT ALL ON SEQUENCES TO posuser;
