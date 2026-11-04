namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Leitura da classificação: quadro de pesos por área do Enem e regras de eliminação.
/// </summary>
internal static class LeituraDaClassificacao
{
    private static readonly string[] ChavesDaClassificacao =
    [
        "regraCalculo", "regraArredondamento", "casasArredondamento", "escalaNotaFinal", "regraOrdemAlocacao",
        "regrasEliminacao", "baseadoEmEnem", "resolucaoPesoAreaEnem", "quadroPesoAreaEnem",
    ];

    internal static ConfiguracaoClassificacao? LerClassificacao(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonObject bloco = leitor.Objeto(payload, "classificacao", "$");
        leitor.ExigirChaves(bloco, "classificacao", ChavesDaClassificacao);

        ReferenciaRegra regraCalculo = leitor.Regra(
            bloco, "regraCalculo", "classificacao", RegraCalculoCodigo.FormulaMediaPonderada, RegraCalculoCodigo.ClassificacaoImportada);
        ReferenciaRegra? regraArredondamento = leitor.RegraOpcional(
            bloco, "regraArredondamento", "classificacao", RegraArredondamentoCodigo.PrecisaoTruncar, RegraArredondamentoCodigo.PrecisaoArredondarCima);
        int? casas = leitor.InteiroOpcional(bloco, "casasArredondamento", "classificacao");
        decimal? escalaNotaFinal = leitor.DecimalOpcional(
            bloco, "escalaNotaFinal", ConfiguracaoClassificacao.EscalaNotaFinalEscala, "classificacao", LimitesDoEnvelope.PrecisaoEscalaNotaFinal);
        ReferenciaRegra regraOrdem = leitor.Regra(
            bloco, "regraOrdemAlocacao", "classificacao", RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria);
        bool baseadoEmEnem = leitor.Booleano(bloco, "baseadoEmEnem", "classificacao");
        List<RegraEliminacao>? eliminacoes = LerRegrasDeEliminacao(leitor, bloco);
        string? resolucaoPesoAreaEnem = leitor.TextoOpcional(
            bloco, "resolucaoPesoAreaEnem", "classificacao", LimitesDoEnvelope.ResolucaoPesoAreaEnem);
        IReadOnlyList<GrupoPesoAreaEnemCongelado>? quadro = eliminacoes is null ? null : LerQuadroPesoAreaEnem(leitor, bloco);
        if (leitor.Falhou)
        {
            return null;
        }

        Result<ConfiguracaoClassificacao> classificacao = ConfiguracaoClassificacao.Criar(
            regraCalculo, regraArredondamento, casas, regraOrdem, eliminacoes!, baseadoEmEnem,
            resolucaoPesoAreaEnem, quadro!, escalaNotaFinal);

        return classificacao.IsFailure
            ? leitor.Propagar<ConfiguracaoClassificacao>(classificacao.Error!)
            : classificacao.Value;
    }

    private static List<RegraEliminacao>? LerRegrasDeEliminacao(LeitorEnvelope leitor, JsonObject bloco)
    {
        JsonArray array = leitor.Array(bloco, "regrasEliminacao", "classificacao");
        if (leitor.Falhou)
        {
            return null;
        }

        List<RegraEliminacao> eliminacoes = [];
        for (int i = 0; i < array.Count; i++)
        {
            RegraEliminacao? eliminacao = LerRegraDeEliminacao(
                leitor, leitor.ItemObjeto(array, i, "classificacao.regrasEliminacao"), $"classificacao.regrasEliminacao[{i}]");
            if (eliminacao is null)
            {
                return null;
            }

            eliminacoes.Add(eliminacao);
        }

        return eliminacoes;
    }

    private static RegraEliminacao? LerRegraDeEliminacao(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(item, path, "regra", "args");

        ReferenciaRegra regra = leitor.Regra(
            item,
            "regra",
            path,
            RegraEliminacaoCodigo.ElimNotaMinimaEtapa,
            RegraEliminacaoCodigo.ElimCorteEmArea,
            RegraEliminacaoCodigo.ElimZeroEmArea,
            RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem,
            RegraEliminacaoCodigo.ElimAusenciaNaEtapa);
        JsonObject args = leitor.Objeto(item, "args", path);
        if (leitor.Falhou)
        {
            return null;
        }

        ArgsRegraEliminacao? argumentos = LerArgsEliminacao(leitor, regra.Codigo, args, $"{path}.args");
        if (leitor.Falhou)
        {
            return null;
        }

        Result<RegraEliminacao> eliminacao = RegraEliminacao.Criar(regra, argumentos!);
        return eliminacao.IsFailure ? leitor.Propagar<RegraEliminacao?>(eliminacao.Error!) : eliminacao.Value;
    }

    /// <summary>
    /// O quadro de pesos por área congelado. Coerência entre o quadro e a resolução
    /// declarada, e a presença dos dois só na classificação baseada em ENEM com cálculo
    /// local, ficam com <see cref="ConfiguracaoClassificacao.Criar"/>; os limites de cada
    /// valor, com <see cref="GrupoPesoAreaEnemCongelado.Criar"/> e com as colunas que o
    /// recebem (<see cref="LimitesDoEnvelope"/>).
    /// </summary>
    internal static IReadOnlyList<GrupoPesoAreaEnemCongelado>? LerQuadroPesoAreaEnem(LeitorEnvelope leitor, JsonObject bloco)
    {
        JsonArray array = leitor.Array(bloco, "quadroPesoAreaEnem", "classificacao");
        if (leitor.Falhou)
        {
            return null;
        }

        List<GrupoPesoAreaEnemCongelado> quadro = [];
        for (int i = 0; i < array.Count; i++)
        {
            GrupoPesoAreaEnemCongelado? grupo = LerGrupoDePesos(
                leitor, leitor.ItemObjeto(array, i, "classificacao.quadroPesoAreaEnem"), $"classificacao.quadroPesoAreaEnem[{i}]");
            if (grupo is null)
            {
                return null;
            }

            quadro.Add(grupo);
        }

        return quadro;
    }

    private static GrupoPesoAreaEnemCongelado? LerGrupoDePesos(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(item, path, "grupoAreaEnem", "baseLegal", "areas");

        (string Codigo, string Rotulo)? grupo = LeituraDasVagas.LerGrupoAreaEnemObrigatorio(leitor, item, path);
        string baseLegal = leitor.TextoNaoVazio(item, "baseLegal", path, LimitesDoEnvelope.BaseLegalPesoAreaEnem);
        JsonArray arrayAreas = leitor.Array(item, "areas", path);
        if (leitor.Falhou || grupo is not { } grupoLido)
        {
            return null;
        }

        List<(string? Codigo, string? Rotulo, decimal Peso, decimal? Corte)>? areas = LerAreas(leitor, arrayAreas, path);
        if (areas is null)
        {
            return null;
        }

        Result<GrupoPesoAreaEnemCongelado> congelado = GrupoPesoAreaEnemCongelado.Criar(grupoLido.Codigo, grupoLido.Rotulo, baseLegal, areas);
        return congelado.IsFailure ? leitor.Propagar<GrupoPesoAreaEnemCongelado?>(congelado.Error!) : congelado.Value;
    }

    private static List<(string? Codigo, string? Rotulo, decimal Peso, decimal? Corte)>? LerAreas(
        LeitorEnvelope leitor, JsonArray arrayAreas, string path)
    {
        List<(string? Codigo, string? Rotulo, decimal Peso, decimal? Corte)> areas = [];
        for (int j = 0; j < arrayAreas.Count; j++)
        {
            string pathArea = $"{path}.areas[{j}]";
            JsonObject area = leitor.ItemObjeto(arrayAreas, j, $"{path}.areas");
            leitor.ExigirChaves(area, pathArea, "codigo", "rotulo", "peso", "corte");

            string codigo = leitor.TextoNaoVazio(area, "codigo", pathArea, LimitesDoEnvelope.AreaPesoAreaEnemCodigo);
            string rotulo = leitor.TextoNaoVazio(area, "rotulo", pathArea, LimitesDoEnvelope.AreaPesoAreaEnemRotulo);
            decimal peso = leitor.Decimal(area, "peso", EscritaCanonica.EscalaPadrao, pathArea, LimitesDoEnvelope.PrecisaoPesoAreaEnem);
            decimal? corte = leitor.DecimalOpcional(area, "corte", EscritaCanonica.EscalaPadrao, pathArea, LimitesDoEnvelope.PrecisaoCorteAreaEnem);
            if (leitor.Falhou)
            {
                return null;
            }

            areas.Add((codigo, rotulo, peso, corte));
        }

        return areas;
    }

    internal static ArgsRegraEliminacao? LerArgsEliminacao(
        LeitorEnvelope leitor,
        string codigo,
        JsonObject args,
        string path) => codigo switch
        {
            RegraEliminacaoCodigo.ElimNotaMinimaEtapa => LerNotaMinimaDaEtapa(leitor, args, path),
            RegraEliminacaoCodigo.ElimCorteEmArea => LerCorteEmArea(leitor, args, path),
            RegraEliminacaoCodigo.ElimZeroEmArea => LerSemArgumentos(leitor, args, path, static () => new ArgsElimZeroEmArea()),
            RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem => LerSemArgumentos(leitor, args, path, static () => new ArgsElimFaltaEmDiaDeProvaEnem()),
            RegraEliminacaoCodigo.ElimAusenciaNaEtapa => LerAusenciaNaEtapa(leitor, args, path),
            _ => leitor.Propagar<ArgsRegraEliminacao>(new DomainError(
                ErrosCodecEnvelope.RegraDesconhecida,
                $"Não há variante de args conhecida para a regra de eliminação '{codigo}' em '{path}'.")),
        };

    private static ArgsElimNotaMinimaEtapa? LerNotaMinimaDaEtapa(LeitorEnvelope leitor, JsonObject args, string path)
    {
        leitor.ExigirChaves(args, path, "etapaRef", "notaMinima");
        Guid etapaRef = leitor.Identificador(args, "etapaRef", path);
        decimal notaMinima = leitor.Decimal(args, "notaMinima", EscritaCanonica.EscalaPadrao, path);
        return leitor.Falhou ? null : new ArgsElimNotaMinimaEtapa(etapaRef, notaMinima);
    }

    private static ArgsElimCorteEmArea? LerCorteEmArea(LeitorEnvelope leitor, JsonObject args, string path)
    {
        leitor.ExigirChaves(args, path, "areaCodigo", "minimo");
        string areaCodigo = leitor.TextoNaoVazio(args, "areaCodigo", path, LimitesDoEnvelope.AreaPesoAreaEnemCodigo);
        decimal minimo = leitor.Decimal(args, "minimo", EscritaCanonica.EscalaPadrao, path);
        return leitor.Falhou ? null : new ArgsElimCorteEmArea(areaCodigo, minimo);
    }

    private static ArgsElimAusenciaNaEtapa? LerAusenciaNaEtapa(LeitorEnvelope leitor, JsonObject args, string path)
    {
        leitor.ExigirChaves(args, path, "etapaRef");
        Guid etapaRef = leitor.Identificador(args, "etapaRef", path);
        return leitor.Falhou ? null : new ArgsElimAusenciaNaEtapa(etapaRef);
    }

    private static ArgsRegraEliminacao? LerSemArgumentos(LeitorEnvelope leitor, JsonObject args, string path, Func<ArgsRegraEliminacao> criar)
    {
        leitor.ExigirChaves(args, path);
        return leitor.Falhou ? null : criar();
    }
}
