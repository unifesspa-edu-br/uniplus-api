namespace Unifesspa.UniPlus.Contratos.UnitTests.TestSupport;

/// <summary>Uma operação de um documento OpenAPI gerado por um executável, com o nome que o contrato publica para ela.</summary>
/// <param name="Executavel">O executável que serve o documento.</param>
/// <param name="Documento">O nome do documento, o mesmo do arquivo <c>openapi.{nome}.json</c>.</param>
/// <param name="Verbo">O verbo HTTP.</param>
/// <param name="Caminho">O caminho exatamente como o documento o publica.</param>
/// <param name="Nome">O <c>operationId</c>; nulo ou vazio quando a operação não tem nome.</param>
/// <param name="ParametrosDeConsulta">Os parâmetros que a operação lê da query string.</param>
internal sealed record OperacaoPublicada(
    string Executavel, string Documento, string Verbo, string Caminho, string? Nome, IReadOnlyList<ParametroDeConsulta> ParametrosDeConsulta)
{
    /// <summary>Identifica a operação: o mesmo endpoint, em mais de um documento do executável, tem a mesma identidade.</summary>
    public string Identidade => $"{Executavel} {Verbo} {Caminho}";

    public string Descricao => $"{Verbo} {Caminho} ({Documento})";
}

/// <summary>Os documentos que um executável serve, com as operações de cada um.</summary>
internal sealed record DocumentoPublicado(string Executavel, string Nome, IReadOnlyList<OperacaoPublicada> Operacoes);

/// <summary>Um parâmetro da query string de uma operação.</summary>
/// <param name="Nome">O nome exatamente como o contrato o publica.</param>
/// <param name="TextoLivre">Verdadeiro quando o valor é texto sem lista fechada de valores nem formato (data, identificador, código).</param>
internal sealed record ParametroDeConsulta(string Nome, bool TextoLivre);
