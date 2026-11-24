namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

internal static class FormaDoCodigoDeAreaCitada
{
    internal static string Descricao => $"um código de até {GrupoPesoAreaEnemCongelado.AreaCodigoMaxLength} caracteres, com letras maiúsculas sem acento, algarismos e sublinhado";
    internal static bool Aceita(string? codigo) => !string.IsNullOrEmpty(codigo)
        && codigo.Length <= GrupoPesoAreaEnemCongelado.AreaCodigoMaxLength
        && codigo.All(static c => c is (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_');
}
