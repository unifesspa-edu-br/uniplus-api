namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O que o Representante do controlador leu do circuito de aprovação do item antes de decidir: o ciclo e quantas
/// manifestações técnicas o ciclo tinha. A decisão só vale sobre essa leitura: a manifestação registrada, ou o reinício
/// do ciclo, entre a leitura e o envio a torna recusada por conflito (UNI-REQ-0374).
/// </summary>
/// <param name="Ciclo">O ciclo de aprovação lido no item.</param>
/// <param name="ManifestacoesNoCiclo">Quantas manifestações o ciclo lido tinha.</param>
public readonly record struct LeituraDoCircuito(CicloDaAprovacao Ciclo, int ManifestacoesNoCiclo)
{
    private static readonly DomainError ManifestacaoNaoLida = new(
        ProtecaoDoCatalogoErrorCodes.ManifestacaoNaoLida, "Há manifestação técnica registrada depois da leitura; releia o item.");

    /// <summary>
    /// A leitura informada, ou <see langword="null"/> com a recusa no lote (ADR-0125): o ciclo ausente ou menor que 1 no
    /// campo <c>ciclo</c>, e a contagem ausente ou negativa no campo <c>manifestacoesNoCiclo</c>.
    /// </summary>
    public static LeituraDoCircuito? Ler(int? ciclo, int? manifestacoesNoCiclo, List<FieldError> erros)
    {
        ArgumentNullException.ThrowIfNull(erros);

        CicloDaAprovacao? lido = CicloDaAprovacao.Ler(ciclo, erros);
        if (manifestacoesNoCiclo is not >= 0)
        {
            erros.Add(new("manifestacoesNoCiclo", new DomainError(
                ProtecaoDoCatalogoErrorCodes.ManifestacoesNoCicloInvalida, "Informe quantas manifestações o item tinha quando foi lido.")));
            return null;
        }

        return lido is { } valido ? new LeituraDoCircuito(valido, manifestacoesNoCiclo.Value) : null;
    }

    /// <summary>
    /// Confere a leitura contra o <paramref name="circuito"/>: o ciclo e a contagem iguais aos gravados. Senão, a falha
    /// <c>ManifestacaoNaoLida</c>, sem campo, que pede a releitura.
    /// </summary>
    public Result ConferirCom(AprovacaoDaProtecao circuito)
    {
        ArgumentNullException.ThrowIfNull(circuito);

        return Ciclo == circuito.Ciclo && ManifestacoesNoCiclo == circuito.ManifestacoesNoCiclo
            ? Result.Success()
            : Result.Failure(ManifestacaoNaoLida);
    }
}
