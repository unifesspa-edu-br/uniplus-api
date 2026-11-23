namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
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
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.OrdemCanonica;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Projeta no envelope o cronograma: fases, produtos, bancas requeridas, regras de recurso e regra de homologação das inscrições.
/// </summary>
internal static class SerializacaoDoCronograma
{
    internal static JsonObject SerializarCronogramaFases(ProcessoSeletivo processo) => new()
    {
        ["origemCandidatos"] = processo.OrigemCandidatos.ToString(),
        ["fases"] = SerializarFasesCronograma(processo),
    };

    private static JsonArray SerializarFasesCronograma(ProcessoSeletivo processo) =>
        new([.. OrdemCanonicaDaConfiguracao.Fases(processo.CronogramaFases)
            .Select(static fase => (JsonNode)SerializarFase(fase))]);

    internal static JsonObject SerializarFase(FaseCronograma fase)
    {
        JsonObject item = new() { ["id"] = fase.Id, ["chave"] = fase.Chave.Valor, ["bancasRequeridas"] = SerializarBancasRequeridas(fase) };
        AcrescentarOrigemCanonica(item, fase);
        AcrescentarDecisoesDeCronograma(item, fase);
        AcrescentarPublicacoes(item, fase);

        return item;
    }

    private static void AcrescentarOrigemCanonica(JsonObject item, FaseCronograma fase)
    {
        item["tipoFaseOrigemId"] = fase.FaseCanonicaOrigemId;
        item["codigo"] = HashCanonicalComputer.NormalizeNfc(fase.Codigo);
        item["origemData"] = fase.OrigemData.ToString();
        item["agrupaEtapas"] = fase.AgrupaEtapas;
        item["permiteComplementacao"] = fase.PermiteComplementacao;
        item["coletaInscricao"] = fase.ColetaInscricao;
        item["coletaSolicitacaoIsencao"] = fase.ColetaSolicitacaoIsencao;
    }

    private static void AcrescentarDecisoesDeCronograma(JsonObject item, FaseCronograma fase)
    {
        item["ordem"] = fase.Ordem;
        item["noMesmoMomentoDaAnterior"] = fase.NoMesmoMomentoDaAnterior;
        item["inicio"] = fase.Inicio is { } inicio ? HashCanonicalComputer.SerializeInstantCanonical(inicio) : null;
        item["fim"] = fase.Fim is { } fim ? HashCanonicalComputer.SerializeInstantCanonical(fim) : null;
        item["unidadeResponsavel"] = fase.UnidadeResponsavel is { } unidade ? SerializarUnidadeResponsavel(unidade) : null;
    }

    private static void AcrescentarPublicacoes(JsonObject item, FaseCronograma fase)
    {
        item["publicacoesPrevistas"] = SerializarProdutosDaFase(fase);
        item["emiteParecerIndividual"] = fase.EmiteParecerIndividual;
        item["regraRecurso"] = fase.RegraRecurso is { } regraRecurso ? SerializarRegraRecursoFase(regraRecurso) : null;
        item["tiposDeDecisao"] = TiposDeDecisaoNoEnvelope.Serializar(fase.TiposDeDecisao);
        item[RegraDeHomologacaoNoEnvelope.Chave] = fase.RegraDeHomologacao is { } regraDeHomologacao
            ? RegraDeHomologacaoNoEnvelope.Serializar(regraDeHomologacao)
            : null;
    }

    private static JsonObject SerializarUnidadeResponsavel(UnidadeResponsavelSnapshot unidade) => new()
    {
        ["origemId"] = unidade.OrigemId,
        ["sigla"] = HashCanonicalComputer.NormalizeNfc(unidade.Sigla),
        ["nome"] = HashCanonicalComputer.NormalizeNfc(unidade.Nome),
    };

    private static JsonArray SerializarProdutosDaFase(FaseCronograma fase)
    {
        IOrderedEnumerable<ProdutoDaFase> ordenados = OrdemCanonicaDaConfiguracao.ProdutosDaFase(fase.Produtos);

        return new JsonArray([.. ordenados.Select(static p => (JsonNode)new JsonObject
        {
            ["id"] = p.Id,
            ["atoCodigo"] = HashCanonicalComputer.NormalizeNfc(p.AtoCodigo),
            ["papel"] = p.Papel?.ToString(),
        })]);
    }

    private static JsonArray SerializarBancasRequeridas(FaseCronograma fase)
    {
        IOrderedEnumerable<(BancaRequerida Banca, JsonObject Item)> ordenadas = OrdemCanonicaDaConfiguracao
            .BancasDaFase(fase.BancasRequeridas.Select(static b => (Banca: b, Item: SerializarBancaRequerida(b))), static par => par.Banca);

        return new JsonArray([.. ordenadas.Select(static par => (JsonNode)par.Item)]);
    }

    private static JsonObject SerializarBancaRequerida(BancaRequerida banca)
    {
        JsonObject item = new()
        {
            ["id"] = banca.Id,
            ["chave"] = banca.Chave.Valor,
            ["posicaoNoTipo"] = banca.PosicaoNoTipo,
            ["regraDeDeliberacao"] = RegraDeDeliberacaoNoEnvelope.Serializar(banca.RegraDeDeliberacao),
            ["candidatosAtendidos"] = SerializarCandidatosAtendidos(banca),
        };
        TipoDeBancaNoEnvelope.Serializar(item, banca.Tipo);

        return item;
    }

    private static JsonArray SerializarCandidatosAtendidos(BancaRequerida banca)
    {
        IOrderedEnumerable<CategoriaJulgada> ordenadas = OrdemCanonicaDaConfiguracao.CandidatosAtendidos(banca.CandidatosAtendidos);

        return new JsonArray([.. ordenadas.Select(static c => (JsonNode)new JsonObject
        {
            ["categoriaDocumentoOrigemId"] = c.CategoriaDocumentoOrigemId,
            ["codigo"] = HashCanonicalComputer.NormalizeNfc(c.Codigo),
        })]);
    }

    private static JsonObject SerializarRegraRecursoFase(RegraRecursoFase regraRecurso) => new()
    {
        ["regra"] = EscritaCanonica.SerializarReferenciaRegra(regraRecurso.Regra),
        ["publicacaoDeReferenciaId"] = regraRecurso.ProdutoAncoraId,
        ["parametros"] = SerializarParametrosRegraPrazoRecurso(regraRecurso.Parametros),
        ["procedimentos"] = SerializacaoDosProcedimentosDeRecurso.Serializar(regraRecurso.Procedimentos),
    };

    internal static JsonObject SerializarParametrosRegraPrazoRecurso(ParametrosRegraPrazoRecurso parametros) => new()
    {
        ["prazoValor"] = Valor(parametros.PrazoDeInterposicao),
        ["prazoUnidade"] = parametros.PrazoDeInterposicao.Unidade.Codigo(),
        ["admiteInstanciaSuperior"] = parametros.AdmiteInstanciaSuperior,
        ["prazoSegundaInstanciaValor"] = parametros.PrazoDaSegundaInstancia is { } segunda ? Valor(segunda) : null,
        ["prazoSegundaInstanciaUnidade"] = parametros.PrazoDaSegundaInstancia?.Unidade.Codigo(),
        ["efeitoSuspensivo"] = parametros.EfeitoSuspensivo.Codigo(),
        ["guardaDaVaga"] = parametros.GuardaDaVaga.Codigo(),
    };

    private static string Valor(PrazoDoRecurso prazo) =>
        HashCanonicalComputer.SerializeDecimalCanonical(prazo.Valor, EscritaCanonica.EscalaPadrao);
}
