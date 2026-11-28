namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.OfertasDeCurso;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoCongelado;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Projeta no envelope a distribuição de vagas: os insumos da distribuição, as modalidades, as ofertas de
/// curso e as opções de curso. O quadro de vagas calculado é do <see cref="SerializacaoDoQuadroDeVagas"/>.
/// </summary>
internal static class SerializacaoDasVagas
{
    internal static JsonArray SerializarDistribuicao(ProcessoSeletivo processo) =>
        new([.. OrdenarPorOfertaCursoOrigemId(processo.DistribuicaoVagas).Select(static c => (JsonNode)SerializarInsumos(c))]);

    private static JsonObject SerializarInsumos(ConfiguracaoDistribuicaoVagas configuracao) => new()
    {
        ["ofertaCursoOrigemId"] = configuracao.OfertaCursoOrigemId,
        ["vagasOfertadasBase"] = configuracao.VagasOfertadasBase,
        ["percentualDaReservaLegal"] = SerializarPercentual(configuracao.PercentualDaReservaLegal),
        ["regraDistribuicao"] = EscritaCanonica.SerializarReferenciaRegra(configuracao.RegraDistribuicao),
        ["regraAjuste"] = configuracao.RegraAjuste is { } regraAjuste ? EscritaCanonica.SerializarReferenciaRegra(regraAjuste) : null,
        ["ajusteDoQuadro"] = SerializacaoDoQuadroDeVagas.SerializarAjusteDoQuadro(configuracao.AjusteDoQuadro),
        ["referenciaDemografica"] = configuracao.ReferenciaDemografica is { } referencia ? SerializarReferenciaDemografica(referencia) : null,
        ["grupoAreaEnem"] = configuracao.GrupoAreaEnem is { } grupo ? AreasDoEnemNoDocumentoCongelado.Grupo(grupo) : null,
    };

    private static string? SerializarPercentual(decimal? percentual) =>
        percentual is { } valor ? HashCanonicalComputer.SerializeDecimalCanonical(valor, EscritaCanonica.EscalaPadrao) : null;

    private static JsonObject SerializarReferenciaDemografica(ReferenciaReservaDemograficaSnapshot referencia) => new()
    {
        ["origemId"] = referencia.OrigemId,
        ["censoReferencia"] = HashCanonicalComputer.NormalizeNfc(referencia.CensoReferencia),
        ["ppiPercentual"] = HashCanonicalComputer.SerializeDecimalCanonical(referencia.PpiPercentual, EscritaCanonica.EscalaPercentual),
        ["quilombolaPercentual"] = HashCanonicalComputer.SerializeDecimalCanonical(referencia.QuilombolaPercentual, EscritaCanonica.EscalaPercentual),
        ["pcdPercentual"] = HashCanonicalComputer.SerializeDecimalCanonical(referencia.PcdPercentual, EscritaCanonica.EscalaPercentual),
        ["baseLegal"] = HashCanonicalComputer.NormalizeNfc(referencia.BaseLegal),
    };

    internal static JsonArray SerializarModalidades(ProcessoSeletivo processo) =>
        new([.. OrdenarPorOfertaCursoOrigemId(processo.DistribuicaoVagas).SelectMany(static configuracao => configuracao.Modalidades
            .OrderBy(static m => m.Codigo, StringComparer.Ordinal)
            .Select(modalidade => (JsonNode)SerializarModalidade(configuracao.OfertaCursoOrigemId, modalidade)))]);

    private static JsonObject SerializarModalidade(Guid ofertaCursoOrigemId, ModalidadeSelecionada modalidade) =>
        AcrescentarRegraEQuantidade(AcrescentarRemanejamento(IdentificarModalidade(ofertaCursoOrigemId, modalidade), modalidade), modalidade);

    private static JsonObject IdentificarModalidade(Guid ofertaCursoOrigemId, ModalidadeSelecionada modalidade) => new()
    {
        ["ofertaCursoOrigemId"] = ofertaCursoOrigemId,
        ["modalidadeOrigemId"] = modalidade.ModalidadeOrigemId,
        ["codigo"] = HashCanonicalComputer.NormalizeNfc(modalidade.Codigo),
        ["nome"] = HashCanonicalComputer.NormalizeNfc(modalidade.Nome),
        ["descricao"] = modalidade.Descricao is { } descricao ? HashCanonicalComputer.NormalizeNfc(descricao) : null,
        ["naturezaLegal"] = modalidade.NaturezaLegal.Codigo(),
        ["composicaoVagas"] = modalidade.ComposicaoVagas.Codigo(),
        ["composicaoOrigemCodigo"] = modalidade.ComposicaoOrigemCodigo,
    };

    private static JsonObject AcrescentarRemanejamento(JsonObject objeto, ModalidadeSelecionada modalidade)
    {
        objeto["regraRemanejamento"] = modalidade.RegraRemanejamento is { } regra ? regra.Codigo() : null;
        ContextoDosRotulos.Acrescentar(objeto, "naturezaLegal", modalidade.NaturezaLegal);
        ContextoDosRotulos.Acrescentar(objeto, "composicaoVagas", modalidade.ComposicaoVagas);
        ContextoDosRotulos.Acrescentar(objeto, "regraRemanejamento", modalidade.RegraRemanejamento);
        objeto["remanejamentoDestino"] = modalidade.RemanejamentoDestino;
        objeto["remanejamentoPar"] = modalidade.RemanejamentoPar;
        objeto["remanejamentoDestinoFinal"] = modalidade.RemanejamentoFallback;
        return objeto;
    }

    private static JsonObject AcrescentarRegraEQuantidade(JsonObject objeto, ModalidadeSelecionada modalidade)
    {
        objeto["regraDeAplicabilidade"] = SerializacaoDosPredicados.SerializarDnf(SerializacaoDosPredicados.LinhasDoPredicado(modalidade.RegraDeAplicabilidade));
        objeto["acaoQuandoIndeferido"] = modalidade.AcaoQuandoIndeferido;
        objeto["baseLegal"] = HashCanonicalComputer.NormalizeNfc(modalidade.BaseLegal);
        objeto["quantidadeDeclarada"] = modalidade.QuantidadeDeclarada;
        return objeto;
    }

    private static IOrderedEnumerable<ConfiguracaoDistribuicaoVagas> OrdenarPorOfertaCursoOrigemId(
        IEnumerable<ConfiguracaoDistribuicaoVagas> distribuicoes) =>
        distribuicoes.OrderBy(static d => d.OfertaCursoOrigemId);

    internal static JsonArray SerializarOfertas(ProcessoSeletivo processo) =>
        new([.. processo.DistribuicaoVagas
            .OrderBy(static d => d.OfertaCursoOrigemId.ToString(), StringComparer.Ordinal)
            .Select(static c => (JsonNode)SerializarOferta(c))]);

    private static JsonObject SerializarOferta(ConfiguracaoDistribuicaoVagas configuracao)
    {
        CopiaDaOfertaDeCurso oferta = configuracao.OfertaCurso ?? throw new InvalidOperationException(
            $"Canonicalização de oferta de curso sem nome congelado ({configuracao.OfertaCursoOrigemId}) — a verificação de conformidade deveria ter recusado a transição antes deste ponto.");

        return new JsonObject
        {
            ["ofertaCursoOrigemId"] = configuracao.OfertaCursoOrigemId,
            ["ofertaCursoCodigo"] = HashCanonicalComputer.NormalizeNfc(oferta.Codigo),
            ["cursoNome"] = HashCanonicalComputer.NormalizeNfc(oferta.CursoNome),
            ["cursoGrau"] = HashCanonicalComputer.NormalizeNfc(oferta.CursoGrau),
            ["unidadeOfertanteNome"] = HashCanonicalComputer.NormalizeNfc(oferta.UnidadeOfertanteNome),
            ["turnos"] = new JsonArray([.. oferta.Turnos.Select(static t => (JsonNode?)JsonValue.Create(HashCanonicalComputer.NormalizeNfc(t)))]),
        };
    }

    internal static JsonObject SerializarOpcoesDeCurso(ProcessoSeletivo processo) =>
        new()
        {
            ["numero"] = processo.NumeroDeOpcoesDeCurso
                ?? throw new InvalidOperationException(
                    "Canonicalização de processo sem número de opções de curso — a verificação de conformidade deveria ter recusado a transição antes deste ponto."),
        };
}
