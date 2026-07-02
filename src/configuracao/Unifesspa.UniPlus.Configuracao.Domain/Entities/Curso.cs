namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
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
/// ENEM. Quando informado, valida contra o domínio fechado de quatro grupos
/// (<see cref="GrupoCurso"/>, Res. INEP/ENEM 805/2024) — o pareamento
/// <c>curso.grupo_area_enem ↔ peso_area_enem.grupo_curso</c> é por valor sobre o
/// vocabulário compartilhado, sem FK.</para>
/// <para>Dado institucional sem PII (LGPD inaplicável). A remoção é soft-delete e
/// só é bloqueada quando o curso é referenciado por oferta de curso viva (#749).</para>
/// </remarks>
public sealed class Curso : SoftDeletableEntity, IAuditableEntity
{
    private const int CodigoMinLength = 1;
    private const int CodigoMaxLength = 60;
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

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    // EF Core materialization
    private Curso()
    {
    }

    /// <summary>
    /// Cria um novo Curso. Valida formato/domínio local (incluindo o grupo de
    /// área do ENEM contra o domínio fechado, quando informado — nulo é aceito).
    /// A unicidade de <paramref name="codigo"/> entre cursos vivos é
    /// responsabilidade do handler. Grau e nível de ensino são texto livre
    /// obrigatório (ex.: Bacharelado / Graduação).
    /// </summary>
    public static Result<Curso> Criar(
        string codigo,
        string nome,
        string grau,
        string nivelEnsino,
        string? grupoAreaEnem)
    {
        ArgumentNullException.ThrowIfNull(codigo);
        ArgumentNullException.ThrowIfNull(nome);
        ArgumentNullException.ThrowIfNull(grau);
        ArgumentNullException.ThrowIfNull(nivelEnsino);

        Result<GrupoCurso?> validacao = ValidarCampos(codigo, nome, grau, nivelEnsino, grupoAreaEnem);
        if (validacao.IsFailure)
        {
            return Result<Curso>.Failure(validacao.Error!);
        }

        var curso = new Curso();
        curso.AplicarCampos(codigo, nome, grau, nivelEnsino, validacao.Value);

        return Result<Curso>.Success(curso);
    }

    /// <summary>
    /// Atualiza os atributos do Curso. O <c>Codigo</c> é editável; sua unicidade
    /// (quando alterado) é responsabilidade do handler. Revalida formato/domínio,
    /// incluindo o grupo de área do ENEM (nulo é aceito). O <c>Id</c> é imutável.
    /// </summary>
    public Result Atualizar(
        string codigo,
        string nome,
        string grau,
        string nivelEnsino,
        string? grupoAreaEnem)
    {
        ArgumentNullException.ThrowIfNull(codigo);
        ArgumentNullException.ThrowIfNull(nome);
        ArgumentNullException.ThrowIfNull(grau);
        ArgumentNullException.ThrowIfNull(nivelEnsino);

        Result<GrupoCurso?> validacao = ValidarCampos(codigo, nome, grau, nivelEnsino, grupoAreaEnem);
        if (validacao.IsFailure)
        {
            return Result.Failure(validacao.Error!);
        }

        AplicarCampos(codigo, nome, grau, nivelEnsino, validacao.Value);

        return Result.Success();
    }

    private void AplicarCampos(
        string codigo,
        string nome,
        string grau,
        string nivelEnsino,
        GrupoCurso? grupoAreaEnem)
    {
        Codigo = codigo.Trim();
        Nome = nome.Trim();
        Grau = grau.Trim();
        NivelEnsino = nivelEnsino.Trim();
        GrupoAreaEnem = grupoAreaEnem;
    }

    private static Result<GrupoCurso?> ValidarCampos(
        string codigo,
        string nome,
        string grau,
        string nivelEnsino,
        string? grupoAreaEnem)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return Result<GrupoCurso?>.Failure(new DomainError(
                CursoErrorCodes.CodigoObrigatorio,
                "Código do curso é obrigatório."));
        }

        if (codigo.Trim().Length is < CodigoMinLength or > CodigoMaxLength)
        {
            return Result<GrupoCurso?>.Failure(new DomainError(
                CursoErrorCodes.CodigoTamanho,
                $"Código do curso deve ter entre {CodigoMinLength} e {CodigoMaxLength} caracteres."));
        }

        if (string.IsNullOrWhiteSpace(nome))
        {
            return Result<GrupoCurso?>.Failure(new DomainError(
                CursoErrorCodes.NomeObrigatorio,
                "Nome do curso é obrigatório."));
        }

        if (nome.Trim().Length is < NomeMinLength or > NomeMaxLength)
        {
            return Result<GrupoCurso?>.Failure(new DomainError(
                CursoErrorCodes.NomeTamanho,
                $"Nome do curso deve ter entre {NomeMinLength} e {NomeMaxLength} caracteres."));
        }

        if (string.IsNullOrWhiteSpace(grau))
        {
            return Result<GrupoCurso?>.Failure(new DomainError(
                CursoErrorCodes.GrauObrigatorio,
                "Grau do curso é obrigatório."));
        }

        if (grau.Trim().Length is < GrauMinLength or > GrauMaxLength)
        {
            return Result<GrupoCurso?>.Failure(new DomainError(
                CursoErrorCodes.GrauTamanho,
                $"Grau do curso deve ter entre {GrauMinLength} e {GrauMaxLength} caracteres."));
        }

        if (string.IsNullOrWhiteSpace(nivelEnsino))
        {
            return Result<GrupoCurso?>.Failure(new DomainError(
                CursoErrorCodes.NivelEnsinoObrigatorio,
                "Nível de ensino do curso é obrigatório."));
        }

        if (nivelEnsino.Trim().Length is < NivelEnsinoMinLength or > NivelEnsinoMaxLength)
        {
            return Result<GrupoCurso?>.Failure(new DomainError(
                CursoErrorCodes.NivelEnsinoTamanho,
                $"Nível de ensino do curso deve ter entre {NivelEnsinoMinLength} e {NivelEnsinoMaxLength} caracteres."));
        }

        if (string.IsNullOrWhiteSpace(grupoAreaEnem))
        {
            // Grupo de área do ENEM é opcional: nem todo curso classifica por área.
            return Result<GrupoCurso?>.Success(null);
        }

        Result<GrupoCurso> grupo = GrupoCurso.Criar(grupoAreaEnem);
        if (grupo.IsFailure)
        {
            return Result<GrupoCurso?>.Failure(new DomainError(
                CursoErrorCodes.GrupoAreaEnemInvalido, grupo.Error!.Message));
        }

        return Result<GrupoCurso?>.Success(grupo.Value);
    }
}
