namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Projeta no envelope a distribuição de vagas: os insumos da distribuição, o quadro de vagas calculado,
/// as modalidades, as ofertas de curso e as opções de curso.
/// </summary>
internal static class SerializacaoDasVagas
{
    internal static JsonArray SerializarDistribuicao(ProcessoSeletivo processo)
    {
        JsonArray array = [];
        foreach (ConfiguracaoDistribuicaoVagas configuracao in OrdenarPorOfertaCursoOrigemId(processo.DistribuicaoVagas))
        {
            array.Add(new JsonObject
            {
                ["ofertaCursoOrigemId"] = configuracao.OfertaCursoOrigemId,
                ["voBase"] = configuracao.VoBase,
                ["pr"] = HashCanonicalComputer.SerializeDecimalCanonical(configuracao.Pr, EscritaCanonica.EscalaPadrao),
                ["regraDistribuicao"] = EscritaCanonica.SerializarReferenciaRegra(configuracao.RegraDistribuicao),
                ["regraAjuste"] = configuracao.RegraAjuste is { } regraAjuste ? EscritaCanonica.SerializarReferenciaRegra(regraAjuste) : null,
                ["referenciaDemografica"] = configuracao.ReferenciaDemografica is { } referencia
                    ? SerializarReferenciaDemografica(referencia)
                    : null,
                ["grupoAreaEnem"] = configuracao.GrupoAreaEnem is { } grupo ? SerializarGrupoAreaEnem(grupo) : null,
            });
        }

        return array;
    }

    internal static JsonArray SerializarVagas(ProcessoSeletivo processo)
    {
        JsonArray array = [];
        foreach (ConfiguracaoDistribuicaoVagas configuracao in OrdenarPorOfertaCursoOrigemId(processo.DistribuicaoVagas))
        {
            array.Add(new JsonObject
            {
                ["ofertaCursoOrigemId"] = configuracao.OfertaCursoOrigemId,
                ["quadro"] = SerializarQuadro(configuracao),
                ["vrNominal"] = configuracao.VrNominal,
                ["vrFinal"] = configuracao.VrFinal,
                ["estouro"] = configuracao.Estouro,
                ["capadoEmVo"] = configuracao.CapadoEmVo,
                ["totalPublicado"] = configuracao.TotalPublicado,
            });
        }

        return array;
    }

    private static JsonArray SerializarQuadro(ConfiguracaoDistribuicaoVagas configuracao) =>
        new([.. configuracao.VagasOfertadas
            .OrderBy(static v => v.ModalidadeCodigo, StringComparer.Ordinal)
            .Select(static vaga => (JsonNode)new JsonObject
            {
                ["modalidadeCodigo"] = HashCanonicalComputer.NormalizeNfc(vaga.ModalidadeCodigo),
                ["quantidade"] = vaga.Quantidade,
            })]);

    internal static JsonObject SerializarGrupoAreaEnem(GrupoAreaEnemSnapshot grupo) => new()
    {
        ["codigo"] = HashCanonicalComputer.NormalizeNfc(grupo.Codigo),
        ["rotulo"] = HashCanonicalComputer.NormalizeNfc(grupo.Rotulo),
    };

    private static JsonObject SerializarReferenciaDemografica(ReferenciaReservaDemograficaSnapshot referencia) => new()
    {
        ["origemId"] = referencia.OrigemId,
        ["censoReferencia"] = HashCanonicalComputer.NormalizeNfc(referencia.CensoReferencia),
        ["ppiPercentual"] = HashCanonicalComputer.SerializeDecimalCanonical(referencia.PpiPercentual, EscritaCanonica.EscalaPercentual),
        ["quilombolaPercentual"] = HashCanonicalComputer.SerializeDecimalCanonical(referencia.QuilombolaPercentual, EscritaCanonica.EscalaPercentual),
        ["pcdPercentual"] = HashCanonicalComputer.SerializeDecimalCanonical(referencia.PcdPercentual, EscritaCanonica.EscalaPercentual),
        ["baseLegal"] = HashCanonicalComputer.NormalizeNfc(referencia.BaseLegal),
    };

    internal static JsonArray SerializarModalidades(ProcessoSeletivo processo)
    {
        JsonArray array = [];
        foreach (ConfiguracaoDistribuicaoVagas configuracao in OrdenarPorOfertaCursoOrigemId(processo.DistribuicaoVagas))
        {
            IOrderedEnumerable<ModalidadeSelecionada> modalidadesOrdenadas = configuracao.Modalidades
                .OrderBy(static m => m.Codigo, StringComparer.Ordinal);
            foreach (ModalidadeSelecionada modalidade in modalidadesOrdenadas)
            {
                array.Add(SerializarModalidade(configuracao.OfertaCursoOrigemId, modalidade));
            }
        }

        return array;
    }

    private static JsonObject SerializarModalidade(Guid ofertaCursoOrigemId, ModalidadeSelecionada modalidade) => new()
    {
        ["ofertaCursoOrigemId"] = ofertaCursoOrigemId,
        ["modalidadeOrigemId"] = modalidade.ModalidadeOrigemId,
        ["codigo"] = HashCanonicalComputer.NormalizeNfc(modalidade.Codigo),
        ["descricao"] = modalidade.Descricao is { } descricao ? HashCanonicalComputer.NormalizeNfc(descricao) : null,
        ["naturezaLegal"] = modalidade.NaturezaLegal.ToString(),
        ["composicaoVagas"] = modalidade.ComposicaoVagas.ToString(),
        ["composicaoOrigemCodigo"] = modalidade.ComposicaoOrigemCodigo,
        ["regraRemanejamento"] = modalidade.RegraRemanejamento.ToString(),
        ["remanejamentoDestino"] = modalidade.RemanejamentoDestino,
        ["remanejamentoPar"] = modalidade.RemanejamentoPar,
        ["remanejamentoFallback"] = modalidade.RemanejamentoFallback,
        ["regraDeAplicabilidade"] = SerializacaoDosPredicados.SerializarDnf(SerializacaoDosPredicados.LinhasDoPredicado(modalidade.RegraDeAplicabilidade)),
        ["acaoQuandoIndeferido"] = modalidade.AcaoQuandoIndeferido,
        ["baseLegal"] = HashCanonicalComputer.NormalizeNfc(modalidade.BaseLegal),
        ["quantidadeDeclarada"] = modalidade.QuantidadeDeclarada,
    };

    private static IOrderedEnumerable<ConfiguracaoDistribuicaoVagas> OrdenarPorOfertaCursoOrigemId(
        IEnumerable<ConfiguracaoDistribuicaoVagas> distribuicoes) =>
        distribuicoes.OrderBy(static d => d.OfertaCursoOrigemId);

    internal static JsonArray SerializarOfertas(ProcessoSeletivo processo)
    {
        JsonArray array = [];
        foreach (ConfiguracaoDistribuicaoVagas configuracao in processo.DistribuicaoVagas
            .OrderBy(static d => d.OfertaCursoOrigemId.ToString(), StringComparer.Ordinal))
        {
            OfertaCursoSnapshot oferta = configuracao.OfertaCurso ?? throw new InvalidOperationException(
                $"Canonicalização de oferta de curso sem nome congelado ({configuracao.OfertaCursoOrigemId}) — o gate de conformidade deveria ter recusado a transição antes deste ponto.");

            array.Add(new JsonObject
            {
                ["ofertaCursoOrigemId"] = configuracao.OfertaCursoOrigemId,
                ["cursoNome"] = HashCanonicalComputer.NormalizeNfc(oferta.CursoNome),
                ["cursoGrau"] = HashCanonicalComputer.NormalizeNfc(oferta.CursoGrau),
                ["unidadeOfertanteNome"] = HashCanonicalComputer.NormalizeNfc(oferta.UnidadeOfertanteNome),
                ["turnos"] = new JsonArray([.. oferta.Turnos.Select(static t => (JsonNode?)JsonValue.Create(HashCanonicalComputer.NormalizeNfc(t)))]),
            });
        }

        return array;
    }

    internal static JsonObject SerializarOpcoesDeCurso(ProcessoSeletivo processo) =>
        new()
        {
            ["numero"] = processo.NumeroDeOpcoesDeCurso
                ?? throw new InvalidOperationException(
                    "Canonicalização de processo sem número de opções de curso — o gate de conformidade deveria ter recusado a transição antes deste ponto."),
        };
}
