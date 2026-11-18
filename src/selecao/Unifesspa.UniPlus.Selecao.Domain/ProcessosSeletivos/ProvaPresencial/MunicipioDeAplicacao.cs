namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;

/// <summary>
/// Um município em que a prova é aplicada: o código do IBGE, que é a autoridade, e a UF a que ele
/// pertence. O nome é descritivo e vem do Geo na conferência.
/// </summary>
public sealed record MunicipioDeAplicacao(string CodigoIbge, string Uf);

/// <summary>O município de aplicação como a configuração o informa, ainda por conferir.</summary>
public sealed record MunicipioDeAplicacaoInformado(string? CodigoIbge, string? Uf);
