namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using DTOs;
using Domain.Entities;
using Domain.Interfaces;

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
        [.. processo.Etapas
            .OrderBy(e => e.Ordem)
            .Select(e => new EtapaProcessoDto(e.Id, e.Nome, e.Carater.ToString(), e.Peso, e.NotaMinima, e.Ordem))],
        ProjectOfertaAtendimento(processo.OfertaAtendimento),
        [.. processo.DistribuicaoVagas.Select(ProjectDistribuicaoVagas)],
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
}
