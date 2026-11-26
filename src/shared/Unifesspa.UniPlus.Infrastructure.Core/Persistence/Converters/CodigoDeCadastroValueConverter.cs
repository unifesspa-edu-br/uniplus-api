namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

/// <summary>Confere o formato do código de cadastro na leitura do banco.</summary>
public sealed class CodigoDeCadastroValueConverter : ValueConverter<string, string>
{
    public CodigoDeCadastroValueConverter() : base(codigo => codigo, valor => Ler(valor)) { }

    private static string Ler(string valor) => FormatoDoCodigo.TemFormatoValido(valor)
        ? valor
        : throw new InvalidOperationException("Código de cadastro no banco está fora do formato esperado.");
}
