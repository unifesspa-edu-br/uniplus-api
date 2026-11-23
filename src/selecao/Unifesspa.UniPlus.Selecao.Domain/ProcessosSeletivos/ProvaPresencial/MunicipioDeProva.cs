namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;

/// <summary>
/// Um município em que a prova é aplicada: o código do IBGE, que é a autoridade, e a UF a que ele
/// pertence. O nome é descritivo e vem do Geo na conferência.
/// </summary>
public sealed record MunicipioDeProva(string CodigoIbge, string Uf, string? Nome, int Posicao);

/// <summary>O município de prova como a configuração o informa, ainda por conferir.</summary>
public sealed record MunicipioDeProvaInformado(string? CodigoIbge, string? Uf);
