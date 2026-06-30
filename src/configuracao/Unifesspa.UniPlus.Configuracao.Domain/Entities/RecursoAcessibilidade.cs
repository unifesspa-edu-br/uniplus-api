namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Recurso de acessibilidade — cadastro institucional do <b>apoio concreto</b>
/// oferecido no atendimento especializado (UNI-REQ-0012, módulo Configuração):
/// nomeia o recurso disponibilizado ao candidato (ledor, tempo adicional, prova
/// ampliada, intérprete de Libras, sala reservada…), nunca uma regra material
/// sobre quando ou a quem se aplica — essas regras vivem na exigência/solicitação
/// de atendimento do edital (banco de Seleção).
/// </summary>
/// <remarks>
/// <para>O <c>Nome</c> é a chave natural, único entre recursos vivos (índice único
/// parcial <c>WHERE is_deleted = false</c>) — e <b>editável</b>, pois o consumo
/// cross-módulo é por snapshot-copy desacoplado (ADR-0061): editar o nome vivo não
/// altera o rótulo já congelado numa exigência de Seleção. A unicidade é checada
/// pelo handler (com proteção de corrida via índice).</para>
/// <para>Dado institucional sem PII (LGPD inaplicável). A remoção é sempre
/// soft-delete e nunca bloqueada por referência.</para>
/// </remarks>
public sealed class RecursoAcessibilidade : SoftDeletableEntity, IAuditableEntity
{
    private const int NomeMinLength = 2;
    private const int NomeMaxLength = 200;
    private const int DescricaoMaxLength = 1000;

    public string Nome { get; private set; } = string.Empty;
    public string? Descricao { get; private set; }

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    // EF Core materialization
    private RecursoAcessibilidade()
    {
    }

    /// <summary>
    /// Cria um novo RecursoAcessibilidade. Valida formato/tamanho local. A
    /// unicidade de <paramref name="nome"/> entre recursos vivos é
    /// responsabilidade do handler.
    /// </summary>
    public static Result<RecursoAcessibilidade> Criar(string nome, string? descricao)
    {
        ArgumentNullException.ThrowIfNull(nome);

        Result validacao = ValidarCampos(nome, descricao);
        if (validacao.IsFailure)
        {
            return Result<RecursoAcessibilidade>.Failure(validacao.Error!);
        }

        var recurso = new RecursoAcessibilidade();
        recurso.AplicarCampos(nome, descricao);

        return Result<RecursoAcessibilidade>.Success(recurso);
    }

    /// <summary>
    /// Atualiza os atributos do RecursoAcessibilidade. O <c>Nome</c> é editável;
    /// sua unicidade (quando alterado) é responsabilidade do handler. Revalida
    /// formato/tamanho.
    /// </summary>
    public Result Atualizar(string nome, string? descricao)
    {
        ArgumentNullException.ThrowIfNull(nome);

        Result validacao = ValidarCampos(nome, descricao);
        if (validacao.IsFailure)
        {
            return validacao;
        }

        AplicarCampos(nome, descricao);

        return Result.Success();
    }

    private void AplicarCampos(string nome, string? descricao)
    {
        Nome = nome.Trim();
        Descricao = NormalizarOpcional(descricao);
    }

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static Result ValidarCampos(string nome, string? descricao)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            return Result.Failure(new DomainError(
                RecursoAcessibilidadeErrorCodes.NomeObrigatorio,
                "Nome do recurso de acessibilidade é obrigatório."));
        }

        if (nome.Trim().Length is < NomeMinLength or > NomeMaxLength)
        {
            return Result.Failure(new DomainError(
                RecursoAcessibilidadeErrorCodes.NomeTamanho,
                $"Nome do recurso de acessibilidade deve ter entre {NomeMinLength} e {NomeMaxLength} caracteres."));
        }

        if (descricao is not null && descricao.Trim().Length > DescricaoMaxLength)
        {
            return Result.Failure(new DomainError(
                RecursoAcessibilidadeErrorCodes.DescricaoTamanho,
                $"Descrição do recurso de acessibilidade deve ter no máximo {DescricaoMaxLength} caracteres."));
        }

        return Result.Success();
    }
}
