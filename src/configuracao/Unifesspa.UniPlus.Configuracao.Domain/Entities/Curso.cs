namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Curso — matriz curricular <b>pura</b> da oferta acadêmica (story #588,
/// ADR-0066, módulo Configuração): diz <i>o que o curso é</i> ("Engenharia
/// Civil", bacharelado, graduação), nunca <i>onde nem como é ofertado</i> —
/// código e-MEC, local de oferta e unidade pertencem à <c>OfertaCurso</c>
/// (task futura, #749).
/// </summary>
/// <remarks>
/// <para>O <c>Codigo</c> é a chave natural, único entre cursos vivos (índice único
/// parcial <c>WHERE is_deleted = false</c>) — e <b>editável</b> (mesmo expediente
/// do TipoDocumento), pois o consumo cross-módulo é por snapshot-copy desacoplado
/// (ADR-0061): editar o código vivo não altera o rótulo já congelado num edital de
/// Seleção. A unicidade é checada pelo handler (com proteção de corrida via índice).</para>
/// <para>O <c>GrupoAreaEnem</c> é opcional: nem todo curso classifica por área do
/// ENEM. Quando informado, é um dos quatro grupos da Resolução nº 805/2024/Consepe
/// (<see cref="GrupoCurso"/>), gravado com código e rótulo — o pareamento
/// <c>cursos.grupo_area_enem ↔ pesos_por_area_do_enem.grupo_curso</c> é pelo código, sem FK.</para>
/// <para>Dado institucional sem PII (LGPD inaplicável). A remoção é soft-delete e
/// só é bloqueada quando o curso é referenciado por oferta de curso viva (#749).</para>
/// </remarks>
public sealed class Curso : SoftDeletableEntity, IAuditableEntity
{
    public const int CodigoMaximo = 60;

    private static readonly CampoDeCodigo CampoCodigo = new(
        "Código do curso", CodigoMaximo, CursoErrorCodes.CodigoObrigatorio, CursoErrorCodes.CodigoFormatoInvalido);
    private const int NomeMinLength = 2;
    private const int NomeMaxLength = 200;
    private const int GrauMinLength = 2;
    private const int GrauMaxLength = 60;
    private const int NivelEnsinoMinLength = 2;
    private const int NivelEnsinoMaxLength = 60;

    public string Codigo { get; private set; } = string.Empty;
    public string Nome { get; private set; } = string.Empty;
    public string Grau { get; private set; } = string.Empty;
    public string NivelEnsino { get; private set; } = string.Empty;
    public GrupoCurso? GrupoAreaEnem { get; private set; }

    // Rótulo do grupo, gravado ao lado do código sempre que o grupo é atribuído. O
    // domínio lê o rótulo do próprio GrupoCurso; a coluna existe para o dado
    // persistido carregar código e rótulo.
    private string? _grupoAreaEnemRotulo;

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    // EF Core materialization
    private Curso()
    {
    }

    /// <summary>
    /// Valida os cinco campos editáveis, acumulando toda violação
    /// independente em vez de parar na primeira — sem mutar nada. Existe para o
    /// handler validar o payload por inteiro antes de qualquer I/O (checagem de
    /// unicidade do código, leitura por Id). Os cinco campos são independentes,
    /// sem gating cruzado entre eles.
    /// </summary>
    public static Result<(string Codigo, string Nome, string Grau, string NivelEnsino, GrupoCurso? GrupoAreaEnem)>
        ValidarCamposEditaveis(string? codigo, string? nome, string? grau, string? nivelEnsino, string? grupoAreaEnem)
    {
        FieldError? recusa = CampoCodigo.Recusa(codigo, "codigo");
        List<FieldError> erros = recusa is null ? [] : [recusa];
        string? nomeValidado = ValidarTexto(nome, "nome", "Nome", NomeMinLength, NomeMaxLength,
            (CursoErrorCodes.NomeObrigatorio, CursoErrorCodes.NomeTamanho), erros);
        string? grauValidado = ValidarTexto(grau, "grau", "Grau", GrauMinLength, GrauMaxLength,
            (CursoErrorCodes.GrauObrigatorio, CursoErrorCodes.GrauTamanho), erros);
        string? nivelValidado = ValidarTexto(nivelEnsino, "nivelEnsino", "Nível de ensino", NivelEnsinoMinLength, NivelEnsinoMaxLength,
            (CursoErrorCodes.NivelEnsinoObrigatorio, CursoErrorCodes.NivelEnsinoTamanho), erros);
        GrupoCurso? grupo = ValidarGrupo(grupoAreaEnem, erros);
        return erros.Count > 0
            ? Result<(string, string, string, string, GrupoCurso?)>.ValidationFailure(erros)
            : Result<(string, string, string, string, GrupoCurso?)>.Success((codigo!, nomeValidado!, grauValidado!, nivelValidado!, grupo));
    }

    private static string? ValidarTexto(string? valor, string campo, string nome, int minimo, int maximo,
        (string Obrigatorio, string Tamanho) codigos, List<FieldError> erros)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            erros.Add(new(campo, new DomainError(codigos.Obrigatorio, $"{nome} do curso é obrigatório.")));
            return null;
        }
        string texto = valor.Trim();
        if (texto.Length >= minimo && texto.Length <= maximo)
        {
            return texto;
        }
        erros.Add(new(campo, new DomainError(codigos.Tamanho, $"{nome} do curso deve ter entre {minimo} e {maximo} caracteres.")));
        return null;
    }

    private static GrupoCurso? ValidarGrupo(string? valor, List<FieldError> erros)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }
        Result<GrupoCurso> grupo = GrupoCurso.Criar(valor);
        if (grupo.IsFailure)
        {
            erros.Add(new("grupoAreaEnem", new DomainError(CursoErrorCodes.GrupoAreaEnemInvalido, grupo.Error!.Message)));
            return null;
        }
        return grupo.Value;
    }

    /// <summary>
    /// Cria um novo Curso. Revalida os cinco campos editáveis, acumulando toda
    /// violação no mesmo lote. A unicidade do código entre cursos vivos é
    /// responsabilidade do handler.
    /// </summary>
    public static Result<Curso> Criar(string? codigo, string? nome, string? grau, string? nivelEnsino, string? grupoAreaEnem)
    {
        Result<(string Codigo, string Nome, string Grau, string NivelEnsino, GrupoCurso? GrupoAreaEnem)> campos =
            ValidarCamposEditaveis(codigo, nome, grau, nivelEnsino, grupoAreaEnem);
        if (campos.IsFailure)
        {
            return Result<Curso>.ValidationFailure(campos.Errors);
        }

        var curso = new Curso();
        curso.AplicarCampos(
            campos.Value.Codigo, campos.Value.Nome, campos.Value.Grau, campos.Value.NivelEnsino, campos.Value.GrupoAreaEnem);

        return Result<Curso>.Success(curso);
    }

    /// <summary>
    /// Atualiza os atributos do Curso. O <c>Codigo</c> é editável; sua unicidade
    /// (quando alterado) é responsabilidade do handler. Revalida os cinco campos
    /// editáveis, acumulando toda violação no mesmo lote. O <c>Id</c> é imutável.
    /// </summary>
    public Result Atualizar(string? codigo, string? nome, string? grau, string? nivelEnsino, string? grupoAreaEnem)
    {
        Result<(string Codigo, string Nome, string Grau, string NivelEnsino, GrupoCurso? GrupoAreaEnem)> campos =
            ValidarCamposEditaveis(codigo, nome, grau, nivelEnsino, grupoAreaEnem);
        if (campos.IsFailure)
        {
            return Result.ValidationFailure(campos.Errors);
        }

        AplicarCampos(
            campos.Value.Codigo, campos.Value.Nome, campos.Value.Grau, campos.Value.NivelEnsino, campos.Value.GrupoAreaEnem);

        return Result.Success();
    }

    private void AplicarCampos(string codigo, string nome, string grau, string nivelEnsino, GrupoCurso? grupoAreaEnem)
    {
        Codigo = codigo;
        Nome = nome;
        Grau = grau;
        NivelEnsino = nivelEnsino;
        GrupoAreaEnem = grupoAreaEnem;
        _grupoAreaEnemRotulo = grupoAreaEnem?.Rotulo;
    }
}
