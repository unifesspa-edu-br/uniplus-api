namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Campos que uma listagem aceita no parâmetro de ordenação. É contrato público:
/// os nomes aqui são os que aparecem na consulta, na mensagem de recusa e na
/// documentação da API.
/// </summary>
/// <remarks>
/// A lista é fechada de propósito. Ordenar por coluna arbitrária transformaria a
/// listagem numa consulta livre sobre o banco — sem índice previsível e sem limite
/// de custo. Acrescentar um campo aqui é uma decisão, não uma configuração.
/// </remarks>
public static class CamposOrdenacaoCurso
{
    /// <summary>Nome do curso — o padrão quando a consulta não pede ordenação.</summary>
    public const string Nome = "nome";

    /// <summary>Código do curso.</summary>
    public const string Codigo = "codigo";

    /// <summary>Grau: bacharelado, licenciatura e afins.</summary>
    public const string Grau = "grau";

    /// <summary>Nível de ensino.</summary>
    public const string NivelEnsino = "nivelEnsino";

    /// <summary>Instante de criação do cadastro.</summary>
    public const string CriadoEm = "criadoEm";

    /// <summary>Todos os campos aceitos, na ordem em que a documentação os apresenta.</summary>
    public static IReadOnlyList<string> Todos { get; } =
        [Nome, Codigo, Grau, NivelEnsino, CriadoEm];
}

/// <summary>
/// Campos que a listagem de Ofertas de Curso aceita no parâmetro de ordenação.
/// </summary>
/// <remarks>
/// Os campos do curso vêm da junção que a listagem já faz. Campos do local de
/// oferta ficam de fora enquanto exigirem uma segunda junção — e vale notar que
/// <c>LocalOferta</c> não tem um campo de nome: o que existe é a cidade.
/// </remarks>
public static class CamposOrdenacaoOfertaCurso
{
    /// <summary>Nome do curso ofertado — o padrão quando a consulta não pede ordenação.</summary>
    public const string CursoNome = "cursoNome";

    /// <summary>Código do curso ofertado.</summary>
    public const string CursoCodigo = "cursoCodigo";

    /// <summary>Sigla da unidade que oferta.</summary>
    public const string UnidadeOfertanteSigla = "unidadeOfertanteSigla";

    /// <summary>Programa de oferta.</summary>
    public const string ProgramaDeOferta = "programaDeOferta";

    /// <summary>Formato pedagógico.</summary>
    public const string FormatoPedagogico = "formatoPedagogico";

    /// <summary>Regime de funcionamento.</summary>
    public const string RegimeDeFuncionamento = "regimeDeFuncionamento";

    /// <summary>Regime de turno.</summary>
    public const string RegimeDeTurno = "regimeDeTurno";

    /// <summary>Instante de criação do cadastro.</summary>
    public const string CriadoEm = "criadoEm";

    /// <summary>Todos os campos aceitos, na ordem em que a documentação os apresenta.</summary>
    public static IReadOnlyList<string> Todos { get; } =
    [
        CursoNome,
        CursoCodigo,
        UnidadeOfertanteSigla,
        ProgramaDeOferta,
        FormatoPedagogico,
        RegimeDeFuncionamento,
        RegimeDeTurno,
        CriadoEm,
    ];
}
