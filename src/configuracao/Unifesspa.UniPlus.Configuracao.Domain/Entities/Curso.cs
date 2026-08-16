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
    /// Valida e normaliza os cinco campos editáveis, acumulando toda violação
    /// independente em vez de parar na primeira — sem mutar nada. Existe para o
    /// handler validar o payload por inteiro antes de qualquer I/O (checagem de
    /// unicidade do código, leitura por Id). Os cinco campos são independentes,
    /// sem gating cruzado entre eles.
    /// </summary>
    public static Result<(string Codigo, string Nome, string Grau, string NivelEnsino, GrupoCurso? GrupoAreaEnem)>
        ValidarCamposEditaveis(string? codigo, string? nome, string? grau, string? nivelEnsino, string? grupoAreaEnem)
    {
        List<FieldError> erros = [];

        string? codigoNormalizado = null;
        if (string.IsNullOrWhiteSpace(codigo))
        {
            erros.Add(new("codigo", new DomainError(
                CursoErrorCodes.CodigoObrigatorio, "Código do curso é obrigatório.")));
        }
        else
        {
            codigoNormalizado = codigo.Trim();
            if (codigoNormalizado.Length is < CodigoMinLength or > CodigoMaxLength)
            {
                erros.Add(new("codigo", new DomainError(
                    CursoErrorCodes.CodigoTamanho,
                    $"Código do curso deve ter entre {CodigoMinLength} e {CodigoMaxLength} caracteres.")));
                codigoNormalizado = null;
            }
        }

        string? nomeNormalizado = null;
        if (string.IsNullOrWhiteSpace(nome))
        {
            erros.Add(new("nome", new DomainError(
                CursoErrorCodes.NomeObrigatorio, "Nome do curso é obrigatório.")));
        }
        else
        {
            nomeNormalizado = nome.Trim();
            if (nomeNormalizado.Length is < NomeMinLength or > NomeMaxLength)
            {
                erros.Add(new("nome", new DomainError(
                    CursoErrorCodes.NomeTamanho,
                    $"Nome do curso deve ter entre {NomeMinLength} e {NomeMaxLength} caracteres.")));
                nomeNormalizado = null;
            }
        }

        string? grauNormalizado = null;
        if (string.IsNullOrWhiteSpace(grau))
        {
            erros.Add(new("grau", new DomainError(
                CursoErrorCodes.GrauObrigatorio, "Grau do curso é obrigatório.")));
        }
        else
        {
            grauNormalizado = grau.Trim();
            if (grauNormalizado.Length is < GrauMinLength or > GrauMaxLength)
            {
                erros.Add(new("grau", new DomainError(
                    CursoErrorCodes.GrauTamanho,
                    $"Grau do curso deve ter entre {GrauMinLength} e {GrauMaxLength} caracteres.")));
                grauNormalizado = null;
            }
        }

        string? nivelEnsinoNormalizado = null;
        if (string.IsNullOrWhiteSpace(nivelEnsino))
        {
            erros.Add(new("nivelEnsino", new DomainError(
                CursoErrorCodes.NivelEnsinoObrigatorio, "Nível de ensino do curso é obrigatório.")));
        }
        else
        {
            nivelEnsinoNormalizado = nivelEnsino.Trim();
            if (nivelEnsinoNormalizado.Length is < NivelEnsinoMinLength or > NivelEnsinoMaxLength)
            {
                erros.Add(new("nivelEnsino", new DomainError(
                    CursoErrorCodes.NivelEnsinoTamanho,
                    $"Nível de ensino do curso deve ter entre {NivelEnsinoMinLength} e {NivelEnsinoMaxLength} caracteres.")));
                nivelEnsinoNormalizado = null;
            }
        }

        // Grupo de área do ENEM é opcional: nem todo curso classifica por área.
        GrupoCurso? grupoResolvido = null;
        if (!string.IsNullOrWhiteSpace(grupoAreaEnem))
        {
            Result<GrupoCurso> grupo = GrupoCurso.Criar(grupoAreaEnem);
            if (grupo.IsFailure)
            {
                erros.Add(new("grupoAreaEnem", new DomainError(
                    CursoErrorCodes.GrupoAreaEnemInvalido, grupo.Error!.Message)));
            }
            else
            {
                grupoResolvido = grupo.Value;
            }
        }

        if (erros.Count > 0)
        {
            return Result<(string, string, string, string, GrupoCurso?)>.ValidationFailure(erros);
        }

        return Result<(string, string, string, string, GrupoCurso?)>.Success(
            (codigoNormalizado!, nomeNormalizado!, grauNormalizado!, nivelEnsinoNormalizado!, grupoResolvido));
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
    }
}
