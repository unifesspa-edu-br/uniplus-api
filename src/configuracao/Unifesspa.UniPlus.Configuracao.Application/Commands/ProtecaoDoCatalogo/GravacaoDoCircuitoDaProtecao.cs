namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.CalendariosDiasUteis;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposProcesso;
using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A gravação da solicitação, da decisão e da manifestação no circuito de aprovação da proteção do catálogo de tipos
/// de documento e de fatos do candidato: todas mudam a linha do item, condicionada ao token de concorrência dela
/// (ADR-0163). A corrida perdida responde o mesmo
/// conflito, com nova tentativa possível depois de reler o item.
/// </summary>
/// <remarks>
/// Os endpoints têm Idempotency-Key (ADR-0119): a captura é local, com descarte, para o <c>SaveChangesAsync</c> do
/// outbox não reencontrar a entidade modificada. Os índices únicos da cadeia de manifestações são defesa em
/// profundidade: a mudança na linha do item já serializa as manifestações, mas o índice que disparar responde o mesmo
/// conflito, e não erro interno.
/// </remarks>
internal static class GravacaoDoCircuitoDaProtecao
{
    private const string IndiceDaManifestacaoInicial = RestricoesDoBanco.ManifestacaoDaProtecaoInicialDoAutorUnica;
    private const string IndiceDaSubstituicao = RestricoesDoBanco.ManifestacaoDaProtecaoSubstituicaoUnica;

    public static DomainError Conflito { get; } = new(
        ProtecaoDoCatalogoErrorCodes.ConflitoDeConcorrencia,
        "O tipo de documento ou fato do candidato foi alterado concorrentemente; releia o item e tente de novo.");

    public static async Task<Result> SalvarAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (OptimisticConcurrencyViolation.Is(ex)
            || UniqueConstraintViolation.EhConflito(ex, IndiceDaManifestacaoInicial)
            || UniqueConstraintViolation.EhConflito(ex, IndiceDaSubstituicao))
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(Conflito);
        }

        return Result.Success();
    }
}
