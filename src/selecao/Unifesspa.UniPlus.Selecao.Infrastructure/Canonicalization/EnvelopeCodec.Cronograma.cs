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
/// Leitura do bloco <c>cronogramaFases</c>: fases, produtos publicados, bancas requeridas e
/// a regra de recurso de cada fase.
/// </summary>
public sealed partial class EnvelopeCodec
{
    /// <summary>
    /// O cronograma de fases (Story #851): <c>origemCandidatos</c> é lido e validado para
    /// fechar a gramática, mas <b>não é propagado</b> — é atributo de raiz do agregado
    /// (<c>ProcessoSeletivo.OrigemCandidatos</c>), imutável após a criação (nenhum
    /// <c>Definir*</c> o altera), e por isso nada aqui precisa repô-lo.
    /// </summary>
    /// <param name="comId">
    /// <see langword="true"/> quando o bloco congela <c>id</c> por fase (Story #554, PR #903,
    /// bump 1.2, achado de revisão) — a 1.1 nunca teve essa chave, e <c>ExigirChaves</c> é
    /// fechado (chave extra reprova tanto quanto chave ausente), então o mesmo leitor não
    /// pode aceitar as duas formas com uma única lista fixa. Com <see langword="true"/>, a
    /// fase reidrata preservando o Id congelado (<see cref="FaseCronograma.Reidratar"/>);
    /// com <see langword="false"/> (1.1, comportamento inalterado), <see cref="FaseCronograma.Criar"/>
    /// gera um Id novo, exatamente como sempre gerou.
    /// </param>
    private static IReadOnlyList<FaseCronograma> LerCronogramaFases(LeitorEnvelope leitor, JsonObject payload, bool comId = false)
    {
        JsonObject bloco = leitor.Objeto(payload, "cronogramaFases", "$");
        leitor.ExigirChaves(bloco, "cronogramaFases", "origemCandidatos", "fases");

        leitor.Enumeracao<OrigemCandidatos>(bloco, "origemCandidatos", "cronogramaFases");

        JsonArray array = leitor.Array(bloco, "fases", "cronogramaFases");
        if (leitor.Falhou)
        {
            return [];
        }

        List<FaseCronograma> fases = [];
        for (int i = 0; i < array.Count; i++)
        {
            string path = $"cronogramaFases.fases[{i}]";
            JsonObject item = leitor.ItemObjeto(array, i, "cronogramaFases.fases");
            string[] chavesBase =
            [
                "ordem", "faseCanonicaOrigemId", "codigo", "donoInstitucional", "origemData",
                "agrupaEtapas", "permiteComplementacao",
                "coletaInscricao", "coletaSolicitacaoIsencao", "inicio", "fim",
                "produtos", "faseConcluinteCodigo", "emiteParecerIndividual",
                "bancasRequeridas", "regraRecurso",
            ];
            leitor.ExigirChaves(item, path, comId ? [.. chavesBase, "id"] : chavesBase);

            Guid? id = comId ? leitor.Identificador(item, "id", path) : null;
            int ordem = leitor.Inteiro(item, "ordem", path);
            Guid faseCanonicaOrigemId = leitor.Identificador(item, "faseCanonicaOrigemId", path);
            string codigo = leitor.TextoNaoVazio(item, "codigo", path, LimitesDoEnvelope.FaseCodigo);
            string donoInstitucional = leitor.TextoNaoVazio(item, "donoInstitucional", path, LimitesDoEnvelope.DonoInstitucional);
            OrigemDataFase origemData = leitor.Enumeracao<OrigemDataFase>(item, "origemData", path);
            bool agrupaEtapas = leitor.Booleano(item, "agrupaEtapas", path);
            bool permiteComplementacao = leitor.Booleano(item, "permiteComplementacao", path);
            bool coletaInscricao = leitor.Booleano(item, "coletaInscricao", path);
            bool coletaSolicitacaoIsencao = leitor.Booleano(item, "coletaSolicitacaoIsencao", path);
            DateTimeOffset? inicio = leitor.InstanteOpcional(item, "inicio", path);
            DateTimeOffset? fim = leitor.InstanteOpcional(item, "fim", path);
            string? faseConcluinteCodigo = leitor.TextoOpcional(item, "faseConcluinteCodigo", path, LimitesDoEnvelope.FaseCodigo);
            bool emiteParecerIndividual = leitor.Booleano(item, "emiteParecerIndividual", path);

            if (leitor.Falhou)
            {
                return [];
            }

            IReadOnlyList<ProdutoDaFase> produtos = LerProdutosDaFase(leitor, item, path);
            if (leitor.Falhou)
            {
                return [];
            }

            IReadOnlyList<BancaRequerida> bancas = LerBancasRequeridas(leitor, item, path);
            if (leitor.Falhou)
            {
                return [];
            }

            RegraRecursoFase? regraRecurso = LerRegraRecursoFase(leitor, item, path);
            if (leitor.Falhou)
            {
                return [];
            }

            // A âncora do recurso é a única referência cruzada dentro da fase, e a
            // reidratação a repõe como estava em vez de rederivá-la. Conferi-la aqui, pelo
            // MESMO predicado do agregado, é o que faz um envelope incoerente virar recusa
            // nomeada em vez de exceção da fábrica — ela só tem `throw` para esse estado,
            // porque nenhum caminho de escrita o produz.
            if (FaseCronograma.ViolacaoDaAncoraDoRecurso(codigo, produtos, regraRecurso) is { } violacaoDaAncora)
            {
                return leitor.Propagar<IReadOnlyList<FaseCronograma>>(violacaoDaAncora.Error) ?? [];
            }

            Result<FaseCronograma> fase = comId
                ? Result<FaseCronograma>.Success(FaseCronograma.Reidratar(
                    id!.Value, ordem, faseCanonicaOrigemId, codigo, donoInstitucional, origemData,
                    agrupaEtapas, permiteComplementacao, coletaInscricao, coletaSolicitacaoIsencao,
                    inicio, fim, produtos, faseConcluinteCodigo, emiteParecerIndividual, bancas, regraRecurso))
                : FaseCronograma.Criar(
                    ordem, faseCanonicaOrigemId, codigo, donoInstitucional, origemData,
                    agrupaEtapas, permiteComplementacao, coletaInscricao, coletaSolicitacaoIsencao,
                    inicio, fim, produtos, faseConcluinteCodigo, emiteParecerIndividual, bancas, regraRecurso);
            if (fase.IsFailure)
            {
                return leitor.Propagar<IReadOnlyList<FaseCronograma>>(fase.Error!) ?? [];
            }

            fases.Add(fase.Value!);
        }

        return fases;
    }

    /// <summary>
    /// Os produtos que a fase publica. O <c>id</c> é congelado e reidratado — é contra ele
    /// que o ato publicado resolverá, de volta, a configuração de recurso que lhe
    /// corresponde.
    /// </summary>
    private static List<ProdutoDaFase> LerProdutosDaFase(LeitorEnvelope leitor, JsonObject faseItem, string pathPai)
    {
        JsonArray array = leitor.Array(faseItem, "produtos", pathPai);
        if (leitor.Falhou)
        {
            return [];
        }

        List<ProdutoDaFase> produtos = [];
        for (int i = 0; i < array.Count; i++)
        {
            string path = $"{pathPai}.produtos[{i}]";
            JsonObject item = leitor.ItemObjeto(array, i, $"{pathPai}.produtos");
            leitor.ExigirChaves(item, path, "id", "atoCodigo", "papel");

            Guid? id = leitor.Identificador(item, "id", path);
            string atoCodigo = leitor.TextoNaoVazio(item, "atoCodigo", path, LimitesDoEnvelope.TipoAtoCodigo);
            PapelProdutoFase? papel = leitor.EnumeracaoOpcional<PapelProdutoFase>(item, "papel", path);

            if (leitor.Falhou)
            {
                return [];
            }

            produtos.Add(ProdutoDaFase.Reidratar(id!.Value, atoCodigo, papel));
        }

        return produtos;
    }

    private static List<BancaRequerida> LerBancasRequeridas(LeitorEnvelope leitor, JsonObject faseItem, string pathPai)
    {
        JsonArray array = leitor.Array(faseItem, "bancasRequeridas", pathPai);
        if (leitor.Falhou)
        {
            return [];
        }

        List<BancaRequerida> bancas = [];
        for (int i = 0; i < array.Count; i++)
        {
            string path = $"{pathPai}.bancasRequeridas[{i}]";
            JsonObject item = leitor.ItemObjeto(array, i, $"{pathPai}.bancasRequeridas");
            leitor.ExigirChaves(item, path, "tipoBancaOrigemId", "codigo");

            Guid tipoBancaOrigemId = leitor.Identificador(item, "tipoBancaOrigemId", path);
            string codigo = leitor.TextoNaoVazio(item, "codigo", path, LimitesDoEnvelope.TipoBancaCodigo);

            if (leitor.Falhou)
            {
                return [];
            }

            bancas.Add(BancaRequerida.Criar(tipoBancaOrigemId, codigo));
        }

        return bancas;
    }

    /// <summary>
    /// A regra de recurso da fase (0..1 — a PRESENÇA do bloco é o que faz a fase admitir
    /// recurso, §3.6). O vocabulário de <c>regra.codigo</c> é fechado a
    /// <see cref="RegraPrazoRecursoCodigo.AncoradoEmAto"/> — a única variante que
    /// <see cref="RegraRecursoFase"/> admite (CA-02).
    /// </summary>
    /// <remarks>
    /// O <c>produtoAncoraId</c> é irmão de <c>regra</c> e <c>args</c>, e não campo de
    /// <c>args</c>: ele não é parâmetro que alguém preencha, e sim a referência que a fase
    /// deriva do próprio produto preliminar. A restauração o repõe como estava congelado —
    /// é ele que o ato já publicado resolve de volta para achar o prazo que lhe corresponde
    /// (UNI-REQ-0093).
    /// </remarks>
    private static RegraRecursoFase? LerRegraRecursoFase(LeitorEnvelope leitor, JsonObject faseItem, string pathPai)
    {
        JsonObject? bloco = leitor.ObjetoOpcional(faseItem, "regraRecurso", pathPai);
        if (leitor.Falhou || bloco is null)
        {
            return null;
        }

        string path = $"{pathPai}.regraRecurso";
        leitor.ExigirChaves(bloco, path, "regra", "produtoAncoraId", "args");

        ReferenciaRegra regra = leitor.Regra(bloco, "regra", path, RegraPrazoRecursoCodigo.AncoradoEmAto);
        Guid produtoAncoraId = leitor.Identificador(bloco, "produtoAncoraId", path);
        JsonObject argsObjeto = leitor.Objeto(bloco, "args", path);
        if (leitor.Falhou)
        {
            return null;
        }

        string argsPath = $"{path}.args";
        leitor.ExigirChaves(
            argsObjeto, argsPath,
            "prazoValor", "prazoUnidade",
            "suspensividadePrimeiraInstanciaValor", "suspensividadePrimeiraInstanciaUnidade",
            "suspensividadeSegundaInstanciaValor", "suspensividadeSegundaInstanciaUnidade");

        decimal prazoValor = leitor.Decimal(argsObjeto, "prazoValor", EscalaPadrao, argsPath, LimitesDoEnvelope.PrecisaoPrazo);
        UnidadePrazo prazoUnidade = leitor.Enumeracao<UnidadePrazo>(argsObjeto, "prazoUnidade", argsPath);
        decimal? suspensividade1Valor = leitor.DecimalOpcional(
            argsObjeto, "suspensividadePrimeiraInstanciaValor", EscalaPadrao, argsPath, LimitesDoEnvelope.PrecisaoPrazo);
        UnidadePrazo? suspensividade1Unidade = leitor.EnumeracaoOpcional<UnidadePrazo>(
            argsObjeto, "suspensividadePrimeiraInstanciaUnidade", argsPath);
        decimal? suspensividade2Valor = leitor.DecimalOpcional(
            argsObjeto, "suspensividadeSegundaInstanciaValor", EscalaPadrao, argsPath, LimitesDoEnvelope.PrecisaoPrazo);
        UnidadePrazo? suspensividade2Unidade = leitor.EnumeracaoOpcional<UnidadePrazo>(
            argsObjeto, "suspensividadeSegundaInstanciaUnidade", argsPath);

        if (leitor.Falhou)
        {
            return null;
        }

        ArgsRegraPrazoRecurso args = new(
            prazoValor, prazoUnidade,
            suspensividade1Valor, suspensividade1Unidade,
            suspensividade2Valor, suspensividade2Unidade);

        Result<RegraRecursoFase> regraRecurso = RegraRecursoFase.Reidratar(regra, args, produtoAncoraId);
        return regraRecurso.IsFailure ? leitor.Propagar<RegraRecursoFase>(regraRecurso.Error!) : regraRecurso.Value;
    }
}
