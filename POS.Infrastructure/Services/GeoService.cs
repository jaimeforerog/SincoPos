using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using POS.Application.DTOs;

namespace POS.Infrastructure.Services;

/// <summary>
/// Servicio para obtener información geográfica (países, departamentos, ciudades)
/// Usa REST Countries API y datos estáticos para ciudades de Colombia
/// </summary>
public class GeoService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GeoService> _logger;
    private const string CACHE_KEY_PAISES = "geo_paises";
    private static readonly TimeSpan CACHE_DURATION = TimeSpan.FromHours(24);

    // Ciudades principales de Colombia (top 50)
    private static readonly List<string> CiudadesColombia = new()
    {
        "Bogotá", "Medellín", "Cali", "Barranquilla", "Cartagena",
        "Cúcuta", "Bucaramanga", "Pereira", "Santa Marta", "Ibagué",
        "Pasto", "Manizales", "Neiva", "Villavicencio", "Armenia",
        "Valledupar", "Montería", "Popayán", "Sincelejo", "Tunja",
        "Florencia", "Riohacho", "Quibdó", "Yopal", "Mocoa",
        "Leticia", "Inírida", "Mitú", "Puerto Carreño", "San Andrés",
        "Soacha", "Bello", "Itagüí", "Soledad", "Palmira",
        "Buenaventura", "Floridablanca", "Tumaco", "Girardot", "Barrancabermeja",
        "Tuluá", "Dosquebradas", "Envigado", "Cartago", "Apartadó",
        "Rionegro", "Facatativá", "Zipaquirá", "Chía", "Fusagasugá"
    };

    public GeoService(HttpClient httpClient, IMemoryCache cache, ILogger<GeoService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
        _httpClient.BaseAddress = new Uri("https://restcountries.com/v3.1/");
    }

    /// <summary>
    /// Obtener todos los países
    /// </summary>
    public async Task<List<PaisDto>> ObtenerPaises()
    {
        // Verificar caché
        if (_cache.TryGetValue(CACHE_KEY_PAISES, out List<PaisDto>? paisesCache))
        {
            return paisesCache!;
        }

        try
        {
            // Llamar a REST Countries API
            var response = await _httpClient.GetAsync("all?fields=cca2,name,flag");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<List<JsonElement>>(json);

            var paises = data?.Select(p => new PaisDto(
                Iso2: p.GetProperty("cca2").GetString() ?? "",
                Nombre: p.GetProperty("name").GetProperty("common").GetString() ?? "",
                NombreNativo: p.GetProperty("name").GetProperty("common").GetString(),
                Emoji: p.TryGetProperty("flag", out var flag) ? flag.GetString() : null
            ))
            .OrderBy(p => p.Nombre)
            .ToList() ?? new List<PaisDto>();

            // Guardar en caché
            _cache.Set(CACHE_KEY_PAISES, paises, CACHE_DURATION);

            _logger.LogInformation("Paises cargados: {Count}", paises.Count);
            return paises;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener países de la API");

            // Fallback: retornar países principales de América Latina
            return GetPaisesFallback();
        }
    }

    /// <summary>
    /// Obtener ciudades de un país
    /// Implementado para países principales de Latinoamérica
    /// </summary>
    public Task<List<CiudadDto>> ObtenerCiudadesPorPais(string codigoPais)
    {
        var ciudadesDict = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["CO"] = CiudadesColombia,
            ["MX"] = new List<string>
            {
                "Ciudad de México", "Guadalajara", "Monterrey", "Puebla", "Tijuana",
                "León", "Juárez", "Zapopan", "Mérida", "Cancún",
                "Querétaro", "Aguascalientes", "Hermosillo", "Saltillo", "Mexicali",
                "Culiacán", "Acapulco", "Veracruz", "Toluca", "Morelia"
            },
            ["AR"] = new List<string>
            {
                "Buenos Aires", "Córdoba", "Rosario", "Mendoza", "La Plata",
                "San Miguel de Tucumán", "Mar del Plata", "Salta", "Santa Fe", "San Juan",
                "Resistencia", "Neuquén", "Posadas", "Bahía Blanca", "Paraná"
            },
            ["PE"] = new List<string>
            {
                "Lima", "Arequipa", "Trujillo", "Chiclayo", "Piura",
                "Cusco", "Iquitos", "Huancayo", "Tacna", "Pucallpa",
                "Cajamarca", "Chimbote", "Ica", "Ayacucho", "Juliaca"
            },
            ["CL"] = new List<string>
            {
                "Santiago", "Valparaíso", "Concepción", "La Serena", "Antofagasta",
                "Temuco", "Rancagua", "Talca", "Arica", "Puerto Montt",
                "Coquimbo", "Osorno", "Valdivia", "Punta Arenas", "Iquique"
            },
            ["EC"] = new List<string>
            {
                "Quito", "Guayaquil", "Cuenca", "Santo Domingo", "Machala",
                "Manta", "Portoviejo", "Ambato", "Riobamba", "Loja",
                "Esmeraldas", "Ibarra", "Latacunga", "Milagro", "Babahoyo"
            },
            ["VE"] = new List<string>
            {
                "Caracas", "Maracaibo", "Valencia", "Barquisimeto", "Maracay",
                "Ciudad Guayana", "Maturín", "Barcelona", "Cumaná", "Mérida",
                "San Cristóbal", "Puerto La Cruz", "Barinas", "Punto Fijo", "Los Teques"
            },
            ["PA"] = new List<string>
            {
                "Ciudad de Panamá", "San Miguelito", "Tocumen", "David", "Arraiján",
                "Colón", "Las Cumbres", "La Chorrera", "Pacora", "Santiago de Veraguas"
            },
            ["CR"] = new List<string>
            {
                "San José", "Alajuela", "Cartago", "Heredia", "Limón",
                "Puntarenas", "Liberia", "Paraíso", "Pérez Zeledón", "San Isidro"
            },
            ["UY"] = new List<string>
            {
                "Montevideo", "Salto", "Paysandú", "Las Piedras", "Rivera",
                "Maldonado", "Tacuarembó", "Melo", "Mercedes", "Artigas"
            },
            ["PY"] = new List<string>
            {
                "Asunción", "Ciudad del Este", "San Lorenzo", "Luque", "Capiatá",
                "Lambaré", "Fernando de la Mora", "Limpio", "Ñemby", "Encarnación"
            },
            ["BO"] = new List<string>
            {
                "La Paz", "Santa Cruz de la Sierra", "Cochabamba", "Sucre", "Oruro",
                "Potosí", "Tarija", "El Alto", "Montero", "Trinidad"
            },
            ["BR"] = new List<string>
            {
                "São Paulo", "Rio de Janeiro", "Brasília", "Salvador", "Fortaleza",
                "Belo Horizonte", "Manaus", "Curitiba", "Recife", "Porto Alegre",
                "Belém", "Goiânia", "Guarulhos", "Campinas", "São Luís"
            },
            ["US"] = new List<string>
            {
                "New York", "Los Angeles", "Chicago", "Houston", "Phoenix",
                "Philadelphia", "San Antonio", "San Diego", "Dallas", "San Jose",
                "Miami", "Atlanta", "Boston", "Seattle", "Denver"
            },
            ["ES"] = new List<string>
            {
                "Madrid", "Barcelona", "Valencia", "Sevilla", "Zaragoza",
                "Málaga", "Murcia", "Palma", "Las Palmas", "Bilbao",
                "Alicante", "Córdoba", "Valladolid", "Vigo", "Gijón"
            }
        };

        if (ciudadesDict.TryGetValue(codigoPais, out var ciudadesList))
        {
            var ciudades = ciudadesList
                .Select(c => new CiudadDto(c, codigoPais, null, null))
                .OrderBy(c => c.Nombre)
                .ToList();

            return Task.FromResult(ciudades);
        }

        // Para otros países, retornar lista vacía (el usuario podrá escribir libremente)
        return Task.FromResult(new List<CiudadDto>());
    }

    private static List<PaisDto> GetPaisesFallback()
    {
        return new List<PaisDto>
        {
            new("CO", "Colombia", "Colombia", "🇨🇴"),
            new("US", "Estados Unidos", "United States", "🇺🇸"),
            new("MX", "México", "México", "🇲🇽"),
            new("AR", "Argentina", "Argentina", "🇦🇷"),
            new("BR", "Brasil", "Brasil", "🇧🇷"),
            new("CL", "Chile", "Chile", "🇨🇱"),
            new("PE", "Perú", "Perú", "🇵🇪"),
            new("EC", "Ecuador", "Ecuador", "🇪🇨"),
            new("VE", "Venezuela", "Venezuela", "🇻🇪"),
            new("PA", "Panamá", "Panamá", "🇵🇦"),
            new("CR", "Costa Rica", "Costa Rica", "🇨🇷"),
            new("UY", "Uruguay", "Uruguay", "🇺🇾"),
            new("PY", "Paraguay", "Paraguay", "🇵🇾"),
            new("BO", "Bolivia", "Bolivia", "🇧🇴"),
            new("ES", "España", "España", "🇪🇸"),
        };
    }
}
