namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Leitura da regra de recurso de uma fase: o recorte de competência e os prazos.
/// </summary>
internal static class LeituraDosPrazosDeRecurso
{
    /// <summary>
    /// O recorte de competência da banca — as categorias de documento que ela julga. Como
    /// as próprias bancas, as categorias não congelam <c>id</c>: nada aponta para a linha
    /// de fora dela, e a reidratação as recria por inteiro.
    /// </summary>
    internal static List<CategoriaJulgada> LerRecorteDeCompetencia(LeitorEnvelope leitor, JsonObject bancaItem, string pathPai)
    {
        JsonArray array = leitor.Array(bancaItem, "recorteDeCompetencia", pathPai);
        if (leitor.Falhou)
        {
            return [];
        }

        return LerCategorias(leitor, array, pathPai);
    }

    private static List<CategoriaJulgada> LerCategorias(LeitorEnvelope leitor, JsonArray array, string pathPai)
    {
        List<CategoriaJulgada> recorte = [];
        for (int i = 0; i < array.Count; i++)
        {
            CategoriaJulgada? categoria = LerCategoria(leitor, array, i, pathPai);
            if (leitor.Falhou)
            {
                return [];
            }

            recorte.Add(categoria!);
        }

        return recorte;
    }

    private static CategoriaJulgada? LerCategoria(LeitorEnvelope leitor, JsonArray array, int indice, string pathPai)
    {
        string path = $"{pathPai}.recorteDeCompetencia[{indice}]";
        JsonObject item = leitor.ItemObjeto(array, indice, $"{pathPai}.recorteDeCompetencia");
        leitor.ExigirChaves(item, path, "categoriaDocumentoOrigemId", "codigo");
        Guid id = leitor.Identificador(item, "categoriaDocumentoOrigemId", path);
        string codigo = leitor.TextoNaoVazio(item, "codigo", path, LimitesDoEnvelope.CategoriaDocumentoCodigo);
        return leitor.Falhou ? null : CategoriaJulgada.Criar(id, codigo);
    }

    /// <summary>
    /// A regra de recurso da fase (0..1 — a PRESENÇA do bloco é o que faz a fase admitir
    /// recurso). O vocabulário de <c>regra.codigo</c> é fechado a
    /// <see cref="RegraPrazoRecursoCodigo.AncoradoEmAto"/> — a única variante que
    /// <see cref="RegraRecursoFase"/> admite.
    /// </summary>
    /// <remarks>
    /// O <c>produtoAncoraId</c> é irmão de <c>regra</c> e <c>parametros</c>, e não campo de
    /// <c>parametros</c>: ele não é parâmetro que alguém preencha, e sim a referência que a fase
    /// deriva do próprio produto preliminar. A reidratação o repõe como estava congelado —
    /// é ele que o ato já publicado resolve de volta para achar o prazo que lhe corresponde
    /// (UNI-REQ-0093).
    /// </remarks>
    internal static RegraRecursoFase? LerRegraRecursoFase(LeitorEnvelope leitor, JsonObject faseItem, string pathPai)
    {
        JsonObject? bloco = leitor.ObjetoOpcional(faseItem, "regraRecurso", pathPai);
        if (leitor.Falhou || bloco is null)
        {
            return null;
        }

        return LerRegra(leitor, bloco, $"{pathPai}.regraRecurso");
    }

    private static RegraRecursoFase? LerRegra(LeitorEnvelope leitor, JsonObject bloco, string path)
    {
        leitor.ExigirChaves(bloco, path, "regra", "produtoAncoraId", "parametros", "procedimentos");
        ReferenciaRegra regra = leitor.Regra(bloco, "regra", path, RegraPrazoRecursoCodigo.AncoradoEmAto);
        Guid ancora = leitor.Identificador(bloco, "produtoAncoraId", path);
        JsonObject objeto = leitor.Objeto(bloco, "parametros", path);
        return leitor.Falhou ? null : MontarRegra(leitor, bloco, path, regra, ancora, objeto);
    }

    private static RegraRecursoFase? MontarRegra(LeitorEnvelope leitor, JsonObject bloco, string path,
        ReferenciaRegra regra, Guid ancora, JsonObject objeto)
    {
        ParametrosRegraPrazoRecurso? parametros = LerParametrosDePrazoDeRecurso(leitor, objeto, $"{path}.parametros");
        if (leitor.Falhou || parametros is null)
        {
            return null;
        }

        ProcedimentosDeRecurso? procedimentos = LeituraDosProcedimentosDeRecurso.Ler(leitor, bloco, path, CodigosDoProcedimentoDeRecurso.DaFase);
        if (leitor.Falhou || procedimentos is null)
        {
            return null;
        }

        Result<RegraRecursoFase> resultado = RegraRecursoFase.Reidratar(regra, parametros, ancora, procedimentos);
        return resultado.IsFailure ? leitor.Propagar<RegraRecursoFase>(resultado.Errors) : resultado.Value;
    }

    /// <summary>
    /// Lê os parâmetros do prazo de recurso — prazo, os dois pares de suspensividade e a declaração de instância superior.
    /// </summary>
    /// <remarks>
    /// Um lugar só porque fase e etapa carregam o mesmo value object: lê-lo em dois lugares deixaria um deles
    /// reconstruir os parâmetros com as suspensividades nulas, e o que o edital prometeu ao candidato voltaria vazio
    /// do envelope sem que nada acusasse.
    /// </remarks>
    internal static ParametrosRegraPrazoRecurso? LerParametrosDePrazoDeRecurso(
        LeitorEnvelope leitor,
        JsonObject parametrosObjeto,
        string parametrosPath)
    {
        ExigirChavesDosParametros(leitor, parametrosObjeto, parametrosPath);
        return LerParametros(leitor, parametrosObjeto, parametrosPath);
    }

    private static void ExigirChavesDosParametros(LeitorEnvelope leitor, JsonObject objeto, string path) =>
        leitor.ExigirChaves(objeto, path, "prazoValor", "prazoUnidade",
            "suspensividadePrimeiraInstanciaValor", "suspensividadePrimeiraInstanciaUnidade",
            "admiteInstanciaSuperior", "suspensividadeSegundaInstanciaValor", "suspensividadeSegundaInstanciaUnidade");

    private static ParametrosRegraPrazoRecurso? LerParametros(LeitorEnvelope leitor, JsonObject parametrosObjeto, string parametrosPath)
    {
        decimal prazoValor = leitor.Decimal(parametrosObjeto, "prazoValor", EscritaCanonica.EscalaPadrao, parametrosPath, LimitesDoEnvelope.PrecisaoPrazo);
        UnidadePrazo prazoUnidade = leitor.Enumeracao<UnidadePrazo>(parametrosObjeto, "prazoUnidade", parametrosPath);
        (decimal? valor1, UnidadePrazo? unidade1) = LerSuspensividade(leitor, parametrosObjeto, parametrosPath, "Primeira");
        bool admiteInstanciaSuperior = leitor.Booleano(parametrosObjeto, "admiteInstanciaSuperior", parametrosPath);
        (decimal? valor2, UnidadePrazo? unidade2) = LerSuspensividade(leitor, parametrosObjeto, parametrosPath, "Segunda");

        return leitor.Falhou
            ? null
            : new ParametrosRegraPrazoRecurso(
                prazoValor, prazoUnidade,
                valor1, unidade1,
                admiteInstanciaSuperior,
                valor2, unidade2);
    }

    private static (decimal? Valor, UnidadePrazo? Unidade) LerSuspensividade(
        LeitorEnvelope leitor, JsonObject objeto, string path, string instancia)
    {
        string prefixo = $"suspensividade{instancia}Instancia";
        decimal? valor = leitor.DecimalOpcional(objeto, $"{prefixo}Valor", EscritaCanonica.EscalaPadrao, path, LimitesDoEnvelope.PrecisaoPrazo);
        UnidadePrazo? unidade = leitor.EnumeracaoOpcional<UnidadePrazo>(objeto, $"{prefixo}Unidade", path);
        return (valor, unidade);
    }
}
