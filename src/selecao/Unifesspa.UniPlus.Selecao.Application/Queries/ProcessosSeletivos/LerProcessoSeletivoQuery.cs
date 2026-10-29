namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Text.Json.Nodes;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Kernel.Results;

using Wolverine.Attributes;

/// <summary>
/// A leitura do processo por quem o consulta: o que <see cref="ObterProcessoSeletivoQuery"/> devolve, menos o que ainda
/// não foi publicado para quem não configura o processo. "Não encontrado" é estado normal de leitura.
/// </summary>
public sealed record LerProcessoSeletivoQuery(Guid Id) : IQuery<ProcessoSeletivoDto?>;

/// <summary>
/// A configuração viva do processo é administrativa: traz blocos internos e, sob retificação, as edições da sessão que o
/// edital vigente ainda não tem. Quem não configura o processo na unidade dona recebe sempre um DTO montado só com a
/// identificação, e a localidade vem da versão que o certame divulgou — não da mais nova por tempo, que sob retificação ainda
/// não divulgada não tem publicidade. A montagem é por inclusão: campo novo do DTO nasce
/// vazio para esse leitor até alguém decidir expô-lo.
/// </summary>
public static class LerProcessoSeletivoQueryHandler
{
    /// <summary>
    /// <c>[NonTransactional]</c>: a decisão lê o <c>AutorizacaoDbContext</c> e o repositório, o <c>SelecaoDbContext</c>; o
    /// detector de transação do Wolverine recusa dois contextos. A consulta só lê.
    /// </summary>
    [NonTransactional]
    public static async Task<ProcessoSeletivoDto?> Handle(
        LerProcessoSeletivoQuery query,
        IProcessoSeletivoRepository processos,
        ICertameDivulgadoRepository certameDivulgado,
        IVerificadorDeAcesso verificador,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(processos);
        ArgumentNullException.ThrowIfNull(certameDivulgado);

        EstadoDoProcesso? estado = await processos.ObterEstadoAsync(query.Id, cancellationToken).ConfigureAwait(false);
        ProcessoSeletivoDto? processo = await ObterProcessoSeletivoQueryHandler
            .Handle(new ObterProcessoSeletivoQuery(query.Id), processos, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null || estado is null)
        {
            return null;
        }

        if (await AcessoAoProcesso.ConfiguraAsync(verificador, estado.UnidadeId, query.Id, cancellationToken).ConfigureAwait(false))
        {
            return processo;
        }

        // Sem divulgação (ou com ela ilegível), o leitor recebe a identificação sem localidade, como sem versão.
        Result<VersaoDivulgada> divulgada = await VersaoDivulgadaResolver
            .ResolverAsync(query.Id, certameDivulgado, processos, cancellationToken)
            .ConfigureAwait(false);

        return SoIdentificacao(processo, LocalidadePublicada(divulgada.IsSuccess ? divulgada.Value!.Versao : null));
    }

    private static ProcessoSeletivoDto SoIdentificacao(ProcessoSeletivoDto processo, LocalidadeRegenteDto localidade) => new(
        processo.Id,
        processo.Nome,
        IdentificadorLegivel: null,
        processo.TipoProcesso,
        processo.Status,
        processo.OrigemCandidatos,
        processo.UnidadeAdministradora,
        localidade,
        Etapas: [],
        OfertaAtendimento: null,
        DistribuicaoVagas: [],
        BonusRegional: null,
        AplicaBonusRegional: null,
        OpcoesDeCurso: null,
        Cascata: null,
        CriteriosDesempate: [],
        Classificacao: null,
        CronogramaFases: [],
        DocumentosExigidos: [],
        RaizesExigencia: [],
        ReferenciaTemporalFatos: null,
        RegrasDerivacao: [],
        Formularios: [],
        OpcoesDeclaradas: [],
        ConfiguracaoDivulgacao: null,
        ConfiguracaoTaxaInscricao: null,
        AlgoritmoContagemPrazo: null,
        processo.CriadoEm);

    // A localidade que a versão divulgada congelou. Envelope ausente ou ilegível, ou sem o bloco (versões antigas), devolve
    // o valor vazio: o leitor recebe a identificação sem localidade, nunca um erro nem a localidade da configuração viva.
    private static LocalidadeRegenteDto LocalidadePublicada(VersaoConfiguracao? vigente) =>
        EnvelopeDaVersao.Ler(vigente) is { } envelope
        && ProjecaoDoCertamePublicado.TentarObjeto(envelope, "localidade", out JsonObject? localidade)
        && ProjecaoDoCertamePublicado.TentarTexto(localidade, "codigoIbge", out string codigoIbge)
        && ProjecaoDoCertamePublicado.TentarTexto(localidade, "nome", out string nome)
        && ProjecaoDoCertamePublicado.TentarTexto(localidade, "uf", out string uf)
            ? new LocalidadeRegenteDto(codigoIbge, nome, uf)
            : new LocalidadeRegenteDto(string.Empty, string.Empty, string.Empty);
}
