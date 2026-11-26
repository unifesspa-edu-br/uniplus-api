namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Leitura do atendimento especializado.
/// </summary>
internal static class LeituraDoAtendimento
{
    internal static AtendimentoEspecializado? LerAtendimento(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonObject bloco = leitor.Objeto(payload, "atendimento", "$");
        leitor.ExigirChaves(bloco, "atendimento", "condicoes", "recursos", "tiposDeficiencia");

        JsonArray arrayCondicoes = leitor.Array(bloco, "condicoes", "atendimento");
        JsonArray arrayRecursos = leitor.Array(bloco, "recursos", "atendimento");
        JsonArray arrayTipos = leitor.Array(bloco, "tiposDeficiencia", "atendimento");
        if (leitor.Falhou)
        {
            return null;
        }

        return LerAtendimentoCompleto(leitor, arrayCondicoes, arrayRecursos, arrayTipos);
    }

    private static AtendimentoEspecializado? LerAtendimentoCompleto(LeitorEnvelope leitor, JsonArray arrayCondicoes, JsonArray arrayRecursos, JsonArray arrayTipos)
    {
        List<CondicaoOfertada>? condicoes = LerItens(leitor, arrayCondicoes, "atendimento.condicoes", LerCondicao);
        List<RecursoOfertado>? recursos = LerItens(leitor, arrayRecursos, "atendimento.recursos", LerRecurso);
        List<DeficienciaAtendida>? tipos = LerItens(leitor, arrayTipos, "atendimento.tiposDeficiencia", LerTipoDeDeficiencia);
        if (condicoes is null || recursos is null || tipos is null)
        {
            return null;
        }

        Result<AtendimentoEspecializado> atendimento = AtendimentoEspecializado.Criar(condicoes, recursos, tipos);
        return atendimento.IsFailure ? leitor.Propagar<AtendimentoEspecializado>(LeitorEnvelope.NoProprioItem(atendimento.Errors), "atendimento") : atendimento.Value;
    }

    private static List<T>? LerItens<T>(LeitorEnvelope leitor, JsonArray array, string pathArray, Func<LeitorEnvelope, JsonObject, string, T?> lerItem)
        where T : class
    {
        List<T> itens = [];
        for (int i = 0; i < array.Count; i++)
        {
            T? item = lerItem(leitor, leitor.ItemObjeto(array, i, pathArray), $"{pathArray}[{i}]");
            if (item is null)
            {
                return null;
            }

            itens.Add(item);
        }

        return itens;
    }

    private static CondicaoOfertada? LerCondicao(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(item, path, "condicaoOrigemId", "condicaoCodigo", "condicaoNome");

        Guid origemId = leitor.Identificador(item, "condicaoOrigemId", path);
        string codigo = leitor.TextoNaoVazio(item, "condicaoCodigo", path, LimitesDoEnvelope.CondicaoCodigo);

        if (!leitor.Falhou && !FormatoDoCodigo.TemFormatoValido(codigo))
        {
            return leitor.Malformado<CondicaoOfertada?>(path, "o código de condição não tem o formato do cadastro.");
        }

        string nome = leitor.TextoNaoVazio(item, "condicaoNome", path, LimitesDoEnvelope.NomeDeCadastro);
        return leitor.Falhou ? null : CondicaoOfertada.Criar(origemId, codigo, nome);
    }

    private static RecursoOfertado? LerRecurso(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(item, path, "recursoOrigemId", "recursoCodigo", "recursoNome");

        Guid origemId = leitor.Identificador(item, "recursoOrigemId", path);
        string codigo = leitor.TextoNaoVazio(item, "recursoCodigo", path, LimitesDoEnvelope.RecursoCodigo);
        string nome = leitor.TextoNaoVazio(item, "recursoNome", path, LimitesDoEnvelope.NomeDeCadastro);
        return leitor.Falhou ? null : RecursoOfertado.Criar(origemId, codigo, nome);
    }

    private static DeficienciaAtendida? LerTipoDeDeficiencia(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(item, path, "tipoDeficienciaOrigemId", "tipoDeficienciaCodigo", "tipoDeficienciaNome");

        Guid origemId = leitor.Identificador(item, "tipoDeficienciaOrigemId", path);
        string codigo = leitor.TextoNaoVazio(item, "tipoDeficienciaCodigo", path, LimitesDoEnvelope.TipoDeficienciaCodigo);
        string nome = leitor.TextoNaoVazio(item, "tipoDeficienciaNome", path, LimitesDoEnvelope.NomeDeCadastro);
        return leitor.Falhou ? null : DeficienciaAtendida.Criar(origemId, codigo, nome);
    }
}
