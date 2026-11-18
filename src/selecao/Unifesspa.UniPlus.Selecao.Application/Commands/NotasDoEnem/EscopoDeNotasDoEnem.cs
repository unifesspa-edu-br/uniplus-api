namespace Unifesspa.UniPlus.Selecao.Application.Commands.NotasDoEnem;

using Abstractions;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;
using Domain.Leiautes;
using Domain.Services;
using Domain.ValueObjects;

using Kernel.Results;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Regras.Services;

/// <summary>
/// O processo, a etapa de nota do ENEM e a edição que a operação alcança, com o edital divulgado que
/// os confirmou: a versão e o grafo reidratado dela, de onde saem as ofertas e os pesos da apuração.
/// A janela de inscrição só é resolvida para quem grava; a consulta de histórico a deixa nula.
/// </summary>
public sealed record EscopoDeNotasDoEnem(
    Guid ProcessoSeletivoId,
    Guid EtapaProcessoId,
    string EdicaoCodigo,
    Guid VersaoConfiguracaoId,
    ConfiguracaoPublicada Configuracao,
    JanelaDaInscricao? Inscricao);

/// <summary>
/// Resolve e confere o escopo do envio ao INEP e da importação das notas pela versão divulgada do
/// certame, e não pelo processo vivo: a edição ou a etapa só mudam no rascunho, e os candidatos
/// inscrevem-se na versão que o público vê.
/// Processo sem versão divulgada é recusado.
/// </summary>
public sealed class ResolvedorDoEscopoDeNotasDoEnem(
    ICertameDivulgadoRepository certameDivulgadoRepository,
    IProcessoSeletivoRepository processoSeletivoRepository,
    IRegistroCodecsEnvelope registroCodecs)
{
    /// <param name="exigeNotaDoEnem">
    /// Verdadeiro para quem grava (envio e importação). A consulta de histórico dispensa: listar o
    /// que há numa etapa que não recebe nota do ENEM devolve só uma lista vazia.
    /// </param>
    public async Task<Result<EscopoDeNotasDoEnem>> ResolverAsync(
        Guid processoSeletivoId, Guid etapaProcessoId, string edicaoCodigo,
        bool exigeNotaDoEnem, CancellationToken cancellationToken)
    {
        Result<VersaoDivulgada> divulgada = await VersaoDivulgadaResolver.ResolverAsync(
            processoSeletivoId, certameDivulgadoRepository, processoSeletivoRepository, cancellationToken).ConfigureAwait(false);
        if (divulgada.IsFailure)
        {
            return Result<EscopoDeNotasDoEnem>.Failure(divulgada.Error!);
        }
        Result<EnvelopeReidratado> envelope = ContextoDaVersaoFixadaResolver.Reidratar(divulgada.Value!.Versao, registroCodecs);
        return envelope.IsFailure
            ? Result<EscopoDeNotasDoEnem>.Failure(envelope.Error!)
            : Montar(processoSeletivoId, etapaProcessoId, edicaoCodigo, exigeNotaDoEnem, divulgada.Value.Versao.Id, envelope.Value!);
    }

    private static Result<EscopoDeNotasDoEnem> Montar(
        Guid processoId, Guid etapaId, string edicao, bool exigeNota, Guid versaoId, EnvelopeReidratado envelope)
    {
        if (ConferirEtapaEEdicao(envelope.Configuracao, etapaId, edicao, exigeNota) is { } recusa)
        {
            return Result<EscopoDeNotasDoEnem>.Failure(recusa);
        }
        // O histórico também atende a processos sem inscrição pelo sistema.
        Result<JanelaDaInscricao>? janela = exigeNota ? JanelaDaInscricao.Da(envelope.Configuracao, envelope.Dados) : null;
        return janela is { IsFailure: true }
            ? Result<EscopoDeNotasDoEnem>.Failure(janela.Error!)
            : Result<EscopoDeNotasDoEnem>.Success(new(processoId, etapaId, edicao, versaoId, envelope.Configuracao, janela?.Value));
    }

    private static DomainError? ConferirEtapaEEdicao(ConfiguracaoPublicada configuracaoPublicada, Guid etapaId, string edicao, bool exigeNota)
    {
        EtapaProcesso? etapa = configuracaoPublicada.Avaliacao.Etapas.FirstOrDefault(e => e.Id == etapaId);
        if (etapa is null)
        {
            return new(NotasDoEnemErrorCodes.EtapaNaoEncontrada, "A etapa informada não pertence a este processo seletivo.");
        }
        if (exigeNota && !etapa.DeclaraNotaDoEnem)
        {
            return new(NotasDoEnemErrorCodes.EtapaSemNotaDoEnem, "A etapa informada não recebe a nota do ENEM.");
        }
        return configuracaoPublicada.Coleta.OpcoesDeclaradas.Any(o => string.Equals(o.FatoCodigo, EdicaoDoEnem.Fato, StringComparison.Ordinal)
            && string.Equals(o.Codigo, edicao, StringComparison.Ordinal))
            ? null : new(NotasDoEnemErrorCodes.EdicaoNaoAdmitida, "A edição informada não é uma das edições do ENEM que o processo admite.");
    }

    /// <summary>
    /// Recusa o envio e a importação enquanto o período de inscrição da versão divulgada não terminou:
    /// até lá o candidato ainda troca a edição do ENEM. O período é o da divulgada, que a retificação
    /// que prorroga as inscrições já atualizou.
    /// </summary>
    public static DomainError? ConferirInscricoesEncerradas(
        EscopoDeNotasDoEnem escopo, DateTimeOffset agora, IResolvedorFusoInstitucional resolvedorFuso)
    {
        ArgumentNullException.ThrowIfNull(escopo);
        ArgumentNullException.ThrowIfNull(resolvedorFuso);

        if (escopo.Inscricao is null)
        {
            throw new InvalidOperationException("O escopo da consulta não resolve a janela de inscrição; só o de quem grava a resolve.");
        }

        Result<TimeZoneInfo> fuso = resolvedorFuso.Resolver();
        return fuso.IsFailure
            ? fuso.Error
            : escopo.Inscricao.ConferirEncerrada(
                agora, fuso.Value!, NotasDoEnemErrorCodes.InscricoesEmAndamento,
                "O envio ao INEP e a importação das notas do ENEM só são feitos depois do encerramento.");
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
