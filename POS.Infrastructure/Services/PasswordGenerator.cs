using System.Security.Cryptography;

namespace POS.Infrastructure.Services;

internal static class PasswordGenerator
{
    private const string Mayusculas = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Minusculas = "abcdefghjkmnpqrstuvwxyz";
    private const string Numeros = "23456789";
    private const string Simbolos = "!@#$%&*+-=?";
    private const string Todos = Mayusculas + Minusculas + Numeros + Simbolos;

    public static string Generate(int largo = 16)
    {
        if (largo < 8) largo = 8;
        Span<byte> bytes = stackalloc byte[largo * 2];
        RandomNumberGenerator.Fill(bytes);

        var chars = new char[largo];
        chars[0] = Mayusculas[bytes[0] % Mayusculas.Length];
        chars[1] = Minusculas[bytes[1] % Minusculas.Length];
        chars[2] = Numeros[bytes[2] % Numeros.Length];
        chars[3] = Simbolos[bytes[3] % Simbolos.Length];
        for (int i = 4; i < largo; i++)
            chars[i] = Todos[bytes[i] % Todos.Length];

        for (int i = largo - 1; i > 0; i--)
        {
            var j = bytes[largo + i] % (i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
        return new string(chars);
    }
}
