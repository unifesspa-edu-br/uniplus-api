namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
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
internal static partial class LeituraDoBonusEAtendimento
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
        return atendimento.IsFailure ? leitor.Propagar<AtendimentoEspecializado>(atendimento.Error!) : atendimento.Value;
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

    /// <summary>
    /// O bônus tem <b>duas formas fechadas</b>: <c>{"presente": false}</c> e a completa.
    /// Fechar as duas é o que impede o pior modo de falha desta leitura — um
    /// <c>{"presente":false,"fator":"1.2000",…}</c> lido como “sem bônus”
    /// <b>descartaria o bônus regional do certame</b> (Fator de Atenuação das Desigualdades de Acesso, UNI-REQ-0015) sem deixar rastro.
    /// </summary>
    internal static ConfiguracaoBonusRegional? LerBonusRegional(LeitorEnvelope leitor, JsonObject payload)
    {
        if (leitor.ObjetoSePresente(payload, "fada") is not { } bloco)
        {
            return null;
        }

        ExigirChavesDoBonus(leitor, bloco);
        return LerParametrosDoBonus(leitor, bloco);
    }

    private static ConfiguracaoBonusRegional? LerParametrosDoBonus(LeitorEnvelope leitor, JsonObject bloco)
    {
        ReferenciaRegra regra = leitor.Regra<RegraDeBonus>(bloco, "regra", "fada");
        decimal fator = leitor.Decimal(bloco, "fator", EscritaCanonica.EscalaPadrao, "fada", LimitesDoEnvelope.PrecisaoBonus);
        decimal? teto = leitor.DecimalOpcional(bloco, "teto", EscritaCanonica.EscalaPadrao, "fada", LimitesDoEnvelope.PrecisaoBonus);
        string? fatoElegibilidade = leitor.TextoOpcional(bloco, "codigoDoDadoDeElegibilidade", "fada", LimitesDoEnvelope.FatoElegibilidadeBonusRegional);
        BaseLegalCongelada? baseLegal = LerBaseLegalCongelada(leitor, bloco);
        if (baseLegal is null)
        {
            return null;
        }

        Result<ConfiguracaoBonusRegional> bonus = ConfiguracaoBonusRegional.Criar(regra, fator, teto, fatoElegibilidade, baseLegal);
        return bonus.IsFailure ? leitor.Propagar<ConfiguracaoBonusRegional>(bonus.Error!) : bonus.Value;
    }

    private static string LerCodigoDaBaseLegal(LeitorEnvelope leitor, JsonObject bloco)
    {
        string codigo = leitor.TextoNaoVazio(bloco, "baseLegalCodigo", "fada", LimitesDoEnvelope.CodigoBaseLegalBonusRegional);
        if (!leitor.Falhou && !FormatoDoCodigo.TemFormatoValido(codigo))
        {
            leitor.Recusar<object>("'fada.baseLegalCodigo' tem de ter até 80 caracteres, começar por letra maiúscula e ter só maiúsculas, dígitos e '_'.");
        }

        return codigo;
    }

    private static void ExigirChavesDoBonus(LeitorEnvelope leitor, JsonObject bloco) =>
        leitor.ExigirChaves(
            bloco, "fada", "presente", "regra", "fator", "teto", "codigoDoDadoDeElegibilidade",
            "baseLegalId", "baseLegalCodigo", "tipoInstrumento", "identificacao", "descricao", "municipios");

    private static BaseLegalCongelada? LerBaseLegalCongelada(LeitorEnvelope leitor, JsonObject bloco)
    {
        Guid id = leitor.Identificador(bloco, "baseLegalId", "fada");
        string codigo = LerCodigoDaBaseLegal(leitor, bloco);
        string tipoInstrumento = leitor.TextoNaoVazio(bloco, "tipoInstrumento", "fada", LimitesDoEnvelope.TipoInstrumentoNormativo);
        string identificacao = leitor.TextoNaoVazio(bloco, "identificacao", "fada", LimitesDoEnvelope.IdentificacaoBaseLegalBonusRegional);
        string descricao = leitor.TextoNaoVazio(bloco, "descricao", "fada", LimitesDoEnvelope.DescricaoBaseLegalBonusRegional);
        List<(string CodigoIbge, string Nome, string Uf)>? municipios = LerMunicipios(leitor, leitor.Array(bloco, "municipios", "fada"));
        return municipios is null ? null : new BaseLegalCongelada(id, codigo, tipoInstrumento, identificacao, descricao, municipios);
    }

    private static List<(string CodigoIbge, string Nome, string Uf)>? LerMunicipios(LeitorEnvelope leitor, JsonArray arrayMunicipios) =>
        leitor.Falhou ? null : LerListaDeMunicipios(leitor, arrayMunicipios);

    private static List<(string CodigoIbge, string Nome, string Uf)>? LerListaDeMunicipios(LeitorEnvelope leitor, JsonArray arrayMunicipios)
    {
        List<(string CodigoIbge, string Nome, string Uf)> municipios = [];
        for (int i = 0; i < arrayMunicipios.Count; i++)
        {
            if (LerMunicipio(leitor, arrayMunicipios, i) is not { } municipio)
            {
                return null;
            }

            municipios.Add(municipio);
        }

        return municipios;
    }
    private static (string CodigoIbge, string Nome, string Uf)? LerMunicipio(LeitorEnvelope leitor, JsonArray array, int indice)
    {
        string path = $"fada.municipios[{indice}]";
        JsonObject item = leitor.ItemObjeto(array, indice, "fada.municipios");
        leitor.ExigirChaves(item, path, "codigoIbge", "nome", "uf");
        string codigo = leitor.TextoNaoVazio(item, "codigoIbge", path, LimitesDoEnvelope.MunicipioBonusRegionalCodigoIbge);
        string nome = leitor.TextoNaoVazio(item, "nome", path, LimitesDoEnvelope.MunicipioBonusRegionalNome);
        string uf = leitor.TextoNaoVazio(item, "uf", path, LimitesDoEnvelope.MunicipioBonusRegionalUf);
        return leitor.Falhou ? null : (codigo, nome, uf);
    }

}
