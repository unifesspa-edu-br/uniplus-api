namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Leitura do Fator de Atenuação das Desigualdades de Acesso (FADA).
/// </summary>
internal static class LeituraDoFada
{
    /// <summary>
    /// A FADA tem <b>duas formas fechadas</b>: <c>{"presente": false}</c> e a completa.
    /// Fechar as duas é o que impede o pior modo de falha desta leitura — um
    /// <c>{"presente":false,"fator":"1.2000",…}</c> lido como “sem FADA”
    /// <b>descartaria o Fator de Atenuação das Desigualdades de Acesso do certame</b> (UNI-REQ-0015) sem deixar rastro.
    /// </summary>
    internal static ConfiguracaoDoFada? LerFada(LeitorEnvelope leitor, JsonObject payload)
    {
        if (leitor.ObjetoSePresente(payload, "fada") is not { } bloco)
        {
            return null;
        }

        ExigirChavesDaFada(leitor, bloco);
        return LerParametrosDaFada(leitor, bloco);
    }

    private static ConfiguracaoDoFada? LerParametrosDaFada(LeitorEnvelope leitor, JsonObject bloco)
    {
        ReferenciaRegra regra = leitor.Regra<RegraDoFada>(bloco, "regra", "fada");
        decimal fator = leitor.Decimal(bloco, "fator", EscritaCanonica.EscalaPadrao, "fada", LimitesDoEnvelope.PrecisaoDoFada);
        decimal? teto = leitor.DecimalOpcional(bloco, "teto", EscritaCanonica.EscalaPadrao, "fada", LimitesDoEnvelope.PrecisaoDoFada);
        string? fatoElegibilidade = leitor.TextoOpcional(bloco, "codigoDoDadoDeElegibilidade", "fada", LimitesDoEnvelope.CodigoDoDadoDeElegibilidadeDoFada);
        BaseLegalCongelada? baseLegal = LerBaseLegalCongelada(leitor, bloco);
        if (baseLegal is null)
        {
            return null;
        }

        Result<ConfiguracaoDoFada> fada = ConfiguracaoDoFada.Criar(regra, fator, teto, fatoElegibilidade, baseLegal);
        return fada.IsFailure ? leitor.Propagar<ConfiguracaoDoFada>(CausasDaFadaNoDocumento(fada.Errors), "fada") : fada.Value;
    }

    private static IReadOnlyList<FieldError> CausasDaFadaNoDocumento(IReadOnlyList<FieldError> causas) =>
        [.. causas.Select(static causa => causa.Field is "regra.codigo" or "fator" or "teto"
            ? causa : LeitorEnvelope.NoProprioItem([causa])[0])];

    private static string LerCodigoDaBaseLegal(LeitorEnvelope leitor, JsonObject bloco)
    {
        string codigo = leitor.TextoNaoVazio(bloco, "baseLegalCodigo", "fada", LimitesDoEnvelope.CodigoDaBaseLegalDoFada);
        if (!leitor.Falhou && !FormatoDoCodigo.TemFormatoValido(codigo))
        {
            leitor.Recusar<object>("'fada.baseLegalCodigo' tem de ter até 80 caracteres, começar por letra maiúscula e ter só maiúsculas, dígitos e '_'.");
        }

        return codigo;
    }

    private static void ExigirChavesDaFada(LeitorEnvelope leitor, JsonObject bloco) =>
        leitor.ExigirChaves(
            bloco, "fada", "presente", "regra", "fator", "teto", "codigoDoDadoDeElegibilidade",
            "baseLegalId", "baseLegalCodigo", "tipoInstrumento", "identificacao", "descricao", "municipios");

    private static BaseLegalCongelada? LerBaseLegalCongelada(LeitorEnvelope leitor, JsonObject bloco)
    {
        Guid id = leitor.Identificador(bloco, "baseLegalId", "fada");
        string codigo = LerCodigoDaBaseLegal(leitor, bloco);
        string tipoInstrumento = leitor.TextoNaoVazio(bloco, "tipoInstrumento", "fada", LimitesDoEnvelope.TipoInstrumentoNormativo);
        string identificacao = leitor.TextoNaoVazio(bloco, "identificacao", "fada", LimitesDoEnvelope.IdentificacaoDaBaseLegalDoFada);
        string descricao = leitor.TextoNaoVazio(bloco, "descricao", "fada", LimitesDoEnvelope.DescricaoDaBaseLegalDoFada);
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
        string codigo = leitor.TextoNaoVazio(item, "codigoIbge", path, LimitesDoEnvelope.MunicipioDoFadaCodigoIbge);
        string nome = leitor.TextoNaoVazio(item, "nome", path, LimitesDoEnvelope.MunicipioDoFadaNome);
        string uf = leitor.TextoNaoVazio(item, "uf", path, LimitesDoEnvelope.MunicipioDoFadaUf);
        return leitor.Falhou ? null : (codigo, nome, uf);
    }
}
