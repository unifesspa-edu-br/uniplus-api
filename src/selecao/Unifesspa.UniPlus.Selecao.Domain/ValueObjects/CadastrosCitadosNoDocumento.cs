namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Collections.Frozen;
using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
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
    private readonly FrozenDictionary<string, TipoDeBancaCopiado> _tiposDeBanca = FrozenDictionary<string, TipoDeBancaCopiado>.Empty;
    private readonly FrozenDictionary<string, TipoEtapaSnapshot> _tiposDeEtapa = FrozenDictionary<string, TipoEtapaSnapshot>.Empty;
    private readonly FrozenDictionary<string, QuadroDePesosCitado> _quadrosDePesos = FrozenDictionary<string, QuadroDePesosCitado>.Empty;
    private readonly FrozenDictionary<string, CategoriaDeDocumentoCitada> _categoriasDeDocumento = FrozenDictionary<string, CategoriaDeDocumentoCitada>.Empty;
    private readonly FrozenDictionary<string, LocalidadeRegente> _localidades = FrozenDictionary<string, LocalidadeRegente>.Empty;
    private readonly FrozenDictionary<(string Codigo, string Versao), RegraDoCatalogoCitada> _regras = FrozenDictionary<(string Codigo, string Versao), RegraDoCatalogoCitada>.Empty;
    private readonly FrozenDictionary<string, DescritorFatoCandidato> _dadosDoDesempate = FrozenDictionary<string, DescritorFatoCandidato>.Empty;
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

    /// <summary>Os tipos de banca vivos, pelo código, já como a cópia que a banca guarda.</summary>
    public IReadOnlyDictionary<string, TipoDeBancaCopiado> TiposDeBanca
    {
        get => _tiposDeBanca;
        init => _tiposDeBanca = Congelado(value);
    }

    /// <summary>Os tipos de etapa ativos, pelo código, já como a cópia que a etapa guarda.</summary>
    public IReadOnlyDictionary<string, TipoEtapaSnapshot> TiposDeEtapa
    {
        get => _tiposDeEtapa;
        init => _tiposDeEtapa = Congelado(value);
    }

    /// <summary>Os quadros de pesos por área do ENEM que o cadastro tem, pela resolução como o documento a cita.</summary>
    public IReadOnlyDictionary<string, QuadroDePesosCitado> QuadrosDePesos
    {
        get => _quadrosDePesos;
        init => _quadrosDePesos = Congelado(value);
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

    /// <summary>
    /// A causa de a localidade citada não ter sido conferida, quando o Geo não respondeu; vazia quando ele respondeu, seja para
    /// confirmar o município, seja para dizer que não o conhece. Sem ela, a falta de conferência se confundiria com município inexistente.
    /// </summary>
    public DomainError? LocalidadeNaoConferida { get; init; }

    /// <summary>As regras do catálogo, pelo par de código e versão.</summary>
    public IReadOnlyDictionary<(string Codigo, string Versao), RegraDoCatalogoCitada> Regras
    {
        get => _regras;
        init => _regras = (value ?? throw new ArgumentNullException(nameof(value))).ToFrozenDictionary();
    }

    /// <summary>
    /// O vocabulário de dados do candidato que o desempate por condição pode citar (ADR-0111), pelo código. Só é lido quando o documento cita
    /// essa regra; vazio recusa todo dado, o que mantém fechada a leitura que o recebeu sem o vocabulário.
    /// </summary>
    public IReadOnlyDictionary<string, DescritorFatoCandidato> DadosDoDesempate
    {
        get => _dadosDoDesempate;
        init => _dadosDoDesempate = Congelado(value);
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

    internal TipoDeBancaCopiado? TipoDeBanca(string? codigo) => Achado(_tiposDeBanca, codigo);

    internal TipoEtapaSnapshot? TipoDeEtapa(string? codigo) => Achado(_tiposDeEtapa, codigo);

    internal QuadroDePesosCitado? QuadroDePesos(string? resolucao) => Achado(_quadrosDePesos, resolucao);

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
    internal UnidadeResponsavelSnapshot ComoResponsavel() => new(OrigemId, Cadastro.Codigo, Cadastro.Sigla, Cadastro.Nome);
}

/// <summary>A categoria de documento que o documento cita nos candidatos atendidos por uma banca.</summary>
public sealed record CategoriaDeDocumentoCitada(Guid OrigemId, string Codigo, string Nome);

/// <summary>
/// A regra do catálogo que o documento cita: o tipo dela, a referência que o processo congela e a declaração lida com ela,
/// com que se confere a configuração sem nova consulta ao catálogo.
/// </summary>
public sealed record RegraDoCatalogoCitada(TipoRegra Tipo, ReferenciaRegra Referencia, JsonElement DeclaracaoDaRegra);

/// <summary>
/// O quadro de pesos por área do ENEM da resolução que o documento cita: os grupos de área cadastrados e os rótulos dos grupos que
/// faltam para a resolução estar completa. O quadro incompleto não serve à classificação.
/// </summary>
public sealed record QuadroDePesosCitado(IReadOnlyList<GrupoPesoAreaEnemCongelado> Grupos, IReadOnlyList<string> GruposAusentes)
{
    /// <summary>Se a resolução tem peso cadastrado para todos os grupos de área.</summary>
    public bool Completo => GruposAusentes.Count == 0;
}
