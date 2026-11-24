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
/// Leitura da regra de recurso de uma fase: os candidatos atendidos pela banca e os parâmetros do recurso.
/// </summary>
internal static class LeituraDosPrazosDeRecurso
{
    /// <summary>
    /// Os candidatos atendidos pela banca — as categorias de documento que ela julga. A banca congela o <c>id</c>, porque o
    /// que fica fora da versão a aponta; as categorias não congelam: nada aponta para a linha de fora dela, e a
    /// reconstrução as recria por inteiro.
    /// </summary>
    internal static List<CategoriaJulgada> LerCandidatosAtendidos(LeitorEnvelope leitor, JsonObject bancaItem, string pathPai)
    {
        JsonArray array = leitor.Array(bancaItem, "candidatosAtendidos", pathPai);
        if (leitor.Falhou)
        {
            return [];
        }

        return LerCategorias(leitor, array, pathPai);
    }

    private static List<CategoriaJulgada> LerCategorias(LeitorEnvelope leitor, JsonArray array, string pathPai)
    {
        List<CategoriaJulgada> atendidos = [];
        for (int i = 0; i < array.Count; i++)
        {
            CategoriaJulgada? categoria = LerCategoria(leitor, array, i, pathPai);
            if (leitor.Falhou)
            {
                return [];
            }

            atendidos.Add(categoria!);
        }

        return atendidos;
    }

    private static CategoriaJulgada? LerCategoria(LeitorEnvelope leitor, JsonArray array, int indice, string pathPai)
    {
        string path = $"{pathPai}.candidatosAtendidos[{indice}]";
        JsonObject item = leitor.ItemObjeto(array, indice, $"{pathPai}.candidatosAtendidos");
        leitor.ExigirChaves(item, path, "categoriaDocumentoOrigemId", "codigo", "nome");
        Guid id = leitor.Identificador(item, "categoriaDocumentoOrigemId", path);
        string codigo = leitor.TextoNaoVazio(item, "codigo", path, LimitesDoEnvelope.CategoriaDocumentoCodigo);
        string nome = leitor.TextoNaoVazio(item, "nome", path, LimitesDoEnvelope.CategoriaDocumentoNome);
        return leitor.Falhou ? null : CategoriaJulgada.Criar(id, codigo, nome);
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
        leitor.ExigirChaves(bloco, path, "regra", "publicacaoDeReferenciaId", "parametros", "procedimentos");
        ReferenciaRegra regra = leitor.Regra(bloco, "regra", path, RegraPrazoRecursoCodigo.AncoradoEmAto);
        Guid ancora = leitor.Identificador(bloco, "publicacaoDeReferenciaId", path);
        JsonObject objeto = leitor.Objeto(bloco, "parametros", path);
        return leitor.Falhou ? null : MontarRegra(leitor, bloco, path, regra, ancora, objeto);
    }

    private static RegraRecursoFase? MontarRegra(LeitorEnvelope leitor, JsonObject bloco, string path,
        ReferenciaRegra regra, Guid ancora, JsonObject objeto)
    {
        ParametrosDeclaradosDoRecurso? parametros = LerParametrosDePrazoDeRecurso(leitor, objeto, $"{path}.parametros");
        if (leitor.Falhou || parametros is null)
        {
            return null;
        }

        ProcedimentosDeRecurso? procedimentos = LeituraDosProcedimentosDeRecurso.Ler(leitor, bloco, path, CodigosDoProcedimentoDeRecurso.DaFase);
        if (leitor.Falhou || procedimentos is null)
        {
            return null;
        }

        return Reidratar(leitor, path, RegraRecursoFase.Reidratar(regra, parametros, ancora, procedimentos));
    }

    private static RegraRecursoFase? Reidratar(LeitorEnvelope leitor, string path, Result<RegraRecursoFase> resultado) =>
        resultado.IsFailure
            ? leitor.Propagar<RegraRecursoFase>([.. resultado.Errors.Select(erro => erro.NoCaminhoDe(path))])
            : resultado.Value;

    /// <summary>
    /// Lê os parâmetros do recurso como o registro os traz: os dois prazos, a declaração de instância superior, o efeito
    /// suspensivo e a guarda da vaga, os códigos como texto. Quem confere o código é o domínio, na reidratação.
    /// </summary>
    /// <remarks>
    /// Um lugar só porque fase e etapa carregam os mesmos parâmetros: lê-los em dois lugares deixaria um deles reconstruir os
    /// parâmetros sem um dos campos, e o que o edital prometeu ao candidato voltaria vazio do registro sem que nada acusasse.
    /// </remarks>
    internal static ParametrosDeclaradosDoRecurso? LerParametrosDePrazoDeRecurso(
        LeitorEnvelope leitor,
        JsonObject parametrosObjeto,
        string parametrosPath)
    {
        ExigirChavesDosParametros(leitor, parametrosObjeto, parametrosPath);
        return LerParametros(leitor, parametrosObjeto, parametrosPath);
    }

    private static void ExigirChavesDosParametros(LeitorEnvelope leitor, JsonObject objeto, string path) =>
        leitor.ExigirChaves(objeto, path, "prazoValor", "prazoUnidade", "admiteInstanciaSuperior",
            "prazoSegundaInstanciaValor", "prazoSegundaInstanciaUnidade", "efeitoSuspensivo", "guardaDaVaga");

    private static ParametrosDeclaradosDoRecurso? LerParametros(LeitorEnvelope leitor, JsonObject objeto, string path)
    {
        PrazoDeclarado interposicao = LerPrazo(leitor, objeto, path, "prazo", obrigatorio: true);
        bool admiteInstanciaSuperior = leitor.Booleano(objeto, "admiteInstanciaSuperior", path);
        PrazoDeclarado segundaInstancia = LerPrazo(leitor, objeto, path, "prazoSegundaInstancia", obrigatorio: false);
        string efeito = leitor.Texto(objeto, "efeitoSuspensivo", path);
        string guarda = leitor.Texto(objeto, "guardaDaVaga", path);

        return leitor.Falhou
            ? null
            : new ParametrosDeclaradosDoRecurso(interposicao, admiteInstanciaSuperior, segundaInstancia, efeito, guarda);
    }

    private static PrazoDeclarado LerPrazo(LeitorEnvelope leitor, JsonObject objeto, string path, string prefixo, bool obrigatorio)
    {
        decimal? valor = obrigatorio
            ? leitor.Decimal(objeto, $"{prefixo}Valor", EscritaCanonica.EscalaPadrao, path, LimitesDoEnvelope.PrecisaoPrazo)
            : leitor.DecimalOpcional(objeto, $"{prefixo}Valor", EscritaCanonica.EscalaPadrao, path, LimitesDoEnvelope.PrecisaoPrazo);
        string? unidade = obrigatorio
            ? leitor.Texto(objeto, $"{prefixo}Unidade", path)
            : leitor.TextoOpcional(objeto, $"{prefixo}Unidade", path);
        return new PrazoDeclarado(valor, unidade);
    }
}
