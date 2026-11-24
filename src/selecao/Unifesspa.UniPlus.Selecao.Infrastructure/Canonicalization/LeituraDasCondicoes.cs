namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
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

using LinhaDoPredicado = (int Clausula, string Fato, Unifesspa.UniPlus.Regras.Enums.Operador Operador, System.Text.Json.JsonElement Valor);

/// <summary>
/// Leitura das condições em forma normal disjuntiva e do que depende delas: obrigatoriedade, impedimento e gatilho das exigências.
/// </summary>
internal static class LeituraDasCondicoes
{
    /// <summary>
    /// A obrigatoriedade de um item ou de um termo: <c>SEMPRE</c> ou <c>NUNCA</c> sem predicado,
    /// <c>QUANDO</c> com ele. Nulo quando a forma é outra, com a recusa já propagada ao leitor.
    /// </summary>
    internal static Obrigatoriedade? LerObrigatoriedade(LeitorEnvelope leitor, JsonObject item, string path)
    {
        JsonObject bloco = leitor.Objeto(item, "obrigatoriedade", path);

        return leitor.Falhou ? null : LerObrigatoriedadeDoBloco(leitor, bloco, $"{path}.obrigatoriedade");
    }

    private static Obrigatoriedade? LerObrigatoriedadeDoBloco(LeitorEnvelope leitor, JsonObject bloco, string path)
    {
        leitor.ExigirChaves(bloco, path, "tipo", "quando");
        TipoObrigatoriedade tipo = leitor.CodigoDeContrato<TipoObrigatoriedade>(bloco, "tipo", path);
        IReadOnlyList<LinhaDoPredicado> linhas = LerDnf(leitor, bloco, "quando", path);
        if (leitor.Falhou)
        {
            return null;
        }

        Result<PredicadoDnf?> predicado = PredicadoOpcional(linhas);

        return predicado.IsFailure
            ? leitor.Propagar<Obrigatoriedade>(predicado.Error!)
            : ObrigatoriedadeDaForma(leitor, tipo, predicado.Value, path);
    }

    private static Obrigatoriedade? ObrigatoriedadeDaForma(LeitorEnvelope leitor, TipoObrigatoriedade tipo, PredicadoDnf? predicado, string path) =>
        Obrigatoriedade.DaForma(tipo, predicado)
            ?? leitor.Recusar<Obrigatoriedade>($"'{path}' tem tipo SEMPRE ou NUNCA sem predicado, ou QUANDO com predicado.");

    /// <summary>O predicado das linhas lidas; <see langword="null"/> quando não há condição.</summary>
    internal static Result<PredicadoDnf?> PredicadoOpcional(
        IReadOnlyList<(int Clausula, string Fato, Operador Operador, JsonElement Valor)> linhas)
    {
        if (linhas.Count == 0)
        {
            return Result<PredicadoDnf?>.Success(null);
        }

        Result<List<(int Clausula, CondicaoDnf Condicao)>> condicoes = CondicoesDasLinhas(linhas);
        if (condicoes.IsFailure)
        {
            return Result<PredicadoDnf?>.Failure(condicoes.Error!);
        }

        Result<PredicadoDnf> predicado = PredicadoDnf.CriarDeCondicoesAgrupadas(condicoes.Value!);
        return predicado.IsSuccess ? Result<PredicadoDnf?>.Success(predicado.Value) : Result<PredicadoDnf?>.Failure(predicado.Error!);
    }

    private static Result<List<(int Clausula, CondicaoDnf Condicao)>> CondicoesDasLinhas(
        IReadOnlyList<(int Clausula, string Fato, Operador Operador, JsonElement Valor)> linhas)
    {
        List<(int Clausula, CondicaoDnf Condicao)> condicoes = [];
        foreach ((int clausula, string fato, Operador operador, JsonElement valor) in linhas)
        {
            Result<CondicaoDnf> condicao = CondicaoDnf.Criar(fato, operador, valor);
            if (condicao.IsFailure)
            {
                return Result<List<(int Clausula, CondicaoDnf Condicao)>>.Failure(condicao.Error!);
            }

            condicoes.Add((clausula, condicao.Value!));
        }

        return Result<List<(int Clausula, CondicaoDnf Condicao)>>.Success(condicoes);
    }

    /// <summary>
    /// O predicado DNF: <see langword="null"/> na chave é "sem gatilho" (0
    /// cláusulas); do contrário, um array de cláusulas (OU), cada uma um array de
    /// condições (E) — a forma espelha exatamente <see cref="SerializacaoDasExigencias.SerializarCondicaoGatilho"/>.
    /// A validação de forma de cada condição é <see cref="CondicaoGatilho.Criar"/>, a
    /// mesma factory que o caminho de comando usa — o decoder não revalida semântica
    /// (congelamento dos parâmetros na publicação, UNI-REQ-0019: um predicado congelado não é reinterpretado contra um vocabulário que pode
    /// ter mudado).
    /// </summary>
    internal static IReadOnlyList<CondicaoGatilho> LerCondicaoGatilho(LeitorEnvelope leitor, JsonObject item, string pathPai)
    {
        const string chave = "condicoes";
        if (ArrayDeClausulas(leitor, item, chave, pathPai) is not { } clausulas)
        {
            return [];
        }

        List<CondicaoGatilho> condicoes = [];
        bool lidas = Enumerable.Range(0, clausulas.Count)
            .All(c => LerClausulaDoGatilho(leitor, clausulas[c], c, $"{pathPai}.{chave}[{c}]", condicoes));

        return lidas ? condicoes : [];
    }

    /// <summary>
    /// O array de cláusulas da chave; nulo quando a chave falta ou é <c>null</c> (sem condição) e quando a forma
    /// é outra, caso em que a recusa já foi registrada no leitor.
    /// </summary>
    private static JsonArray? ArrayDeClausulas(LeitorEnvelope leitor, JsonObject item, string chave, string pathPai)
    {
        if (item[chave] is not JsonNode raiz)
        {
            return null;
        }

        if (raiz is JsonArray clausulas)
        {
            return clausulas;
        }

        leitor.Recusar<object>($"'{pathPai}.{chave}' deveria ser um array de cláusulas ou null.");
        return null;
    }

    private static bool LerClausulaDoGatilho(LeitorEnvelope leitor, JsonNode? node, int clausula, string clausulaPath, List<CondicaoGatilho> condicoes)
    {
        if (node is not JsonArray condicoesDaClausula)
        {
            leitor.Recusar<object>($"'{clausulaPath}' deveria ser um array de condições.");
            return false;
        }

        List<LinhaDoPredicado> linhas = [];
        return LerLinhas(leitor, condicoesDaClausula, clausula, clausulaPath, linhas) && linhas.TrueForAll(linha => AdicionarGatilho(leitor, linha, condicoes));
    }

    private static bool AdicionarGatilho(LeitorEnvelope leitor, LinhaDoPredicado linha, List<CondicaoGatilho> condicoes)
    {
        Result<CondicaoGatilho> condicao = CondicaoGatilho.Criar(linha.Clausula, linha.Fato, linha.Operador, linha.Valor);
        if (condicao.IsFailure)
        {
            leitor.Propagar<CondicaoGatilho>(condicao.Error!);
            return false;
        }

        condicoes.Add(condicao.Value!);
        return true;
    }

    /// <summary>
    /// A resposta que impede a inscrição, congelada no item; nula quando o item não tem. A forma — a
    /// mensagem e as cláusulas sobre o próprio campo — é reconferida por <see cref="FatoColetado.Criar"/>.
    /// </summary>
    internal static Impedimento? LerImpedimento(LeitorEnvelope leitor, JsonObject item, string path)
    {
        JsonObject? bloco = leitor.ObjetoOpcional(item, "impedimento", path);

        return leitor.Falhou || bloco is null ? null : LerImpedimentoDoBloco(leitor, bloco, $"{path}.impedimento");
    }

    private static Impedimento? LerImpedimentoDoBloco(LeitorEnvelope leitor, JsonObject bloco, string path)
    {
        leitor.ExigirChaves(bloco, path, "quando", "mensagem");
        string mensagem = leitor.TextoNaoVazio(bloco, "mensagem", path, LimitesDoEnvelope.MensagemDoImpedimento);
        IReadOnlyList<LinhaDoPredicado> linhas = LerDnf(leitor, bloco, "quando", path);
        if (leitor.Falhou)
        {
            return null;
        }

        Result<PredicadoDnf?> quando = PredicadoOpcional(linhas);

        return quando.IsFailure
            ? leitor.Propagar<Impedimento>(quando.Error!)
            : ImpedimentoDe(leitor, quando.Value, mensagem, path);
    }

    private static Impedimento? ImpedimentoDe(LeitorEnvelope leitor, PredicadoDnf? quando, string mensagem, string path) =>
        quando is { } condicao
            ? new Impedimento(condicao, mensagem)
            : leitor.Propagar<Impedimento>(new DomainError(ErrosCodecEnvelope.EnvelopeMalformado, $"'{path}.quando' sem condição."));

    /// <summary>
    /// Um predicado DNF <c>{fato, operador, valor}</c> (pré-condição de fato ou <c>quando</c> de
    /// regra), na mesma forma do gatilho: array de cláusulas (OU), cada uma array de condições (E).
    /// Ausente/nulo = sem condição. Mesma disciplina de <c>LerCondicaoGatilho</c> —
    /// a factory revalida a forma; código de operador fora do catálogo recusa o envelope.
    /// </summary>
    internal static IReadOnlyList<LinhaDoPredicado> LerDnf(LeitorEnvelope leitor, JsonObject item, string chave, string pathPai) =>
        ArrayDeClausulas(leitor, item, chave, pathPai) is { } clausulas
            ? LerClausulasDoPredicado(leitor, clausulas, $"{pathPai}.{chave}")
            : [];

    /// <summary>
    /// O encoder nunca emite DNF vazio: "sem condição" é null, não []. Um array vazio num envelope adulterado
    /// colapsaria para "sem pré-condição", forma que a projeção viva não produz, e a reidratação o
    /// recanonicalizaria como null, aceitando em silêncio um snapshot nunca congelado.
    /// </summary>
    private static List<LinhaDoPredicado> LerClausulasDoPredicado(LeitorEnvelope leitor, JsonArray clausulas, string path)
    {
        if (clausulas.Count == 0)
        {
            leitor.Recusar<object>($"'{path}' é um array de cláusulas vazio — a ausência de condição é `null`, nunca `[]`.");
            return [];
        }

        List<LinhaDoPredicado> condicoes = [];
        bool lidas = Enumerable.Range(0, clausulas.Count)
            .All(c => LerClausula(leitor, clausulas[c], c, $"{path}[{c}]", condicoes));

        return lidas ? condicoes : [];
    }

    private static bool LerClausula(LeitorEnvelope leitor, JsonNode? node, int clausula, string clausulaPath, List<LinhaDoPredicado> condicoes)
    {
        if (node is not JsonArray condicoesDaClausula)
        {
            leitor.Recusar<object>($"'{clausulaPath}' deveria ser um array de condições.");
            return false;
        }

        if (condicoesDaClausula.Count == 0)
        {
            leitor.Recusar<object>($"'{clausulaPath}' é uma cláusula vazia — toda cláusula tem ao menos uma condição.");
            return false;
        }

        return LerLinhas(leitor, condicoesDaClausula, clausula, clausulaPath, condicoes);
    }

    private static bool LerLinhas(LeitorEnvelope leitor, JsonArray condicoesDaClausula, int clausula, string clausulaPath, List<LinhaDoPredicado> linhas)
    {
        for (int i = 0; i < condicoesDaClausula.Count; i++)
        {
            if (LerLinha(leitor, condicoesDaClausula, i, clausula, clausulaPath) is not { } linha)
            {
                return false;
            }

            linhas.Add(linha);
        }

        return true;
    }

    private static LinhaDoPredicado? LerLinha(LeitorEnvelope leitor, JsonArray condicoes, int indice, int clausula, string clausulaPath)
    {
        string condicaoPath = $"{clausulaPath}[{indice}]";
        JsonObject condicao = leitor.ItemObjeto(condicoes, indice, clausulaPath);
        leitor.ExigirChaves(condicao, condicaoPath, "codigoDoDado", "operador", "valor");

        string fato = leitor.TextoNaoVazio(condicao, "codigoDoDado", condicaoPath, LimitesDoEnvelope.Fato);
        Operador operador = LerOperador(leitor, condicao, condicaoPath);
        JsonElement valor = leitor.Valor(condicao, "valor", condicaoPath);

        return leitor.Falhou ? null : (clausula, fato, operador, valor);
    }

    /// <summary>
    /// O operador congelado no envelope é o código declarado no enum. O código fora do catálogo, como o de um formato
    /// antigo, recusa o envelope: ele não é lido.
    /// </summary>
    internal static Operador LerOperador(LeitorEnvelope leitor, JsonObject condicao, string condicaoPath)
    {
        string codigo = leitor.TextoNaoVazio(condicao, "operador", condicaoPath);
        if (leitor.Falhou)
        {
            return default;
        }

        if (CatalogoDe<Operador>.TentarObter(codigo, out Operador operador))
        {
            return operador;
        }

        string aceitos = string.Join(", ", CatalogoDe<Operador>.Todos().Select(static d => d.Codigo));
        return leitor.Malformado<Operador>($"{condicaoPath}.operador", $"o operador não é um dos códigos aceitos: {aceitos}.");
    }
}
