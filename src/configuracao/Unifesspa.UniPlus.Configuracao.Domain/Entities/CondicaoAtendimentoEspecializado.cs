namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Condição que habilita atendimento especializado — cadastro institucional
/// (UNI-REQ-0012, módulo Configuração): diz <i>qual é a condição</i> que ampara
/// um pedido de atendimento especializado (ex.: PCD, dislexia, lactante), nunca o
/// recurso concedido nem a solicitação de um candidato (essas vivem no Módulo
/// Seleção). Dado institucional sem PII (LGPD inaplicável).
/// </summary>
/// <remarks>
/// <para>O <see cref="Codigo"/> (value object <see cref="CodigoCondicao"/>) é a
/// chave natural, único entre condições vivas (índice único parcial
/// <c>WHERE is_deleted = false</c>) — e <b>editável</b>, pois o consumo
/// cross-módulo é por snapshot-copy desacoplado (ADR-0061): editar o código vivo
/// não altera o rótulo já congelado numa solicitação de Seleção. A unicidade é
/// checada pelo handler (com proteção de corrida via índice).</para>
/// <para>O código reservado <see cref="CodigoCondicao.Pcd"/> não pode ser
/// renomeado (este agregado bloqueia em <see cref="Atualizar"/>) nem removido (o
/// handler de remoção bloqueia) — a ADR-0067 o referencia literalmente.</para>
/// <para>A remoção é sempre soft-delete e nunca bloqueada por consumo de Seleção
/// (snapshot-copy desacoplado).</para>
/// </remarks>
public sealed class CondicaoAtendimentoEspecializado : SoftDeletableEntity, IAuditableEntity
{
    private const int NomeMinLength = 2;
    private const int NomeMaxLength = 200;
    private const int DescricaoMaxLength = 1000;

    public CodigoCondicao Codigo { get; private set; } = null!;
    public string Nome { get; private set; } = string.Empty;
    public string? Descricao { get; private set; }

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    // EF Core materialization
    private CondicaoAtendimentoEspecializado()
    {
    }

    /// <summary>
    /// Cria uma nova condição de atendimento especializado. Valida o código (via
    /// <see cref="CodigoCondicao.Criar"/>), o nome (obrigatório, tamanho) e a
    /// descrição (tamanho). A unicidade do <paramref name="codigo"/> entre
    /// condições vivas é responsabilidade do handler.
    /// </summary>
    public static Result<CondicaoAtendimentoEspecializado> Criar(
        string codigo,
        string nome,
        string? descricao)
    {
        ArgumentNullException.ThrowIfNull(codigo);
        ArgumentNullException.ThrowIfNull(nome);

        Result<CodigoCondicao> validacao = ValidarCampos(codigo, nome, descricao);
        if (validacao.IsFailure)
        {
            return Result<CondicaoAtendimentoEspecializado>.Failure(validacao.Error!);
        }

        var condicao = new CondicaoAtendimentoEspecializado();
        condicao.AplicarCampos(validacao.Value!, nome, descricao);

        return Result<CondicaoAtendimentoEspecializado>.Success(condicao);
    }

    /// <summary>
    /// Atualiza os atributos da condição. O <c>Codigo</c> é editável e sua
    /// unicidade (quando alterado) é responsabilidade do handler — <b>exceto</b>
    /// quando o código atual é <see cref="CodigoCondicao.Pcd"/>: a condição
    /// reservada não pode ser renomeada (retorna
    /// <c>CodigoProtegidoNaoEditavel</c>). Revalida formato/tamanho.
    /// </summary>
    public Result Atualizar(string codigo, string nome, string? descricao)
    {
        ArgumentNullException.ThrowIfNull(codigo);
        ArgumentNullException.ThrowIfNull(nome);

        Result<CodigoCondicao> validacao = ValidarCampos(codigo, nome, descricao);
        if (validacao.IsFailure)
        {
            return Result.Failure(validacao.Error!);
        }

        CodigoCondicao novoCodigo = validacao.Value!;

        // O código reservado PCD não pode ser renomeado (ADR-0067). Editar o nome
        // ou a descrição mantendo o código PCD continua permitido.
        if (Codigo.EhProtegido && !novoCodigo.EhProtegido)
        {
            return Result.Failure(new DomainError(
                CondicaoAtendimentoErrorCodes.CodigoProtegidoNaoEditavel,
                $"A condição reservada '{CodigoCondicao.Pcd}' não pode ter o código alterado."));
        }

        AplicarCampos(novoCodigo, nome, descricao);

        return Result.Success();
    }

    private void AplicarCampos(CodigoCondicao codigo, string nome, string? descricao)
    {
        Codigo = codigo;
        Nome = nome.Trim();
        Descricao = NormalizarOpcional(descricao);
    }

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static Result<CodigoCondicao> ValidarCampos(string codigo, string nome, string? descricao)
    {
        Result<CodigoCondicao> codigoResult = CodigoCondicao.Criar(codigo);
        if (codigoResult.IsFailure)
        {
            return Result<CodigoCondicao>.Failure(codigoResult.Error!);
        }

        if (string.IsNullOrWhiteSpace(nome))
        {
            return Result<CodigoCondicao>.Failure(new DomainError(
                CondicaoAtendimentoErrorCodes.NomeObrigatorio,
                "Nome da condição de atendimento especializado é obrigatório."));
        }

        if (nome.Trim().Length is < NomeMinLength or > NomeMaxLength)
        {
            return Result<CodigoCondicao>.Failure(new DomainError(
                CondicaoAtendimentoErrorCodes.NomeTamanho,
                $"Nome da condição deve ter entre {NomeMinLength} e {NomeMaxLength} caracteres."));
        }

        if (descricao is not null && descricao.Trim().Length > DescricaoMaxLength)
        {
            return Result<CodigoCondicao>.Failure(new DomainError(
                CondicaoAtendimentoErrorCodes.DescricaoTamanho,
                $"Descrição da condição deve ter no máximo {DescricaoMaxLength} caracteres."));
        }

        return Result<CodigoCondicao>.Success(codigoResult.Value!);
    }
}
