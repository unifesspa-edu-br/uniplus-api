namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Collections.Frozen;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Os cadastros citados são lidos uma vez, com um só instante de referência, em que vale a vigência do que se lê;
/// os cadastros vivos são consultados em sequência, sem visão isolada entre eles, e o congelamento os relê.
/// </summary>
public sealed class CadastrosCitadosNoDocumento
{
    private readonly FrozenDictionary<string, UnidadeCitada> _unidades = FrozenDictionary<string, UnidadeCitada>.Empty;
    private readonly FrozenDictionary<string, TipoProcessoSnapshot> _tiposDeProcesso = FrozenDictionary<string, TipoProcessoSnapshot>.Empty;
    private readonly FrozenDictionary<string, OrigemCanonicaDaFase> _fasesCanonicas = FrozenDictionary<string, OrigemCanonicaDaFase>.Empty;
    private readonly FrozenDictionary<string, TipoDeBancaCitado> _tiposDeBanca = FrozenDictionary<string, TipoDeBancaCitado>.Empty;
    private readonly FrozenDictionary<string, CategoriaDeDocumentoCitada> _categoriasDeDocumento = FrozenDictionary<string, CategoriaDeDocumentoCitada>.Empty;
    private readonly FrozenDictionary<string, LocalidadeRegente> _localidades = FrozenDictionary<string, LocalidadeRegente>.Empty;
    private readonly FrozenDictionary<(string Codigo, string Versao), RegraDoCatalogoCitada> _regras = FrozenDictionary<(string Codigo, string Versao), RegraDoCatalogoCitada>.Empty;
    private readonly FrozenSet<string> _tiposDeAto = FrozenSet<string>.Empty;

    public CadastrosCitadosNoDocumento(DateTimeOffset lidoEm) => LidoEm = lidoEm;

    /// <summary>O instante da leitura dos cadastros, único para o documento inteiro: a vigência do que se lê vale nele.</summary>
    public DateTimeOffset LidoEm { get; }

    /// <summary>As unidades vivas, pelo código da unidade: a administradora do processo e as responsáveis pelas fases.</summary>
    public IReadOnlyDictionary<string, UnidadeCitada> Unidades
    {
        get => _unidades;
        init => _unidades = Congelado(value);
    }

    /// <summary>Os tipos de processo ativos, pelo código.</summary>
    public IReadOnlyDictionary<string, TipoProcessoSnapshot> TiposDeProcesso
    {
        get => _tiposDeProcesso;
        init => _tiposDeProcesso = Congelado(value);
    }

    /// <summary>As fases canônicas vivas, pelo código, com o que a fase herda do cadastro.</summary>
    public IReadOnlyDictionary<string, OrigemCanonicaDaFase> FasesCanonicas
    {
        get => _fasesCanonicas;
        init => _fasesCanonicas = Congelado(value);
    }

    /// <summary>Os tipos de banca vivos, pelo código.</summary>
    public IReadOnlyDictionary<string, TipoDeBancaCitado> TiposDeBanca
    {
        get => _tiposDeBanca;
        init => _tiposDeBanca = Congelado(value);
    }

    /// <summary>As categorias de documento vivas, pelo código.</summary>
    public IReadOnlyDictionary<string, CategoriaDeDocumentoCitada> CategoriasDeDocumento
    {
        get => _categoriasDeDocumento;
        init => _categoriasDeDocumento = Congelado(value);
    }

    /// <summary>Os municípios que o Geo conhece, pelo código IBGE.</summary>
    public IReadOnlyDictionary<string, LocalidadeRegente> Localidades
    {
        get => _localidades;
        init => _localidades = Congelado(value);
    }

    /// <summary>As regras do catálogo, pelo par de código e versão.</summary>
    public IReadOnlyDictionary<(string Codigo, string Versao), RegraDoCatalogoCitada> Regras
    {
        get => _regras;
        init => _regras = (value ?? throw new ArgumentNullException(nameof(value))).ToFrozenDictionary();
    }

    /// <summary>Os códigos dos tipos de ato com versão vigente em <see cref="LidoEm"/>.</summary>
    public IReadOnlySet<string> TiposDeAto
    {
        get => _tiposDeAto;
        init => _tiposDeAto = (value ?? throw new ArgumentNullException(nameof(value))).ToFrozenSet(StringComparer.Ordinal);
    }

    internal UnidadeCitada? Unidade(string? codigo) => Achado(_unidades, codigo);

    internal TipoProcessoSnapshot? TipoDeProcesso(string? codigo) => Achado(_tiposDeProcesso, codigo);

    internal OrigemCanonicaDaFase? FaseCanonica(string? codigo) => Achado(_fasesCanonicas, codigo);

    internal TipoDeBancaCitado? TipoDeBanca(string? codigo) => Achado(_tiposDeBanca, codigo);

    internal CategoriaDeDocumentoCitada? Categoria(string? codigo) => Achado(_categoriasDeDocumento, codigo);

    internal LocalidadeRegente? Localidade(string? codigoIbge) => Achado(_localidades, codigoIbge);

    internal bool TipoDeAtoVigente(string codigo) => _tiposDeAto.Contains(codigo);

    internal RegraDoCatalogoCitada? Regra(string? codigo, string? versao) =>
        codigo is not null && versao is not null && _regras.TryGetValue((codigo, versao), out RegraDoCatalogoCitada? regra) ? regra : null;

    private static T? Achado<T>(IReadOnlyDictionary<string, T> cadastro, string? codigo)
        where T : class =>
        codigo is not null && cadastro.TryGetValue(codigo, out T? achado) ? achado : null;

    private static FrozenDictionary<string, T> Congelado<T>(IReadOnlyDictionary<string, T>? cadastro) =>
        (cadastro ?? throw new ArgumentNullException(nameof(cadastro))).ToFrozenDictionary(StringComparer.Ordinal);
}

/// <summary>A unidade que o documento cita: o id dela no cadastro e a cópia que o processo guarda; a fase a guarda só como responsável.</summary>
public sealed record UnidadeCitada(Guid OrigemId, UnidadeAdministradoraSnapshot Cadastro)
{
    internal UnidadeResponsavelSnapshot ComoResponsavel() => new(OrigemId, Cadastro.Sigla, Cadastro.Nome);
}

/// <summary>O tipo de banca que o documento cita.</summary>
public sealed record TipoDeBancaCitado(Guid OrigemId, string Codigo, string Nome);

/// <summary>A categoria de documento que o documento cita no recorte de competência de uma banca.</summary>
public sealed record CategoriaDeDocumentoCitada(Guid OrigemId, string Codigo);

/// <summary>A regra do catálogo que o documento cita: o tipo dela e a referência que o processo congela.</summary>
public sealed record RegraDoCatalogoCitada(TipoRegra Tipo, ReferenciaRegra Referencia);
