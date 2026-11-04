namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
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
        if (leitor.Falhou)
        {
            return null;
        }

        string pathObrigatoriedade = $"{path}.obrigatoriedade";
        leitor.ExigirChaves(bloco, pathObrigatoriedade, "tipo", "predicado");
        string tipo = leitor.TextoNaoVazio(bloco, "tipo", pathObrigatoriedade);
        IReadOnlyList<LinhaDoPredicado> linhas = LerDnf(leitor, bloco, "predicado", pathObrigatoriedade);
        if (leitor.Falhou)
        {
            return null;
        }

        Result<PredicadoDnf?> predicado = PredicadoOpcional(linhas);
        if (predicado.IsFailure)
        {
            return leitor.Propagar<Obrigatoriedade>(predicado.Error!);
        }

        return (PredicadoDnfJson.TipoDoToken(tipo), predicado.Value) switch
        {
            (TipoObrigatoriedade.Sempre, null) => Obrigatoriedade.Sempre,
            (TipoObrigatoriedade.Nunca, null) => Obrigatoriedade.Nunca,
            (TipoObrigatoriedade.Quando, { } quando) => Obrigatoriedade.Quando(quando),
            _ => leitor.Recusar<Obrigatoriedade>($"'{pathObrigatoriedade}' tem tipo SEMPRE ou NUNCA sem predicado, ou QUANDO com predicado."),
        };
    }

    /// <summary>O predicado das linhas lidas; <see langword="null"/> quando não há condição.</summary>
    internal static Result<PredicadoDnf?> PredicadoOpcional(
        IReadOnlyList<(int Clausula, string Fato, Operador Operador, JsonElement Valor)> linhas)
    {
        if (linhas.Count == 0)
        {
            return Result<PredicadoDnf?>.Success(null);
        }

        List<(int Clausula, CondicaoDnf Condicao)> condicoes = [];
        foreach ((int clausula, string fato, Operador operador, JsonElement valor) in linhas)
        {
            Result<CondicaoDnf> condicao = CondicaoDnf.Criar(fato, operador, valor);
            if (condicao.IsFailure)
            {
                return Result<PredicadoDnf?>.Failure(condicao.Error!);
            }

            condicoes.Add((clausula, condicao.Value!));
        }

        Result<PredicadoDnf> predicado = PredicadoDnf.CriarDeCondicoesAgrupadas(condicoes);
        return predicado.IsSuccess ? Result<PredicadoDnf?>.Success(predicado.Value) : Result<PredicadoDnf?>.Failure(predicado.Error!);
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
        const string chave = "condicaoGatilho";
        if (item[chave] is not JsonNode raiz)
        {
            return [];
        }

        if (raiz is not JsonArray clausulas)
        {
            return leitor.Recusar<IReadOnlyList<CondicaoGatilho>>($"'{pathPai}.{chave}' deveria ser um array de cláusulas ou null.") ?? [];
        }

        List<CondicaoGatilho> condicoes = [];
        for (int c = 0; c < clausulas.Count; c++)
        {
            if (!LerClausulaDoGatilho(leitor, clausulas[c], c, $"{pathPai}.{chave}[{c}]", condicoes))
            {
                return [];
            }
        }

        return condicoes;
    }

    private static bool LerClausulaDoGatilho(LeitorEnvelope leitor, JsonNode? node, int clausula, string clausulaPath, List<CondicaoGatilho> condicoes)
    {
        if (node is not JsonArray condicoesDaClausula)
        {
            leitor.Recusar<object>($"'{clausulaPath}' deveria ser um array de condições.");
            return false;
        }

        for (int i = 0; i < condicoesDaClausula.Count; i++)
        {
            string condicaoPath = $"{clausulaPath}[{i}]";
            JsonObject condicaoItem = leitor.ItemObjeto(condicoesDaClausula, i, clausulaPath);
            leitor.ExigirChaves(condicaoItem, condicaoPath, "fato", "operador", "valor");

            string fato = leitor.TextoNaoVazio(condicaoItem, "fato", condicaoPath, LimitesDoEnvelope.Fato);
            string operadorCodigo = leitor.TextoNaoVazio(condicaoItem, "operador", condicaoPath);
            JsonElement valor = leitor.Valor(condicaoItem, "valor", condicaoPath);
            if (leitor.Falhou)
            {
                return false;
            }

            Result<CondicaoGatilho> condicao = CondicaoGatilho.Criar(clausula, fato, OperadorCodigo.FromCodigo(operadorCodigo), valor);
            if (condicao.IsFailure)
            {
                leitor.Propagar<CondicaoGatilho>(condicao.Error!);
                return false;
            }

            condicoes.Add(condicao.Value!);
        }

        return true;
    }

    /// <summary>
    /// A resposta que impede a inscrição, congelada no item; nula quando o item não tem. A forma — a
    /// mensagem e as cláusulas sobre o próprio campo — é reconferida por <see cref="FatoColetado.Criar"/>.
    /// </summary>
    internal static Impedimento? LerImpedimento(LeitorEnvelope leitor, JsonObject item, string path)
    {
        JsonObject? bloco = leitor.ObjetoOpcional(item, "impedimento", path);
        if (leitor.Falhou || bloco is null)
        {
            return null;
        }

        string pathImpedimento = $"{path}.impedimento";
        leitor.ExigirChaves(bloco, pathImpedimento, "quando", "mensagem");
        string mensagem = leitor.TextoNaoVazio(bloco, "mensagem", pathImpedimento, LimitesDoEnvelope.MensagemDoImpedimento);
        IReadOnlyList<(int Clausula, string Fato, Operador Operador, JsonElement Valor)> linhas = LerDnf(leitor, bloco, "quando", pathImpedimento);
        if (leitor.Falhou)
        {
            return null;
        }

        Result<PredicadoDnf?> quando = PredicadoOpcional(linhas);
        if (quando.IsFailure)
        {
            return leitor.Propagar<Impedimento>(quando.Error!);
        }

        return quando.Value is { } condicao
            ? new Impedimento(condicao, mensagem)
            : leitor.Propagar<Impedimento>(new DomainError(ErrosCodecEnvelope.EnvelopeMalformado, $"'{pathImpedimento}.quando' sem condição."));
    }

    /// <summary>
    /// Um predicado DNF <c>{fato, operador, valor}</c> (pré-condição de fato ou <c>quando</c> de
    /// regra), na mesma forma do gatilho: array de cláusulas (OU), cada uma array de condições (E).
    /// Ausente/nulo = sem condição. Mesma disciplina de <c>LerCondicaoGatilho</c> —
    /// a factory revalida a forma; token de operador não reconhecido vira <c>Nenhuma</c>, que a
    /// factory rejeita como falha de domínio.
    /// </summary>
    internal static IReadOnlyList<LinhaDoPredicado> LerDnf(LeitorEnvelope leitor, JsonObject item, string chave, string pathPai)
    {
        if (item[chave] is not JsonNode raiz)
        {
            return [];
        }

        if (raiz is not JsonArray clausulas)
        {
            return leitor.Recusar<IReadOnlyList<LinhaDoPredicado>>($"'{pathPai}.{chave}' deveria ser um array de cláusulas ou null.") ?? [];
        }

        // O encoder nunca emite DNF vazio: "sem condição" é null, não []. Um array vazio num envelope
        // adulterado colapsaria para "sem pré-condição", forma que a projeção viva não produz, e a
        // restauração o recanonicalizaria como null, aceitando em silêncio um snapshot nunca congelado.
        if (clausulas.Count == 0)
        {
            return leitor.Recusar<IReadOnlyList<LinhaDoPredicado>>($"'{pathPai}.{chave}' é um array de cláusulas vazio — a ausência de condição é `null`, nunca `[]`.") ?? [];
        }

        List<LinhaDoPredicado> condicoes = [];
        for (int c = 0; c < clausulas.Count; c++)
        {
            if (!LerClausula(leitor, clausulas[c], c, $"{pathPai}.{chave}[{c}]", condicoes))
            {
                return [];
            }
        }

        return condicoes;
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

        for (int i = 0; i < condicoesDaClausula.Count; i++)
        {
            string condicaoPath = $"{clausulaPath}[{i}]";
            JsonObject condicaoItem = leitor.ItemObjeto(condicoesDaClausula, i, clausulaPath);
            leitor.ExigirChaves(condicaoItem, condicaoPath, "fato", "operador", "valor");

            string fato = leitor.TextoNaoVazio(condicaoItem, "fato", condicaoPath, LimitesDoEnvelope.Fato);
            string operadorCodigo = leitor.TextoNaoVazio(condicaoItem, "operador", condicaoPath);
            JsonElement valor = leitor.Valor(condicaoItem, "valor", condicaoPath);
            if (leitor.Falhou)
            {
                return false;
            }

            condicoes.Add((clausula, fato, OperadorCodigo.FromCodigo(operadorCodigo), valor));
        }

        return true;
    }
}
