namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Text.Json;

using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

using DTOs;

using Mappings;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Projeta para o contrato REST a distribuição de vagas e o que a acompanha: o atendimento
/// especializado, o bônus regional e a cascata de remanejamento.
/// </summary>
internal static class ProjecaoDasVagas
{
    /// <summary>
    /// A distribuição de vagas de uma oferta de curso. É <c>internal</c> porque a simulação e a escrita
    /// da distribuição devolvem exatamente o mesmo formato da leitura persistida, sem uma segunda
    /// definição que poderia divergir em silêncio.
    /// </summary>
    internal static ConfiguracaoDistribuicaoVagasDto ProjetarDistribuicao(ConfiguracaoDistribuicaoVagas configuracao) => new(
        configuracao.Id,
        configuracao.OfertaCursoOrigemId,
        configuracao.VoBase,
        configuracao.Pr,
        ProjecaoDoProcessoSeletivo.Referencia(configuracao.RegraDistribuicao),
        configuracao.RegraAjuste is { } regraAjuste ? ProjecaoDoProcessoSeletivo.Referencia(regraAjuste) : null,
        configuracao.ReferenciaDemografica is { } demografica ? ProjetarReferenciaDemografica(demografica) : null,
        configuracao.GrupoAreaEnem is { } grupo ? new GrupoAreaEnemSnapshotDto(grupo.Codigo, grupo.Rotulo) : null,
        [.. configuracao.Modalidades.Select(ProjetarModalidade)],
        [.. configuracao.VagasOfertadas.Select(v => new VagaOfertadaDto(v.Id, v.ModalidadeOrigemId, v.ModalidadeCodigo, v.Quantidade))],
        configuracao.VrNominal,
        configuracao.VrFinal,
        configuracao.Estouro,
        configuracao.CapadoEmVo,
        configuracao.TotalPublicado);

    private static ReferenciaReservaDemograficaSnapshotDto ProjetarReferenciaDemografica(ReferenciaReservaDemograficaSnapshot demografica) => new(
        demografica.OrigemId,
        demografica.CensoReferencia,
        demografica.PpiPercentual,
        demografica.QuilombolaPercentual,
        demografica.PcdPercentual,
        demografica.BaseLegal);

    private static ModalidadeSelecionadaDto ProjetarModalidade(ModalidadeSelecionada m) => new(
        m.Id, m.ModalidadeOrigemId, m.Codigo, m.Descricao, m.NaturezaLegal.ToCodigo(), m.ComposicaoVagas.ToCodigo(),
        m.ComposicaoOrigemCodigo, m.RegraRemanejamento.ToCodigo(), m.RemanejamentoDestino, m.RemanejamentoPar, m.RemanejamentoFallback,
        m.RegraDeAplicabilidade.ToDto(), m.AcaoQuandoIndeferido, m.BaseLegal, m.QuantidadeDeclarada);

    internal static OfertaAtendimentoEspecializadoDto? ProjetarAtendimento(OfertaAtendimentoEspecializado? oferta) =>
        oferta is null
            ? null
            : new OfertaAtendimentoEspecializadoDto(
                oferta.Id,
                [.. oferta.Condicoes.Select(c => new OfertaCondicaoDto(c.Id, c.CondicaoOrigemId, c.CondicaoCodigo, c.CondicaoNome))],
                [.. oferta.Recursos.Select(r => new OfertaRecursoDto(r.Id, r.RecursoOrigemId, r.RecursoCodigo, r.RecursoNome))],
                [.. oferta.TiposDeficiencia.Select(t => new OfertaTipoDeficienciaDto(t.Id, t.TipoDeficienciaOrigemId, t.TipoDeficienciaNome))]);

    internal static ConfiguracaoBonusRegionalDto? ProjetarBonusRegional(ConfiguracaoBonusRegional? bonus) =>
        bonus is null
            ? null
            : new ConfiguracaoBonusRegionalDto(
                bonus.Id,
                ProjecaoDoProcessoSeletivo.Referencia(bonus.Regra),
                bonus.Fator,
                bonus.Teto,
                bonus.FatoElegibilidadeCodigo,
                bonus.BaseLegalBonusRegionalId,
                bonus.TipoInstrumento,
                bonus.Identificacao,
                bonus.Descricao,
                [.. bonus.Municipios.Select(static m => new ConfiguracaoBonusRegionalMunicipioDto(m.CodigoIbge, m.Nome, m.Uf))]);

    internal static ConfiguracaoCascataRemanejamentoDto? ProjetarCascata(ConfiguracaoCascataRemanejamento? cascata) =>
        cascata is null
            ? null
            : new ConfiguracaoCascataRemanejamentoDto(
                cascata.Id,
                ProjecaoDoProcessoSeletivo.Referencia(cascata.Regra),
                cascata.FallbackCodigo,
                [.. cascata.Destinos
                    .OrderBy(d => d.ModalidadeOrigemCodigo, StringComparer.Ordinal)
                    .ThenBy(d => d.Ordem)
                    .Select(d => new DestinoRemanejamentoDto(d.Id, d.ModalidadeOrigemCodigo, d.Ordem, d.ModalidadeDestinoCodigo))]);
}
