namespace Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

/// <summary>
/// A forma do rótulo opaco que nomeia o tipo da entidade a que um ato se vincula (ADR-0105):
/// letras maiúsculas e dígitos em grupos separados por <c>_</c>. Fixa uma grafia canônica — sem ela,
/// <c>PROCESSO_SELETIVO</c> e <c>processo-seletivo</c> seriam objetos distintos na consulta, e a mesma
/// entidade se partiria em duas. Verifica a forma, jamais o valor: o módulo não sabe o que o rótulo significa.
/// </summary>
public static class TipoDaEntidadeVinculada
{
    /// <summary>Máximo de caracteres do rótulo, como recebido.</summary>
    public const int TamanhoMaximo = 60;

    /// <summary>Gramática compartilhada para a restrição no banco.</summary>
    public const string Padrao = FormatoDoCodigo.ExpressaoNoBanco;

    /// <summary>Verdadeiro quando o valor segue o formato compartilhado.</summary>
    public static bool TemFormaCanonica(string? valor) =>
        valor is not null && FormatoDoCodigo.TemFormatoValido(valor);

    /// <summary>
    /// O rótulo recebido, ou a exceção de última linha quando está em branco, passa de
    /// <see cref="TamanhoMaximo"/> ou foge da forma canônica. O validator recusa o mesmo antes, com 422.
    /// </summary>
    internal static string Conferido(string? entidadeTipo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entidadeTipo);
        string tipo = entidadeTipo;
        if (tipo.Length > TamanhoMaximo)
        {
            throw new ArgumentException($"Tipo da entidade deve ter no máximo {TamanhoMaximo} caracteres.", nameof(entidadeTipo));
        }

        if (!TemFormaCanonica(tipo))
        {
            throw new ArgumentException("Tipo da entidade deve começar com letra maiúscula e conter apenas letras maiúsculas sem acento, algarismos e sublinhado entre palavras.", nameof(entidadeTipo));
        }

        return tipo;
    }
}
