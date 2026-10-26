namespace Unifesspa.UniPlus.Selecao.Application.Commands.NotasDoEnem;

using Abstractions;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;
using Domain.Leiautes;
using Domain.ValueObjects;

using Kernel.Results;

using Services;

using Unifesspa.UniPlus.Regras.Services;

/// <summary>
/// O processo, a etapa de nota do ENEM e a edição que a operação alcança, com o edital divulgado que
/// os confirmou: a versão e o grafo reidratado dela, de onde saem as ofertas e os pesos da apuração.
/// </summary>
public sealed record EscopoDeNotasDoEnem(
    Guid ProcessoSeletivoId,
    Guid EtapaProcessoId,
    string EdicaoCodigo,
    Guid VersaoConfiguracaoId,
    GrafoConfiguracao Grafo);

/// <summary>
/// Resolve e confere o escopo do envio ao INEP e da importação das notas pela versão divulgada do
/// certame, e não pelo processo vivo: uma sessão de retificação aberta pode retirar a edição ou
/// trocar a etapa no rascunho enquanto os candidatos já se inscreveram na versão que o público vê.
/// Processo sem versão divulgada é recusado.
/// </summary>
public static class ResolvedorDoEscopoDeNotasDoEnem
{
    /// <param name="exigeNotaDoEnem">
    /// Verdadeiro para quem grava (envio e importação). A consulta de histórico dispensa: listar o
    /// que há numa etapa que não recebe nota do ENEM devolve só uma lista vazia.
    /// </param>
    public static async Task<Result<EscopoDeNotasDoEnem>> ResolverAsync(
        Guid processoSeletivoId,
        Guid etapaProcessoId,
        string edicaoCodigo,
        bool exigeNotaDoEnem,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        CancellationToken cancellationToken)
    {
        Result<VersaoDivulgada> divulgada = await VersaoDivulgadaResolver
            .ResolverAsync(processoSeletivoId, certameDivulgadoRepository, processoSeletivoRepository, cancellationToken)
            .ConfigureAwait(false);
        if (divulgada.IsFailure)
        {
            return Result<EscopoDeNotasDoEnem>.Failure(divulgada.Error!);
        }

        Result<EnvelopeReidratado> envelope = ContextoDaVersaoFixadaResolver.Reidratar(divulgada.Value!.Versao, registroCodecs);
        if (envelope.IsFailure)
        {
            return Result<EscopoDeNotasDoEnem>.Failure(envelope.Error!);
        }

        GrafoConfiguracao grafo = envelope.Value!.Grafo;

        EtapaProcesso? etapa = grafo.Etapas.FirstOrDefault(e => e.Id == etapaProcessoId);
        if (etapa is null)
        {
            return Falha<EscopoDeNotasDoEnem>(NotasDoEnemErrorCodes.EtapaNaoEncontrada, "A etapa informada não pertence a este processo seletivo.");
        }

        if (exigeNotaDoEnem && !etapa.DeclaraNotaDoEnem)
        {
            return Falha<EscopoDeNotasDoEnem>(NotasDoEnemErrorCodes.EtapaSemNotaDoEnem, "A etapa informada não recebe a nota do ENEM.");
        }

        bool edicaoAdmitida = grafo.OpcoesDeclaradas.Any(o =>
            string.Equals(o.FatoCodigo, EdicaoDoEnem.Fato, StringComparison.Ordinal)
            && string.Equals(o.Codigo, edicaoCodigo, StringComparison.Ordinal));
        if (!edicaoAdmitida)
        {
            return Falha<EscopoDeNotasDoEnem>(NotasDoEnemErrorCodes.EdicaoNaoAdmitida, "A edição informada não é uma das edições do ENEM que o processo admite.");
        }

        return Result<EscopoDeNotasDoEnem>.Success(new EscopoDeNotasDoEnem(processoSeletivoId, etapaProcessoId, edicaoCodigo, divulgada.Value!.Versao.Id, grafo));
    }

    /// <summary>O leiaute pelo código e versão, que precisa estar ativo e ser do tipo da operação.</summary>
    public static Result<ILeiauteDeArquivoDoInep> ObterLeiaute(string? codigo, string? versao, TipoDeLeiauteDoInep tipo)
    {
        ILeiauteDeArquivoDoInep? leiaute = CatalogoDeLeiautesDoInep.Padrao.Obter(codigo ?? string.Empty, versao ?? string.Empty, tipo);

        return leiaute is { Ativo: true }
            ? Result<ILeiauteDeArquivoDoInep>.Success(leiaute)
            : Falha<ILeiauteDeArquivoDoInep>(
                NotasDoEnemErrorCodes.LeiauteIndisponivel,
                "O leiaute informado não existe, foi desativado ou não é do tipo desta operação.");
    }

    private static Result<T> Falha<T>(string codigo, string mensagem) => Result<T>.Failure(new DomainError(codigo, mensagem));
}
