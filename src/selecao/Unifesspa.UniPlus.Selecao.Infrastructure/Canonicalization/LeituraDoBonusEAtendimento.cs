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
/// Leitura do atendimento especializado e do bônus regional.
/// </summary>
internal static class LeituraDoBonusEAtendimento
{
    internal static OfertaAtendimentoEspecializado? LerAtendimento(LeitorEnvelope leitor, JsonObject payload)
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

        List<OfertaCondicao>? condicoes = LerItens(leitor, arrayCondicoes, "atendimento.condicoes", LerCondicao);
        List<OfertaRecurso>? recursos = LerItens(leitor, arrayRecursos, "atendimento.recursos", LerRecurso);
        List<OfertaTipoDeficiencia>? tipos = LerItens(leitor, arrayTipos, "atendimento.tiposDeficiencia", LerTipoDeDeficiencia);
        if (condicoes is null || recursos is null || tipos is null)
        {
            return null;
        }

        Result<OfertaAtendimentoEspecializado> oferta = OfertaAtendimentoEspecializado.Criar(condicoes, recursos, tipos);
        return oferta.IsFailure ? leitor.Propagar<OfertaAtendimentoEspecializado>(oferta.Error!) : oferta.Value;
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

    private static OfertaCondicao? LerCondicao(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(item, path, "condicaoOrigemId", "condicaoCodigo", "condicaoNome");

        Guid origemId = leitor.Identificador(item, "condicaoOrigemId", path);
        string codigo = leitor.TextoNaoVazio(item, "condicaoCodigo", path, LimitesDoEnvelope.CondicaoCodigo);

        // O código da condição é chave natural: é por ele que a invariante reconhece a condição PcD.
        // Um código fora do formato do cadastro nunca é produzido por ele.
        if (!leitor.Falhou && !LeituraDasModalidades.FormatoCodigoCondicao.IsMatch(codigo))
        {
            return leitor.Malformado<OfertaCondicao?>(
                path,
                $"o código de condição '{codigo}' não tem o formato do cadastro (maiúscula inicial, depois maiúsculas, dígitos e underscore).");
        }

        string nome = leitor.TextoNaoVazio(item, "condicaoNome", path, LimitesDoEnvelope.NomeDeCadastro);
        return leitor.Falhou ? null : OfertaCondicao.Criar(origemId, codigo, nome);
    }

    private static OfertaRecurso? LerRecurso(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(item, path, "recursoOrigemId", "recursoCodigo", "recursoNome");

        Guid origemId = leitor.Identificador(item, "recursoOrigemId", path);
        string codigo = leitor.TextoNaoVazio(item, "recursoCodigo", path, LimitesDoEnvelope.RecursoCodigo);
        string nome = leitor.TextoNaoVazio(item, "recursoNome", path, LimitesDoEnvelope.NomeDeCadastro);
        return leitor.Falhou ? null : OfertaRecurso.Criar(origemId, codigo, nome);
    }

    private static OfertaTipoDeficiencia? LerTipoDeDeficiencia(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(item, path, "tipoDeficienciaOrigemId", "tipoDeficienciaCodigo", "tipoDeficienciaNome");

        Guid origemId = leitor.Identificador(item, "tipoDeficienciaOrigemId", path);
        string codigo = leitor.TextoNaoVazio(item, "tipoDeficienciaCodigo", path, LimitesDoEnvelope.TipoDeficienciaCodigo);
        string nome = leitor.TextoNaoVazio(item, "tipoDeficienciaNome", path, LimitesDoEnvelope.NomeDeCadastro);
        return leitor.Falhou ? null : OfertaTipoDeficiencia.Criar(origemId, codigo, nome);
    }

    /// <summary>
    /// O bônus tem <b>duas formas fechadas</b>: <c>{"presente": false}</c> e a completa.
    /// Fechar as duas é o que impede o pior modo de falha desta leitura — um
    /// <c>{"presente":false,"fator":"1.2000",…}</c> lido como “sem bônus”
    /// <b>descartaria o bônus regional do certame</b> (Fator de Atenuação das Desigualdades de Acesso, UNI-REQ-0015) sem deixar rastro.
    /// </summary>
    internal static ConfiguracaoBonusRegional? LerBonusRegional(LeitorEnvelope leitor, JsonObject payload)
    {
        if (leitor.ObjetoSePresente(payload, "bonusRegional") is not { } bloco)
        {
            return null;
        }

        leitor.ExigirChaves(
            bloco, "bonusRegional", "presente", "regra", "fator", "teto", "fatoElegibilidadeCodigo",
            "baseLegalBonusRegionalId", "tipoInstrumento", "identificacao", "descricao", "municipios");

        ReferenciaRegra regra = leitor.Regra(bloco, "regra", "bonusRegional", RegraBonusCodigo.Multiplicativo);
        decimal fator = leitor.Decimal(bloco, "fator", EscritaCanonica.EscalaPadrao, "bonusRegional", LimitesDoEnvelope.PrecisaoBonus);
        decimal? teto = leitor.DecimalOpcional(bloco, "teto", EscritaCanonica.EscalaPadrao, "bonusRegional", LimitesDoEnvelope.PrecisaoBonus);
        string? fatoElegibilidade = leitor.TextoOpcional(bloco, "fatoElegibilidadeCodigo", "bonusRegional", LimitesDoEnvelope.FatoElegibilidadeBonusRegional);
        Guid baseLegalBonusRegionalId = leitor.Identificador(bloco, "baseLegalBonusRegionalId", "bonusRegional");
        string tipoInstrumento = leitor.TextoNaoVazio(bloco, "tipoInstrumento", "bonusRegional", LimitesDoEnvelope.TipoInstrumentoNormativo);
        string identificacao = leitor.TextoNaoVazio(bloco, "identificacao", "bonusRegional", LimitesDoEnvelope.IdentificacaoBaseLegalBonusRegional);
        string descricao = leitor.TextoNaoVazio(bloco, "descricao", "bonusRegional", LimitesDoEnvelope.DescricaoBaseLegalBonusRegional);
        List<(string CodigoIbge, string Nome, string Uf)>? municipios = LerMunicipios(leitor, leitor.Array(bloco, "municipios", "bonusRegional"));
        if (municipios is null)
        {
            return null;
        }

        Result<ConfiguracaoBonusRegional> bonus = ConfiguracaoBonusRegional.Criar(
            regra, fator, teto, fatoElegibilidade, baseLegalBonusRegionalId, tipoInstrumento, identificacao, descricao, municipios);
        return bonus.IsFailure ? leitor.Propagar<ConfiguracaoBonusRegional>(bonus.Error!) : bonus.Value;
    }

    private static List<(string CodigoIbge, string Nome, string Uf)>? LerMunicipios(LeitorEnvelope leitor, JsonArray arrayMunicipios)
    {
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

        return municipios;
    }
}
