SELECT setval(pg_get_serial_sequence('public.categorias', 'Id'),
    (SELECT COALESCE(MAX("Id"),0) FROM public.categorias) + 1, false);
