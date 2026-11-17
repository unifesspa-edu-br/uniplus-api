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

        List<CategoriaJulgada> recorte = [];
        for (int i = 0; i < array.Count; i++)
        {
            string path = $"{pathPai}.recorteDeCompetencia[{i}]";
            JsonObject item = leitor.ItemObjeto(array, i, $"{pathPai}.recorteDeCompetencia");
            leitor.ExigirChaves(item, path, "categoriaDocumentoOrigemId", "codigo");

            Guid categoriaDocumentoOrigemId = leitor.Identificador(item, "categoriaDocumentoOrigemId", path);
            string codigo = leitor.TextoNaoVazio(item, "codigo", path, LimitesDoEnvelope.CategoriaDocumentoCodigo);

            if (leitor.Falhou)
            {
                return [];
            }

            recorte.Add(CategoriaJulgada.Criar(categoriaDocumentoOrigemId, codigo));
        }

        return recorte;
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

        string path = $"{pathPai}.regraRecurso";
        leitor.ExigirChaves(bloco, path, "regra", "produtoAncoraId", "parametros", "procedimentos");

        ReferenciaRegra regra = leitor.Regra(bloco, "regra", path, RegraPrazoRecursoCodigo.AncoradoEmAto);
        Guid produtoAncoraId = leitor.Identificador(bloco, "produtoAncoraId", path);
        JsonObject parametrosObjeto = leitor.Objeto(bloco, "parametros", path);
        if (leitor.Falhou)
        {
            return null;
        }

        ParametrosRegraPrazoRecurso? parametros = LerParametrosDePrazoDeRecurso(leitor, parametrosObjeto, $"{path}.parametros");
        if (leitor.Falhou || parametros is null)
        {
            return null;
        }

        ProcedimentosDeRecurso? procedimentos = LeituraDosProcedimentosDeRecurso.Ler(leitor, bloco, path, CodigosDoProcedimentoDeRecurso.DaFase);
        if (leitor.Falhou || procedimentos is null)
        {
            return null;
        }

        Result<RegraRecursoFase> regraRecurso = RegraRecursoFase.Reidratar(regra, parametros, produtoAncoraId, procedimentos);
        return regraRecurso.IsFailure ? leitor.Propagar<RegraRecursoFase>(regraRecurso.Error!) : regraRecurso.Value;
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
        leitor.ExigirChaves(
            parametrosObjeto, parametrosPath,
            "prazoValor", "prazoUnidade",
            "suspensividadePrimeiraInstanciaValor", "suspensividadePrimeiraInstanciaUnidade",
            "admiteInstanciaSuperior",
            "suspensividadeSegundaInstanciaValor", "suspensividadeSegundaInstanciaUnidade");

        decimal prazoValor = leitor.Decimal(parametrosObjeto, "prazoValor", EscritaCanonica.EscalaPadrao, parametrosPath, LimitesDoEnvelope.PrecisaoPrazo);
        UnidadePrazo prazoUnidade = leitor.Enumeracao<UnidadePrazo>(parametrosObjeto, "prazoUnidade", parametrosPath);
        decimal? suspensividade1Valor = leitor.DecimalOpcional(
            parametrosObjeto, "suspensividadePrimeiraInstanciaValor", EscritaCanonica.EscalaPadrao, parametrosPath, LimitesDoEnvelope.PrecisaoPrazo);
        UnidadePrazo? suspensividade1Unidade = leitor.EnumeracaoOpcional<UnidadePrazo>(
            parametrosObjeto, "suspensividadePrimeiraInstanciaUnidade", parametrosPath);
        bool admiteInstanciaSuperior = leitor.Booleano(parametrosObjeto, "admiteInstanciaSuperior", parametrosPath);
        decimal? suspensividade2Valor = leitor.DecimalOpcional(
            parametrosObjeto, "suspensividadeSegundaInstanciaValor", EscritaCanonica.EscalaPadrao, parametrosPath, LimitesDoEnvelope.PrecisaoPrazo);
        UnidadePrazo? suspensividade2Unidade = leitor.EnumeracaoOpcional<UnidadePrazo>(
            parametrosObjeto, "suspensividadeSegundaInstanciaUnidade", parametrosPath);

        return leitor.Falhou
            ? null
            : new ParametrosRegraPrazoRecurso(
                prazoValor, prazoUnidade,
                suspensividade1Valor, suspensividade1Unidade,
                admiteInstanciaSuperior,
                suspensividade2Valor, suspensividade2Unidade);
    }
}
