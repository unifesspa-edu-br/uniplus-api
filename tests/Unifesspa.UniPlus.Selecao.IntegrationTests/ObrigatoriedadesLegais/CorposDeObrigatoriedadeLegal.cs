namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ObrigatoriedadesLegais;

using Unifesspa.UniPlus.Kernel.Domain;

internal static class CorposDeObrigatoriedadeLegal
{
    internal static object RegraUniversal(string regraCodigo, object predicado) => new
    {
        tipoProcessoCodigo = TodosOsTiposDeProcesso.Codigo,
        categoria = "ETAPA",
        regraCodigo,
        predicado,
        descricaoHumana = "Edital deve incluir Prova Objetiva",
        baseLegal = "Lei 12.711/2012 art.1º",
        vigenciaInicio = "2026-01-01",
        vigenciaFim = (string?)null,
        atoNormativoUrl = (string?)null,
        portariaInternaCodigo = (string?)null,
    };

    internal static object RegraSemChave(string regraCodigo, object predicado) =>
        RegraMinima(regraCodigo, predicado, TodosOsTiposDeProcesso.Codigo, "x", "Lei", "2026-01-01");

    internal static object RegraComTipoInvalido(string regraCodigo, object predicado) =>
        RegraMinima(regraCodigo, predicado, "SISU_ANTIGO", "Tipo inválido", "Lei", "2026-01-01");

    internal static object RegraIdempotente(string regraCodigo, object predicado) =>
        RegraMinima(regraCodigo, predicado, TodosOsTiposDeProcesso.Codigo, "Regra idempotente", "Lei", "2026-01-01");

    internal static object RegraOriginal(string regraCodigo, object predicado) =>
        RegraMinima(regraCodigo, predicado, TodosOsTiposDeProcesso.Codigo, "Regra original", "Lei", "2026-01-01");

    internal static object RegraDuplicada(string regraCodigo, object predicado) =>
        RegraMinima(regraCodigo, predicado, TodosOsTiposDeProcesso.Codigo, "Regra duplicada com outro conteúdo", "Lei 14.723/2023", "2026-02-01");

    internal static object RegraAtualizada(Guid id, string regraCodigo, object predicado) =>
        Atualizacao(id, regraCodigo, predicado, "Regra atualizada", "Lei 14.723/2023 art.2º — nova");

    internal static object RegraComIdDivergente(Guid id, string regraCodigo, object predicado) =>
        Atualizacao(id, regraCodigo, predicado, "x", "Lei");

    internal static object RegraInicial(string regraCodigo, string categoria, string baseLegal, string tipoProcessoCodigo, object predicado) => new
    {
        tipoProcessoCodigo,
        categoria,
        regraCodigo,
        predicado,
        descricaoHumana = "Seed",
        baseLegal,
        vigenciaInicio = "2026-01-01",
        vigenciaFim = (string?)null,
        atoNormativoUrl = (string?)null,
        portariaInternaCodigo = (string?)null,
    };

    private static object RegraMinima(
        string regraCodigo, object predicado, string tipoProcessoCodigo,
        string descricaoHumana, string baseLegal, string vigenciaInicio) => new
        {
            tipoProcessoCodigo,
            categoria = "OUTROS",
            regraCodigo,
            predicado,
            descricaoHumana,
            baseLegal,
            vigenciaInicio,
        };

    private static object Atualizacao(Guid id, string regraCodigo, object predicado, string descricaoHumana, string baseLegal) => new
    {
        id,
        tipoProcessoCodigo = TodosOsTiposDeProcesso.Codigo,
        categoria = "OUTROS",
        regraCodigo,
        predicado,
        descricaoHumana,
        baseLegal,
        vigenciaInicio = "2026-01-01",
        vigenciaFim = (string?)null,
        atoNormativoUrl = (string?)null,
        portariaInternaCodigo = (string?)null,
    };
}
