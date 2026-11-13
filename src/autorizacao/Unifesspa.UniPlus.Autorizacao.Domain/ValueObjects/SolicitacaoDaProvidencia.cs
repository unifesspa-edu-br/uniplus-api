namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O que o Encarregado de dados pede sobre uma concessão: o tipo, retirada ou revisão, e o fundamento, como o corpo da
/// requisição os traz. Juntos são o pedido; o agregado os confere ao abrir a providência (UNI-REQ-0375).
/// </summary>
/// <param name="Tipo">O código do tipo: <c>retirada</c> ou <c>revisao</c>.</param>
/// <param name="Fundamento">Por que a providência é pedida.</param>
public sealed record SolicitacaoDaProvidencia(string? Tipo, string? Fundamento)
{
    /// <summary>Tamanho mínimo do fundamento, em caracteres, depois de aparado.</summary>
    public const int FundamentoMinLength = 10;

    /// <summary>Tamanho máximo do fundamento, em caracteres (runas Unicode), também o da coluna que o guarda.</summary>
    public const int FundamentoMaxLength = 2000;

    /// <summary>O tipo informado, ou nulo quando ausente ou desconhecido.</summary>
    internal TipoDaProvidencia? TipoLido => TipoDaProvidenciaCodigo.DoCodigo(Tipo);

    /// <summary>
    /// O fundamento aparado, ou nulo quando ausente, fora dos limites ou com o caractere nulo, que a coluna textual do banco
    /// não armazena.
    /// </summary>
    internal string? FundamentoLido
    {
        get
        {
            string? aparado = Fundamento?.Trim();

            return aparado is not null
                && !aparado.Contains('\0', StringComparison.Ordinal)
                && Caracteres(aparado) is >= FundamentoMinLength and <= FundamentoMaxLength
                ? aparado
                : null;
        }
    }

    /// <summary>Acrescenta ao lote as recusas do tipo e do fundamento, sem parar na primeira (ADR-0125).</summary>
    internal void Validar(List<FieldError> erros)
    {
        if (TipoLido is null)
        {
            erros.Add(new("tipo", new DomainError(ProvidenciaErrorCodes.TipoInvalido, "O tipo da providência é obrigatório: use retirada ou revisao.")));
        }

        if (FundamentoLido is null)
        {
            erros.Add(new("fundamento", new DomainError(
                ProvidenciaErrorCodes.FundamentoInvalido,
                $"O fundamento é obrigatório e deve ter entre {FundamentoMinLength} e {FundamentoMaxLength} caracteres.")));
        }
    }

    // O limite conta caracteres, como a coluna do banco, e não unidades UTF-16: o caractere fora do plano básico (um emoji,
    // por exemplo) conta 1, e não 2.
    private static int Caracteres(string texto) => texto.EnumerateRunes().Count();
}
