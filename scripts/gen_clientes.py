import random
import os

nombres_hombres = ['Carlos', 'Andres', 'Juan', 'Diego', 'Sebastian', 'Luis', 'Jorge', 'Alejandro', 'Gabriel', 'Miguel', 'Jose', 'Fernando', 'Ricardo', 'Oscar', 'David', 'Hector', 'Camilo', 'Mauricio', 'Felipe', 'Eduardo']
nombres_mujeres = ['Maria', 'Paula', 'Andrea', 'Carolina', 'Daniela', 'Natalia', 'Laura', 'Diana', 'Camila', 'Catalina', 'Valentina', 'Sofia', 'Isabella', 'Lucia', 'Mariana', 'Gabriela', 'Valeria', 'Juliana', 'Margarita', 'Luz']
apellidos = ['Gomez', 'Rodriguez', 'Perez', 'Garcia', 'Martinez', 'Lopez', 'Gonzalez', 'Hernandez', 'Ramirez', 'Sanchez', 'Diaz', 'Torres', 'Ruiz', 'Flores', 'Restrepo', 'Mejia', 'Jaramillo', 'Castillo', 'Vargas', 'Rojas', 'Orozco', 'Rendon']
empresas = ['Soluciones Tecnologicas S.A.S', 'Inversiones ABC', 'Constructora del Norte', 'Logistica Express', 'Comercializadora Global LTDA', 'Servicios Integrales S.A.', 'Consultores Empresariales', 'Textiles y Modas S.A.S', 'Agroindustrias del Sur', 'Distribuidora Central']

ciudades = ['Bogotá', 'Medellín', 'Cali', 'Barranquilla', 'Bucaramanga', 'Cartagena', 'Pereira', 'Manizales', 'Santa Marta', 'Cúcuta']
tipos_calle = ['Calle', 'Carrera', 'Avenida', 'Transversal', 'Diagonal']

def generar_telefono():
    prefijos = ['300', '301', '310', '311', '312', '313', '314', '320', '321', '350']
    return f"{random.choice(prefijos)}{random.randint(1000000, 9999999)}"

sql_lines = [
    "-- ==============================================================================",
    "-- Script de Seed de Datos: 100 Clientes con datos reales",
    "-- EmpresaId = 1",
    "-- ==============================================================================",
    "",
    "INSERT INTO public.terceros (",
    '    tipo_identificacion, identificacion, nombre, tipo_tercero,',
    '    telefono, email, direccion, ciudad, origen_datos, activo,',
    '    fecha_creacion, "CreadoPor", perfil_tributario, es_gran_contribuyente,',
    '    es_autorretenedor, es_responsable_iva, "EmpresaId"',
    ") VALUES "
]

values_list = []

# Generate 80 Personas Naturales
for i in range(80):
    is_hombre = random.choice([True, False])
    nombre_pila = f"{random.choice(nombres_hombres)}" if is_hombre else f"{random.choice(nombres_mujeres)}"
    apellido1 = random.choice(apellidos)
    apellido2 = random.choice(apellidos)
    nombre_completo = f"{nombre_pila} {apellido1} {apellido2}"
    
    identificacion = f"{random.randint(10000000, 1199999999)}"
    telefono = generar_telefono()
    email = f"{nombre_pila.lower()}.{apellido1.lower()}{random.randint(1,99)}@gmail.com"
    direccion = f"{random.choice(tipos_calle)} {random.randint(1, 150)} # {random.randint(1, 100)} - {random.randint(1, 99)}"
    ciudad = random.choice(ciudades)
    
    values_list.append(f"(0, '{identificacion}', '{nombre_completo}', 0, '{telefono}', '{email}', '{direccion}', '{ciudad}', 0, true, NOW(), 'seed_100_clientes', 'PERSONA_NATURAL', false, false, false, 1)")

# Generate 20 Empresas (Jurídicas)
for i in range(20):
    nombre_completo = f"{random.choice(empresas)} {i+1}"
    identificacion = f"901{random.randint(100000, 999999)}"
    telefono = generar_telefono()
    domain = nombre_completo.split()[0].lower() + "-test.com"
    email = f"contacto@{domain}"
    direccion = f"{random.choice(tipos_calle)} {random.randint(1, 150)} # {random.randint(1, 100)} - {random.randint(1, 99)}"
    ciudad = random.choice(ciudades)
    
    values_list.append(f"(1, '{identificacion}', '{nombre_completo}', 0, '{telefono}', '{email}', '{direccion}', '{ciudad}', 0, true, NOW(), 'seed_100_clientes', 'REGIMEN_COMUN', false, false, true, 1)")

sql_content = "\n".join(sql_lines) + "\n" + ",\n".join(values_list) + "\nON CONFLICT (identificacion) DO NOTHING;\n"

output_path = r"c:\Users\jaime.forero\RiderProjects\SincoPos\seed_100_clientes_reales.sql"
with open(output_path, "w", encoding="utf-8") as f:
    f.write(sql_content)

print(f"File {output_path} generated successfully.")
