namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

/// <summary>
/// Um comando SQL da semente. Quando o comando só grava o item sob condição, <see cref="Item"/> diz qual é, para a
/// aplicação registrar o item que o banco não gravou.
/// </summary>
/// <param name="Texto">O comando, executado como texto.</param>
/// <param name="Item">O item que a condição do comando pode pular, ou nulo quando o comando não é condicional.</param>
internal sealed record ComandoDaSemente(string Texto, ItemDaSemente? Item = null)
{
    public static ComandoDaSemente SemItem(string texto) => new(texto);
}

/// <summary>O que a semente grava só quando a condição do comando permite.</summary>
internal enum TipoDoItemDaSemente
{
    Termo,
    VersaoDoTermo,
    Modelo,
}

/// <summary>
/// O item que um comando condicional grava, com o código que o identifica no registro do que foi pulado: o do termo, ou
/// o do modelo. A versão do termo é identificada pelo código do termo dela.
/// </summary>
/// <param name="Tipo">O que o item é.</param>
/// <param name="Id">O identificador fixo do item.</param>
/// <param name="Codigo">O código do termo ou do modelo.</param>
/// <param name="TermoId">O identificador do termo a que a versão pertence, só para o item que é versão do termo.</param>
internal sealed record ItemDaSemente(TipoDoItemDaSemente Tipo, Guid Id, string Codigo, Guid? TermoId = null)
{
    /// <summary>O que o item é, como o registro do que foi pulado o diz.</summary>
    public string Descricao => Tipo switch
    {
        TipoDoItemDaSemente.Termo => "termo",
        TipoDoItemDaSemente.VersaoDoTermo => "versão do termo",
        _ => "modelo",
    };
}
