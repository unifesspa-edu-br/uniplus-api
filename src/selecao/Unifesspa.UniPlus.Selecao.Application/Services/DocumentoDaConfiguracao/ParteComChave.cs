namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

/// <summary>Até onde a chave de uma parte do documento é única.</summary>
public enum EscopoDaChave
{
    /// <summary>Entre todas as partes do mesmo tipo no documento.</summary>
    Documento,

    /// <summary>Entre as partes do mesmo tipo que pertencem à mesma parte ancestral, como os itens de uma etapa.</summary>
    ParteAncestral,
}

/// <summary>Uma parte do documento que o esquema obriga a ter chave local: o tipo dela e o escopo em que a chave é única.</summary>
/// <param name="Tipo">O nome do tipo da parte, em minúsculas e sem acento (<c>etapa</c>, <c>item</c>), que separa o conjunto de chaves de cada tipo de parte.</param>
/// <param name="Escopo">Onde a chave é única.</param>
public sealed record ParteComChave(string Tipo, EscopoDaChave Escopo);
