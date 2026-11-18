namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

/// <summary>
/// Os códigos que o documento cita em cada cadastro, sem repetição e na ordem em que aparecem: o que a leitura dos cadastros precisa
/// consultar, uma vez por código. O tipo de processo e a localidade são um só código no documento. A regra do catálogo é citada por código e versão, e a citação sem um dos dois não consulta nada.
/// </summary>
internal sealed record CodigosCitadosPorCadastro(
    IReadOnlyList<string> Unidades,
    string? TipoDeProcesso,
    IReadOnlyList<string> FasesCanonicas,
    IReadOnlyList<string> TiposDeBanca,
    IReadOnlyList<string> CategoriasDeDocumento,
    string? Localidade,
    IReadOnlyList<(string Codigo, string Versao)> Regras,
    IReadOnlyList<string> TiposDeAto);

/// <summary>
/// Junta, nas declarações da identificação e do cronograma, os códigos distintos que o documento cita em cada cadastro: a unidade
/// administradora e as responsáveis pelas fases compartilham o cadastro de unidades, e a regra vem do algoritmo de contagem, da regra
/// de recurso e dos procedimentos. Não confere o que cada código é.
/// </summary>
internal static class CodigosCitadosNoDocumento
{
    internal static CodigosCitadosPorCadastro Em(DeclaracoesDoDocumento declaracoes)
    {
        IdentificacaoNoDocumento identificacao = declaracoes.Identificacao;
        FaseNoDocumento[] fases = [.. declaracoes.Fases];
        BancaNoDocumento[] bancas = [.. fases.SelectMany(static fase => fase.Bancas)];

        return new(
            Distintos([identificacao.UnidadeAdministradoraCodigo, .. fases.Select(static fase => fase.Referencias.UnidadeResponsavelCodigo)]),
            identificacao.TipoProcessoCodigo,
            Distintos(fases.Select(static fase => fase.Referencias.FaseCanonicaCodigo)),
            Distintos(bancas.Select(static banca => banca.TipoBancaCodigo)),
            Distintos(bancas.SelectMany(static banca => banca.CategoriasDocumentoCodigos)),
            identificacao.LocalidadeCodigoIbge,
            Regras(identificacao, fases),
            Distintos(fases.SelectMany(static fase => fase.Publicacoes.Produtos).Select(static produto => produto.AtoCodigo)));
    }

    private static string[] Distintos(IEnumerable<string?> codigos) =>
        [.. codigos.OfType<string>().Distinct(StringComparer.Ordinal)];

    private static (string Codigo, string Versao)[] Regras(IdentificacaoNoDocumento identificacao, FaseNoDocumento[] fases) =>
        [.. ReferenciasDeRegra(identificacao, fases)
            .Where(static referencia => referencia.Codigo is not null && referencia.Versao is not null)
            .Select(static referencia => (referencia.Codigo!, referencia.Versao!))
            .Distinct()];

    private static IEnumerable<ReferenciaDeRegraNoDocumento> ReferenciasDeRegra(IdentificacaoNoDocumento identificacao, FaseNoDocumento[] fases)
    {
        RegraDeRecursoNoDocumento[] regrasDeRecurso = [.. fases.Select(static fase => fase.RegraDeRecurso).OfType<RegraDeRecursoNoDocumento>()];

        return
        [
            .. new[] { identificacao.AlgoritmoContagemPrazo }.OfType<ReferenciaDeRegraNoDocumento>(),
            .. regrasDeRecurso.Select(static regra => regra.Regra),
            .. regrasDeRecurso.SelectMany(static regra => regra.Procedimentos).Select(static procedimento => procedimento.Regra),
        ];
    }
}
