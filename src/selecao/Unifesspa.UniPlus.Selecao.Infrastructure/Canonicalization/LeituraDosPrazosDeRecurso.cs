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
/// Leitura da regra de recurso de uma fase: o recorte de competência e os prazos.
/// </summary>
internal static class LeituraDosPrazosDeRecurso
{
    /// <summary>
    /// O recorte de competência da banca — as categorias de documento que ela julga. Como
    /// as próprias bancas, as categorias não congelam <c>id</c>: nada aponta para a linha
    /// de fora dela, e a reposição as recria por inteiro.
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
    internal static RegraRecursoFase? LerRegraRecursoFase(LeitorEnvelope leitor, JsonObject faseItem, string pathPai)
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

        ArgsRegraPrazoRecurso? args = LerArgsDePrazoDeRecurso(leitor, argsObjeto, $"{path}.args");
        if (leitor.Falhou || args is null)
        {
            return null;
        }

        Result<RegraRecursoFase> regraRecurso = RegraRecursoFase.Reidratar(regra, args, produtoAncoraId);
        return regraRecurso.IsFailure ? leitor.Propagar<RegraRecursoFase>(regraRecurso.Error!) : regraRecurso.Value;
    }

    /// <summary>
    /// Lê os args do prazo de recurso — prazo, os dois pares de suspensividade e a declaração de instância superior.
    /// </summary>
    /// <remarks>
    /// Um lugar só porque fase e etapa carregam o MESMO value object, e enquanto cada uma lia o
    /// seu, a da etapa lia menos: ela reconstruía os args com as quatro suspensividades nulas,
    /// de modo que o que o edital prometeu ao candidato voltava vazio do envelope sem que nada
    /// acusasse.
    /// </remarks>
    internal static ArgsRegraPrazoRecurso? LerArgsDePrazoDeRecurso(
        LeitorEnvelope leitor,
        JsonObject argsObjeto,
        string argsPath)
    {
        leitor.ExigirChaves(
            argsObjeto, argsPath,
            "prazoValor", "prazoUnidade",
            "suspensividadePrimeiraInstanciaValor", "suspensividadePrimeiraInstanciaUnidade",
            "admiteInstanciaSuperior",
            "suspensividadeSegundaInstanciaValor", "suspensividadeSegundaInstanciaUnidade");

        decimal prazoValor = leitor.Decimal(argsObjeto, "prazoValor", EscritaCanonica.EscalaPadrao, argsPath, LimitesDoEnvelope.PrecisaoPrazo);
        UnidadePrazo prazoUnidade = leitor.Enumeracao<UnidadePrazo>(argsObjeto, "prazoUnidade", argsPath);
        decimal? suspensividade1Valor = leitor.DecimalOpcional(
            argsObjeto, "suspensividadePrimeiraInstanciaValor", EscritaCanonica.EscalaPadrao, argsPath, LimitesDoEnvelope.PrecisaoPrazo);
        UnidadePrazo? suspensividade1Unidade = leitor.EnumeracaoOpcional<UnidadePrazo>(
            argsObjeto, "suspensividadePrimeiraInstanciaUnidade", argsPath);
        bool admiteInstanciaSuperior = leitor.Booleano(argsObjeto, "admiteInstanciaSuperior", argsPath);
        decimal? suspensividade2Valor = leitor.DecimalOpcional(
            argsObjeto, "suspensividadeSegundaInstanciaValor", EscritaCanonica.EscalaPadrao, argsPath, LimitesDoEnvelope.PrecisaoPrazo);
        UnidadePrazo? suspensividade2Unidade = leitor.EnumeracaoOpcional<UnidadePrazo>(
            argsObjeto, "suspensividadeSegundaInstanciaUnidade", argsPath);

        return leitor.Falhou
            ? null
            : new ArgsRegraPrazoRecurso(
                prazoValor, prazoUnidade,
                suspensividade1Valor, suspensividade1Unidade,
                admiteInstanciaSuperior,
                suspensividade2Valor, suspensividade2Unidade);
    }
}
