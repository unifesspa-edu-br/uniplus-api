namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Text.Json;

using DTOs;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.ValueObjects;

public static class ObterProcessoSeletivoQueryHandler
{
    public static async Task<ProcessoSeletivoDto?> Handle(
        ObterProcessoSeletivoQuery query,
        IProcessoSeletivoRepository processoSeletivoRepository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterComConfiguracaoAsync(query.Id, cancellationToken)
            .ConfigureAwait(false);

        return processo is null ? null : Project(processo);
    }

    internal static ProcessoSeletivoDto Project(ProcessoSeletivo processo) => new(
        processo.Id,
        processo.Nome,
        processo.Tipo.ToString(),
        processo.Status.ToString(),
        processo.OrigemCandidatos.ToString(),
        [.. processo.Etapas
            .OrderBy(e => e.Ordem)
            .Select(e => new EtapaProcessoDto(e.Id, e.Nome, e.Carater.ToString(), e.Peso, e.NotaMinima, e.Ordem))],
        ProjectOfertaAtendimento(processo.OfertaAtendimento),
        [.. processo.DistribuicaoVagas.Select(ProjectDistribuicaoVagas)],
        ProjectBonusRegional(processo.BonusRegional),
        [.. processo.CriteriosDesempate.OrderBy(c => c.Ordem).Select(ProjectCriterioDesempate)],
        ProjectClassificacao(processo),
        [.. processo.CronogramaFases.OrderBy(f => f.Ordem).ThenBy(f => f.Id).Select(ProjectFaseCronograma)],
        processo.CreatedAt);

    private static OfertaAtendimentoEspecializadoDto? ProjectOfertaAtendimento(OfertaAtendimentoEspecializado? oferta)
    {
        if (oferta is null)
        {
            return null;
        }

        return new OfertaAtendimentoEspecializadoDto(
            oferta.Id,
            [.. oferta.Condicoes.Select(c => new OfertaCondicaoDto(c.Id, c.CondicaoOrigemId, c.CondicaoCodigo, c.CondicaoNome))],
            [.. oferta.Recursos.Select(r => new OfertaRecursoDto(r.Id, r.RecursoOrigemId, r.RecursoNome))],
            [.. oferta.TiposDeficiencia.Select(t => new OfertaTipoDeficienciaDto(t.Id, t.TipoDeficienciaOrigemId, t.TipoDeficienciaNome))]);
    }

    private static ConfiguracaoDistribuicaoVagasDto ProjectDistribuicaoVagas(ConfiguracaoDistribuicaoVagas configuracao) => new(
        configuracao.Id,
        configuracao.OfertaCursoOrigemId,
        configuracao.VoBase,
        configuracao.Pr,
        new ReferenciaRegraDto(configuracao.RegraDistribuicao.Codigo, configuracao.RegraDistribuicao.Versao, configuracao.RegraDistribuicao.Hash),
        configuracao.ReferenciaDemografica is { } demografica
            ? new ReferenciaReservaDemograficaSnapshotDto(
                demografica.OrigemId, demografica.CensoReferencia, demografica.PpiPercentual, demografica.QuilombolaPercentual, demografica.PcdPercentual, demografica.BaseLegal)
            : null,
        [.. configuracao.Modalidades.Select(m => new ModalidadeSelecionadaDto(
            m.Id, m.ModalidadeOrigemId, m.Codigo, m.Descricao, m.NaturezaLegal.ToString(), m.ComposicaoVagas.ToString(),
            m.ComposicaoOrigemCodigo, m.RegraRemanejamento.ToString(), m.RemanejamentoDestino, m.RemanejamentoPar, m.RemanejamentoFallback,
            m.CriteriosCumulativos, m.AcaoQuandoIndeferido, m.BaseLegal))]);

    private static ConfiguracaoBonusRegionalDto? ProjectBonusRegional(ConfiguracaoBonusRegional? bonus)
    {
        if (bonus is null)
        {
            return null;
        }

        return new ConfiguracaoBonusRegionalDto(
            bonus.Id,
            new ReferenciaRegraDto(bonus.Regra.Codigo, bonus.Regra.Versao, bonus.Regra.Hash),
            bonus.Fator,
            bonus.Teto,
            bonus.MunicipioConvenio,
            bonus.BaseLegal);
    }

    private static CriterioDesempateDto ProjectCriterioDesempate(CriterioDesempate criterio)
    {
        ReferenciaRegraDto regra = new(criterio.Regra.Codigo, criterio.Regra.Versao, criterio.Regra.Hash);

        return criterio.Args switch
        {
            ArgsDesempateMaiorNotaEtapa args => new CriterioDesempateDto(
                criterio.Id, criterio.Ordem, regra, args.EtapaRef, null, null, null, null),
            ArgsDesempateIdoso args => new CriterioDesempateDto(
                criterio.Id, criterio.Ordem, regra, null, args.IdadeMinima, null, null, null),
            ArgsDesempatePredicadoFato args => new CriterioDesempateDto(
                criterio.Id, criterio.Ordem, regra, null, null,
                args.Condicao.Fato,
                args.Condicao.Operador.ToCodigo(),
                ProjetarValorCondicao(args.Condicao.Valor)),
            _ => new CriterioDesempateDto(criterio.Id, criterio.Ordem, regra, null, null, null, null, null),
        };
    }

    /// <summary>
    /// Achata <c>CondicaoDnf.Valor</c> (JSON escalar ou array) para o formato
    /// textual de <see cref="DTOs.CriterioDesempateDto.Valor"/> — projeção de
    /// leitura, não round-trip byte-a-byte (esse é o do envelope canônico).
    /// </summary>
    private static string ProjetarValorCondicao(JsonElement valor) =>
        valor.ValueKind == JsonValueKind.String ? valor.GetString()! : valor.GetRawText();

    private static ConfiguracaoClassificacaoDto? ProjectClassificacao(ProcessoSeletivo processo)
    {
        ConfiguracaoClassificacao? classificacao = processo.Classificacao;
        if (classificacao is null)
        {
            return null;
        }

        return new ConfiguracaoClassificacaoDto(
            classificacao.Id,
            new ReferenciaRegraDto(classificacao.RegraCalculo.Codigo, classificacao.RegraCalculo.Versao, classificacao.RegraCalculo.Hash),
            classificacao.RegraArredondamento is { } arredondamento
                ? new ReferenciaRegraDto(arredondamento.Codigo, arredondamento.Versao, arredondamento.Hash)
                : null,
            classificacao.CasasArredondamento,
            new ReferenciaRegraDto(classificacao.RegraOrdemAlocacao.Codigo, classificacao.RegraOrdemAlocacao.Versao, classificacao.RegraOrdemAlocacao.Hash),
            classificacao.NOpcoesAlocacao,
            [.. classificacao.RegrasEliminacao.Select(ProjectRegraEliminacao)],
            processo.ConcorrenciaDuplaAplicavel());
    }

    private static RegraEliminacaoDto ProjectRegraEliminacao(RegraEliminacao regra)
    {
        ReferenciaRegraDto referenciaRegra = new(regra.Regra.Codigo, regra.Regra.Versao, regra.Regra.Hash);

        return regra.Args switch
        {
            ArgsElimNotaMinimaEtapa args => new RegraEliminacaoDto(regra.Id, referenciaRegra, args.EtapaRef, args.NotaMinima, null),
            ArgsElimCorteRedacao args => new RegraEliminacaoDto(regra.Id, referenciaRegra, null, null, args.Minimo),
            _ => new RegraEliminacaoDto(regra.Id, referenciaRegra, null, null, null),
        };
    }

    private static FaseCronogramaDto ProjectFaseCronograma(FaseCronograma fase) => new(
        fase.Id,
        fase.Ordem,
        fase.FaseCanonicaOrigemId,
        fase.Codigo,
        fase.DonoInstitucional,
        fase.OrigemData.ToString(),
        fase.AgrupaEtapas,
        fase.PermiteComplementacao,
        fase.ProduzResultado,
        fase.ResultadoDefinitivo,
        fase.ColetaInscricao,
        fase.Inicio,
        fase.Fim,
        fase.AtoProduzidoCodigo,
        fase.AtoProduzidoEfeitoIrreversivel,
        [.. fase.BancasRequeridas.Select(static b => new BancaRequeridaDto(b.Id, b.TipoBancaOrigemId, b.Codigo))],
        fase.RegraRecurso is { } regraRecurso ? ProjectRegraRecursoFase(regraRecurso) : null);

    private static RegraRecursoFaseDto ProjectRegraRecursoFase(RegraRecursoFase regraRecurso) => new(
        regraRecurso.Id,
        new ReferenciaRegraDto(regraRecurso.Regra.Codigo, regraRecurso.Regra.Versao, regraRecurso.Regra.Hash),
        new ArgsRegraPrazoRecursoDto(
            regraRecurso.Args.PrazoValor,
            regraRecurso.Args.PrazoUnidade.ToString(),
            regraRecurso.Args.AtoAncoraCodigo,
            regraRecurso.Args.SuspensividadePrimeiraInstanciaValor,
            regraRecurso.Args.SuspensividadePrimeiraInstanciaUnidade?.ToString(),
            regraRecurso.Args.SuspensividadeSegundaInstanciaValor,
            regraRecurso.Args.SuspensividadeSegundaInstanciaUnidade?.ToString()));
}
