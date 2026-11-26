namespace Unifesspa.UniPlus.Contratos.UnitTests.TestSupport;

/// <summary>
/// A regra do termo de busca no log de requisição: todo parâmetro de query string de texto livre de uma operação é
/// mascarado pelo log ou está nas exceções temporárias, que só carregam código, cursor, ordenação ou endereço do provedor.
/// Texto livre novo reprova até alguém classificar o parâmetro, e assim nenhuma listagem nova grava o termo digitado no log.
/// </summary>
internal static class RegraDosParametrosDeBusca
{
    /// <summary>
    /// Parâmetros que o contrato ainda publica como texto sem lista de valores nem padrão, mas que só carregam código do
    /// cadastro, o cursor emitido pelo servidor, campo de ordenação ou endereço do provedor. Exceção temporária: cada nome
    /// sai daqui quando o contrato passar a publicar os valores aceitos ou o padrão do texto.
    /// </summary>
    public static IReadOnlySet<string> ExcecoesTemporariasDeTextoLivre { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "ato", "cadastro", "cursor", "emissor", "etapa", "fila", "modalidade", "situacao", "sort", "tipoProcesso",
    };

    public static IReadOnlyList<string> NaoCobertos(IEnumerable<OperacaoPublicada> operacoes, IEnumerable<string> nomesMascarados)
    {
        HashSet<string> mascarados = new(nomesMascarados, StringComparer.OrdinalIgnoreCase);

        return [.. operacoes
            .SelectMany(static operacao => operacao.ParametrosDeConsulta.Where(static parametro => parametro.TextoLivre).Select(parametro => (operacao, parametro)))
            .Where(par => !mascarados.Contains(par.parametro.Nome) && !ExcecoesTemporariasDeTextoLivre.Contains(par.parametro.Nome))
            .Select(static par => $"{par.operacao.Descricao}: parâmetro '{par.parametro.Nome}'")
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }
}
