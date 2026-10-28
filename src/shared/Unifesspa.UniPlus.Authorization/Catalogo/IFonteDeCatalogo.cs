namespace Unifesspa.UniPlus.Authorization.Catalogo;

/// <summary>Um enum de permissões incluído no catálogo da aplicação.</summary>
public interface IFonteDeCatalogo
{
    /// <summary>Tipo do enum, para nomear a origem nas mensagens.</summary>
    Type TipoDoEnum { get; }

    /// <summary>Definições do enum, já conferidas.</summary>
    IReadOnlyList<DefinicaoDePermissao> Definicoes();
}

internal sealed class FonteDeCatalogo<TPermissao> : IFonteDeCatalogo
    where TPermissao : struct, Enum
{
    public Type TipoDoEnum => typeof(TPermissao);

    public IReadOnlyList<DefinicaoDePermissao> Definicoes() => CatalogoDePermissoes<TPermissao>.Todas();
}
