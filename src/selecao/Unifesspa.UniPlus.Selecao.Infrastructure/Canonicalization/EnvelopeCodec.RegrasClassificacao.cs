namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Leitura dos blocos de regras de classificação: <c>bonusRegional</c>,
/// <c>cascataRemanejamento</c>, <c>criteriosDesempate</c> e <c>classificacao</c> (incluindo
/// as regras de eliminação).
/// </summary>
public sealed partial class EnvelopeCodec
{
    /// <summary>
    /// O bônus tem <b>duas formas fechadas</b>: <c>{"presente": false}</c> e a completa.
    /// Fechar as duas é o que impede o pior modo de falha desta leitura — um
    /// <c>{"presente":false,"fator":"1.2000",…}</c> lido como “sem bônus”
    /// <b>descartaria o bônus regional do certame</b> (RN05) sem deixar rastro.
    /// </summary>
    private static ConfiguracaoBonusRegional? LerBonusRegional(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonObject bloco = leitor.Objeto(payload, "bonusRegional", "$");
        if (leitor.Falhou)
        {
            return null;
        }

        bool presente = leitor.Booleano(bloco, "presente", "bonusRegional");
        if (leitor.Falhou)
        {
            return null;
        }

        if (!presente)
        {
            leitor.ExigirChaves(bloco, "bonusRegional", "presente");
            return null;
        }

        leitor.ExigirChaves(
            bloco, "bonusRegional", "presente", "regra", "fator", "teto",
            "baseLegalBonusRegionalId", "tipoInstrumento", "identificacao", "descricao", "municipios");

        ReferenciaRegra regra = leitor.Regra(bloco, "regra", "bonusRegional", RegraBonusCodigo.Multiplicativo);
        decimal fator = leitor.Decimal(bloco, "fator", EscalaPadrao, "bonusRegional", LimitesDoEnvelope.PrecisaoBonus);
        decimal? teto = leitor.DecimalOpcional(bloco, "teto", EscalaPadrao, "bonusRegional", LimitesDoEnvelope.PrecisaoBonus);
        Guid baseLegalBonusRegionalId = leitor.Identificador(bloco, "baseLegalBonusRegionalId", "bonusRegional");
        string tipoInstrumento = leitor.TextoNaoVazio(bloco, "tipoInstrumento", "bonusRegional", LimitesDoEnvelope.TipoInstrumentoNormativo);
        string identificacao = leitor.TextoNaoVazio(bloco, "identificacao", "bonusRegional", LimitesDoEnvelope.IdentificacaoBaseLegalBonusRegional);
        string descricao = leitor.TextoNaoVazio(bloco, "descricao", "bonusRegional", LimitesDoEnvelope.DescricaoBaseLegalBonusRegional);
        JsonArray arrayMunicipios = leitor.Array(bloco, "municipios", "bonusRegional");
        if (leitor.Falhou)
        {
            return null;
        }

        List<(string CodigoIbge, string Nome, string Uf)> municipios = [];
        for (int i = 0; i < arrayMunicipios.Count; i++)
        {
            string path = $"bonusRegional.municipios[{i}]";
            JsonObject item = leitor.ItemObjeto(arrayMunicipios, i, "bonusRegional.municipios");
            leitor.ExigirChaves(item, path, "codigoIbge", "nome", "uf");

            string codigoIbge = leitor.TextoNaoVazio(item, "codigoIbge", path, LimitesDoEnvelope.MunicipioBonusRegionalCodigoIbge);
            string nome = leitor.TextoNaoVazio(item, "nome", path, LimitesDoEnvelope.MunicipioBonusRegionalNome);
            string uf = leitor.TextoNaoVazio(item, "uf", path, LimitesDoEnvelope.MunicipioBonusRegionalUf);
            if (leitor.Falhou)
            {
                return null;
            }

            municipios.Add((codigoIbge, nome, uf));
        }

        Result<ConfiguracaoBonusRegional> bonus = ConfiguracaoBonusRegional.Criar(
            regra, fator, teto, baseLegalBonusRegionalId, tipoInstrumento, identificacao, descricao, municipios);
        return bonus.IsFailure ? leitor.Propagar<ConfiguracaoBonusRegional>(bonus.Error!) : bonus.Value;
    }

    /// <summary>
    /// A cascata de remanejamento (Story #575) tem <b>duas formas fechadas</b> — mesmo
    /// raciocínio de <see cref="LerBonusRegional"/>: <c>{"presente": false}</c> e a completa.
    /// A forma (RN-CASCATA-4) é reconferida pela factory de <see cref="ConfiguracaoCascataRemanejamento"/>
    /// ao reconstruir — o decoder não a duplica, só extrai os valores tipados.
    /// </summary>
    private static ConfiguracaoCascataRemanejamento? LerCascataRemanejamento(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonObject bloco = leitor.Objeto(payload, "cascataRemanejamento", "$");
        if (leitor.Falhou)
        {
            return null;
        }

        bool presente = leitor.Booleano(bloco, "presente", "cascataRemanejamento");
        if (leitor.Falhou)
        {
            return null;
        }

        if (!presente)
        {
            leitor.ExigirChaves(bloco, "cascataRemanejamento", "presente");
            return null;
        }

        leitor.ExigirChaves(bloco, "cascataRemanejamento", "presente", "regra", "fallbackCodigo", "ordens");

        ReferenciaRegra regra = leitor.Regra(bloco, "regra", "cascataRemanejamento", RegraRemanejamentoCodigo.Cascata);
        string fallbackCodigo = leitor.Texto(bloco, "fallbackCodigo", "cascataRemanejamento", LimitesDoEnvelope.ModalidadeCodigo);
        JsonArray ordens = leitor.Array(bloco, "ordens", "cascataRemanejamento");
        if (leitor.Falhou)
        {
            return null;
        }

        List<DestinoRemanejamento> destinos = [];
        for (int i = 0; i < ordens.Count; i++)
        {
            string pathOrigem = $"cascataRemanejamento.ordens[{i}]";
            JsonObject itemOrigem = leitor.ItemObjeto(ordens, i, "cascataRemanejamento.ordens");
            leitor.ExigirChaves(itemOrigem, pathOrigem, "origem", "destinos");

            string origem = leitor.Texto(itemOrigem, "origem", pathOrigem, LimitesDoEnvelope.ModalidadeCodigo);
            // Comprimento do código de destino é responsabilidade da factory de
            // DestinoRemanejamento (defesa em profundidade, mesmo raciocínio do decoder
            // ser tão estrito quanto o schema) — Textos() não valida maxLength por si.
            IReadOnlyList<string> destinosDaOrigem = leitor.Textos(itemOrigem, "destinos", pathOrigem);
            if (leitor.Falhou)
            {
                return null;
            }

            for (int j = 0; j < destinosDaOrigem.Count; j++)
            {
                Result<DestinoRemanejamento> destino = DestinoRemanejamento.Criar(origem, j + 1, destinosDaOrigem[j]);
                if (destino.IsFailure)
                {
                    return leitor.Propagar<ConfiguracaoCascataRemanejamento>(destino.Error!);
                }

                destinos.Add(destino.Value!);
            }
        }

        if (leitor.Falhou)
        {
            return null;
        }

        Result<ConfiguracaoCascataRemanejamento> cascata = ConfiguracaoCascataRemanejamento.Criar(regra, fallbackCodigo, destinos);
        return cascata.IsFailure ? leitor.Propagar<ConfiguracaoCascataRemanejamento>(cascata.Error!) : cascata.Value;
    }

    private static IReadOnlyList<CriterioDesempate> LerCriteriosDesempate(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonArray array = leitor.Array(payload, "criteriosDesempate", "$");
        if (leitor.Falhou)
        {
            return [];
        }

        List<CriterioDesempate> criterios = [];
        for (int i = 0; i < array.Count; i++)
        {
            string path = $"criteriosDesempate[{i}]";
            JsonObject item = leitor.ItemObjeto(array, i, "criteriosDesempate");
            leitor.ExigirChaves(item, path, "ordem", "regra", "args");

            int ordem = leitor.Inteiro(item, "ordem", path);
            ReferenciaRegra regra = leitor.Regra(
                item,
                "regra",
                path,
                CriterioDesempateCodigo.MaiorNotaEtapa,
                CriterioDesempateCodigo.MaiorIdade,
                CriterioDesempateCodigo.Idoso,
                CriterioDesempateCodigo.PredicadoFato);
            JsonObject args = leitor.Objeto(item, "args", path);
            if (leitor.Falhou)
            {
                return [];
            }

            ArgsCriterioDesempate? argumentos = LerArgsDesempate(leitor, regra.Codigo, args, $"{path}.args");
            if (leitor.Falhou)
            {
                return [];
            }

            Result<CriterioDesempate> criterio = CriterioDesempate.Criar(ordem, regra, argumentos!);
            if (criterio.IsFailure)
            {
                return leitor.Propagar<IReadOnlyList<CriterioDesempate>>(criterio.Error!) ?? [];
            }

            criterios.Add(criterio.Value!);
        }

        return criterios;
    }

    /// <summary>
    /// A variante vem do <b>código da regra</b> — o envelope não carrega discriminador, e
    /// duas variantes distintas (<c>MAIOR-IDADE</c>, <c>ZERO-EM-AREA</c>) serializam ambas
    /// como <c>{}</c>. Exigir o objeto vazio <b>exatamente</b> é o que impede que args de
    /// uma variante entrem em outra sem que ninguém veja.
    /// </summary>
    private static ArgsCriterioDesempate? LerArgsDesempate(
        LeitorEnvelope leitor,
        string codigo,
        JsonObject args,
        string path)
    {
        switch (codigo)
        {
            case CriterioDesempateCodigo.MaiorNotaEtapa:
                leitor.ExigirChaves(args, path, "etapaRef");
                Guid etapaRef = leitor.Identificador(args, "etapaRef", path);
                return leitor.Falhou ? null : new ArgsDesempateMaiorNotaEtapa(etapaRef);

            case CriterioDesempateCodigo.MaiorIdade:
                leitor.ExigirChaves(args, path);
                return leitor.Falhou ? null : new ArgsDesempateMaiorIdade();

            case CriterioDesempateCodigo.Idoso:
                leitor.ExigirChaves(args, path, "idadeMinima");
                int idadeMinima = leitor.Inteiro(args, "idadeMinima", path);
                return leitor.Falhou ? null : new ArgsDesempateIdoso(idadeMinima);

            case CriterioDesempateCodigo.PredicadoFato:
                leitor.ExigirChaves(args, path, "fato", "operador", "valor");
                // A validação de FORMA (fato não-vazio, operador reconhecido, valor coerente
                // com o operador) é feita por CondicaoDnf.Criar — a mesma factory que o
                // caminho de comando usa (DefinirCriteriosDesempateCommandHandler). O decoder
                // não revalida SEMÂNTICA (fato pertence ao vocabulário vivo): RN08 proíbe
                // reinterpretar um predicado já congelado contra um catálogo que pode ter
                // mudado — só a forma é exigida aqui.
                string fato = leitor.TextoNaoVazio(args, "fato", path);
                string operadorCodigo = leitor.TextoNaoVazio(args, "operador", path);
                System.Text.Json.JsonElement valor = leitor.Valor(args, "valor", path);
                if (leitor.Falhou)
                {
                    return null;
                }

                Operador operador = OperadorCodigo.FromCodigo(operadorCodigo);
                Result<CondicaoDnf> condicaoResult = CondicaoDnf.Criar(fato, operador, valor);
                return condicaoResult.IsFailure
                    ? leitor.Propagar<ArgsCriterioDesempate>(condicaoResult.Error!)
                    : new ArgsDesempatePredicadoFato(condicaoResult.Value!);

            default:
                return leitor.Propagar<ArgsCriterioDesempate>(new DomainError(
                    ErrosCodecEnvelope.RegraDesconhecida,
                    $"Não há variante de args conhecida para o critério de desempate '{codigo}' em '{path}'."));
        }
    }

    private static ConfiguracaoClassificacao? LerClassificacao(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonObject bloco = leitor.Objeto(payload, "classificacao", "$");
        leitor.ExigirChaves(
            bloco,
            "classificacao",
            "regraCalculo",
            "regraArredondamento",
            "casasArredondamento",
            "regraOrdemAlocacao",
            "nOpcoesAlocacao",
            "regrasEliminacao",
            "baseadoEmEnem");

        ReferenciaRegra regraCalculo = leitor.Regra(
            bloco,
            "regraCalculo",
            "classificacao",
            RegraCalculoCodigo.FormulaMediaPonderada,
            RegraCalculoCodigo.ClassificacaoImportada);
        ReferenciaRegra? regraArredondamento = leitor.RegraOpcional(
            bloco,
            "regraArredondamento",
            "classificacao",
            RegraArredondamentoCodigo.PrecisaoTruncar,
            RegraArredondamentoCodigo.PrecisaoArredondarCima);
        int? casas = leitor.InteiroOpcional(bloco, "casasArredondamento", "classificacao");
        ReferenciaRegra regraOrdem = leitor.Regra(
            bloco,
            "regraOrdemAlocacao",
            "classificacao",
            RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04);
        int nOpcoes = leitor.Inteiro(bloco, "nOpcoesAlocacao", "classificacao");
        bool baseadoEmEnem = leitor.Booleano(bloco, "baseadoEmEnem", "classificacao");

        JsonArray array = leitor.Array(bloco, "regrasEliminacao", "classificacao");
        if (leitor.Falhou)
        {
            return null;
        }

        List<RegraEliminacao> eliminacoes = [];
        for (int i = 0; i < array.Count; i++)
        {
            string path = $"classificacao.regrasEliminacao[{i}]";
            JsonObject item = leitor.ItemObjeto(array, i, "classificacao.regrasEliminacao");
            leitor.ExigirChaves(item, path, "regra", "args");

            ReferenciaRegra regra = leitor.Regra(
                item,
                "regra",
                path,
                RegraEliminacaoCodigo.ElimNotaMinimaEtapa,
                RegraEliminacaoCodigo.ElimCorteRedacao,
                RegraEliminacaoCodigo.ElimZeroEmArea);
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
            if (eliminacao.IsFailure)
            {
                return leitor.Propagar<ConfiguracaoClassificacao>(eliminacao.Error!);
            }

            eliminacoes.Add(eliminacao.Value!);
        }

        Result<ConfiguracaoClassificacao> classificacao = ConfiguracaoClassificacao.Criar(
            regraCalculo, regraArredondamento, casas, regraOrdem, nOpcoes, eliminacoes, baseadoEmEnem);

        return classificacao.IsFailure
            ? leitor.Propagar<ConfiguracaoClassificacao>(classificacao.Error!)
            : classificacao.Value;
    }

    private static ArgsRegraEliminacao? LerArgsEliminacao(
        LeitorEnvelope leitor,
        string codigo,
        JsonObject args,
        string path)
    {
        switch (codigo)
        {
            case RegraEliminacaoCodigo.ElimNotaMinimaEtapa:
                leitor.ExigirChaves(args, path, "etapaRef", "notaMinima");
                Guid etapaRef = leitor.Identificador(args, "etapaRef", path);
                decimal notaMinima = leitor.Decimal(args, "notaMinima", EscalaPadrao, path);
                return leitor.Falhou ? null : new ArgsElimNotaMinimaEtapa(etapaRef, notaMinima);

            case RegraEliminacaoCodigo.ElimCorteRedacao:
                leitor.ExigirChaves(args, path, "minimo");
                decimal minimo = leitor.Decimal(args, "minimo", EscalaPadrao, path);
                return leitor.Falhou ? null : new ArgsElimCorteRedacao(minimo);

            case RegraEliminacaoCodigo.ElimZeroEmArea:
                leitor.ExigirChaves(args, path);
                return leitor.Falhou ? null : new ArgsElimZeroEmArea();

            default:
                return leitor.Propagar<ArgsRegraEliminacao>(new DomainError(
                    ErrosCodecEnvelope.RegraDesconhecida,
                    $"Não há variante de args conhecida para a regra de eliminação '{codigo}' em '{path}'."));
        }
    }
}
