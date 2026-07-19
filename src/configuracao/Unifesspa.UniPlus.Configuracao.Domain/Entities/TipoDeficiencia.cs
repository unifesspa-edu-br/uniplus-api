namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Tipo de deficiência — cadastro institucional do tipo de deficiência reconhecido
/// (UNI-REQ-0012, módulo Configuração): Visual, Auditiva, TEA, Física, Intelectual…
/// É um cadastro classificatório simples (apenas nome + descrição opcional); a
/// solicitação concreta de atendimento especializado e os recursos de
/// acessibilidade são entidades distintas (vocabulário INEP/Edital ENEM 52/2025).
/// </summary>
/// <remarks>
/// <para>O <c>Nome</c> é a chave natural, único entre tipos vivos (índice único
/// parcial <c>WHERE is_deleted = false</c>) — e <b>editável</b>: a unicidade
/// (quando o nome muda) é checada pelo handler, com proteção de corrida pelo índice
/// (a violação 23505 é traduzida em <c>NomeJaExiste</c>).</para>
/// <para>Dado institucional sem PII (LGPD inaplicável). A remoção é sempre
/// soft-delete e nunca bloqueada por referência.</para>
/// <para>
/// <c>Descricao</c> é <b>obrigatória</b> (ADR-0116): serve também como a
/// descrição por valor exigida pela spec para o fato <c>TIPO_DEFICIENCIA</c>
/// (<c>DECLARADO</c>), exposta via <c>ITipoDeficienciaReader</c>. <c>Permanente</c>
/// é anulável — <see langword="null"/> significa "ainda não classificado pelo
/// CEPS", distinto de <see langword="false"/> ("classificado como
/// não-permanente"); a taxonomia concreta é refinamento residual que não bloqueia
/// este modelo.
/// </para>
/// </remarks>
public sealed class TipoDeficiencia : SoftDeletableEntity, IAuditableEntity
{
    private const int NomeMinLength = 2;
    private const int NomeMaxLength = 200;
    private const int DescricaoMaxLength = 1000;

    public string Nome { get; private set; } = string.Empty;
    public string Descricao { get; private set; } = string.Empty;
    public bool? Permanente { get; private set; }

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    // EF Core materialization
    private TipoDeficiencia()
    {
    }

    /// <summary>
    /// Cria um novo TipoDeficiencia. Valida obrigatoriedade/tamanho do nome, a
    /// obrigatoriedade/tamanho da descrição. A unicidade de <paramref name="nome"/>
    /// entre tipos vivos é responsabilidade do handler.
    /// </summary>
    public static Result<TipoDeficiencia> Criar(string nome, string descricao, bool? permanente = null)
    {
        ArgumentNullException.ThrowIfNull(nome);
        ArgumentNullException.ThrowIfNull(descricao);

        Result validacao = ValidarCampos(nome, descricao);
        if (validacao.IsFailure)
        {
            return Result<TipoDeficiencia>.Failure(validacao.Error!);
        }

        var tipo = new TipoDeficiencia();
        tipo.AplicarCampos(nome, descricao, permanente);

        return Result<TipoDeficiencia>.Success(tipo);
    }

    /// <summary>
    /// Atualiza os atributos do TipoDeficiencia. O <c>Nome</c> é editável; sua
    /// unicidade (quando alterado) é responsabilidade do handler. Revalida
    /// obrigatoriedade/tamanho do nome e a obrigatoriedade/tamanho da descrição.
    /// </summary>
    public Result Atualizar(string nome, string descricao, bool? permanente = null)
    {
        ArgumentNullException.ThrowIfNull(nome);
        ArgumentNullException.ThrowIfNull(descricao);

        Result validacao = ValidarCampos(nome, descricao);
        if (validacao.IsFailure)
        {
            return validacao;
        }

        AplicarCampos(nome, descricao, permanente);

        return Result.Success();
    }

    private void AplicarCampos(string nome, string descricao, bool? permanente)
    {
        Nome = nome.Trim();
        Descricao = descricao.Trim();
        Permanente = permanente;
    }

    private static Result ValidarCampos(string nome, string? descricao)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            return Result.Failure(new DomainError(
                TipoDeficienciaErrorCodes.NomeObrigatorio,
                "Nome do tipo de deficiência é obrigatório."));
        }

        if (nome.Trim().Length is < NomeMinLength or > NomeMaxLength)
        {
            return Result.Failure(new DomainError(
                TipoDeficienciaErrorCodes.NomeTamanho,
                $"Nome do tipo de deficiência deve ter entre {NomeMinLength} e {NomeMaxLength} caracteres."));
        }

        if (string.IsNullOrWhiteSpace(descricao))
        {
            return Result.Failure(new DomainError(
                TipoDeficienciaErrorCodes.DescricaoObrigatoria,
                "Descrição do tipo de deficiência é obrigatória."));
        }

        if (descricao.Trim().Length > DescricaoMaxLength)
        {
            return Result.Failure(new DomainError(
                TipoDeficienciaErrorCodes.DescricaoTamanho,
                $"Descrição do tipo de deficiência deve ter no máximo {DescricaoMaxLength} caracteres."));
        }

        return Result.Success();
    }
}
