namespace Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;

/// <summary>
/// A forma do rótulo opaco que nomeia o tipo da entidade a que um ato se vincula (ADR-0105):
/// letras maiúsculas e dígitos em grupos separados por <c>_</c>. Fixa uma grafia canônica — sem ela,
/// <c>PROCESSO_SELETIVO</c> e <c>processo-seletivo</c> seriam objetos distintos na consulta, e a mesma
/// entidade se partiria em duas. Verifica a forma, jamais o valor: o módulo não sabe o que o rótulo significa.
/// </summary>
public static class TipoDaEntidadeVinculada
{
    private static readonly CampoDeCodigo Campo = new(
        "O tipo da entidade vinculada",
        TamanhoMaximo,
        AtoNormativoErrorCodes.VinculoTipoDeEntidadeInvalido,
        AtoNormativoErrorCodes.VinculoTipoDeEntidadeInvalido);

    /// <summary>Máximo de caracteres do rótulo, como recebido.</summary>
    public const int TamanhoMaximo = 60;

    /// <summary>Gramática compartilhada para a restrição no banco.</summary>
    public const string Padrao = FormatoDoCodigo.ExpressaoNoBanco;

    /// <summary>Verdadeiro quando o valor segue o formato compartilhado.</summary>
    public static bool TemFormaCanonica(string? valor) =>
        valor is not null && FormatoDoCodigo.TemFormatoValido(valor);

    /// <summary>
    /// A recusa do rótulo recebido, como chegou, quando está em branco, passa de <see cref="TamanhoMaximo"/>
    /// ou foge da forma canônica; nulo quando é aceitável.
    /// </summary>
    internal static FieldError? Recusa(string? entidadeTipo) => Campo.Recusa(entidadeTipo, "entidadeTipo");
}
