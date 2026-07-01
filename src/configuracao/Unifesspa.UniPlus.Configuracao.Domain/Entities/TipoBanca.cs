namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Tipo de banca (UNI-REQ-0064): cadastro classificatório das bancas que atuam na
/// seleção (análise documental, entrevista, correção de redações, análise de
/// recursos). Dado institucional de referência sem PII (LGPD inaplicável). A
/// <b>composição</b> de uma banca (membros, atas, deliberações) é matéria de um
/// incremento futuro e <b>não</b> é modelada aqui — este cadastro entrega apenas o
/// <b>tipo</b> da banca.
/// </summary>
/// <remarks>
/// <para>O <see cref="Codigo"/> (value object <see cref="CodigoBanca"/>) é a chave
/// natural, único entre bancas vivas (índice único parcial <c>WHERE is_deleted =
/// false</c>) e <b>imutável</b>. Além do formato, deve pertencer ao conjunto
/// canônico das quatro bancas (<see cref="TipoBancaCatalogo"/>).</para>
/// <para>A <see cref="FaseTipica"/> é a fase em que a banca usualmente atua — um
/// rótulo de texto <b>orientativo e não vinculante</b>, <b>não</b> uma referência
/// para o cadastro de fases. Pode ser nula e pode conter um valor que não
/// corresponda a nenhuma fase cadastrada.</para>
/// <para>A remoção é sempre soft-delete; nunca bloqueada — o único consumo é por
/// snapshot-copy desacoplado no Módulo Seleção (ADR-0061).</para>
/// </remarks>
public sealed class TipoBanca : SoftDeletableEntity, IAuditableEntity
{
    private const int NomeMaxLength = 200;
    private const int FaseTipicaMaxLength = 60;
    private const int DescricaoMaxLength = 300;

    public CodigoBanca Codigo { get; private set; } = null!;
    public string Nome { get; private set; } = null!;
    public string? FaseTipica { get; private set; }
    public string? Descricao { get; private set; }

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    // EF Core materialization
    private TipoBanca()
    {
    }

    /// <summary>
    /// Cria um novo tipo de banca. Valida o código (formato + pertença ao conjunto
    /// canônico das quatro bancas) e o nome. A <paramref name="faseTipica"/> é
    /// orientativa (não validada contra o cadastro de fases). A unicidade do código
    /// entre vivos é responsabilidade do handler.
    /// </summary>
    public static Result<TipoBanca> Criar(
        string codigo,
        string? nome,
        string? faseTipica,
        string? descricao)
    {
        ArgumentNullException.ThrowIfNull(codigo);

        Result<CodigoBanca> codigoResult = CodigoBanca.Criar(codigo);
        if (codigoResult.IsFailure)
        {
            return Result<TipoBanca>.Failure(codigoResult.Error!);
        }

        CodigoBanca codigoVo = codigoResult.Value!;

        if (!TipoBancaCatalogo.EhCanonico(codigoVo.Valor))
        {
            return Result<TipoBanca>.Failure(new DomainError(
                TipoBancaErrorCodes.CodigoForaDoConjuntoCanonico,
                $"Código '{codigoVo.Valor}' não pertence ao conjunto canônico das quatro bancas."));
        }

        Result<CamposResolvidos> camposResult = ValidarComuns(nome, faseTipica, descricao);
        if (camposResult.IsFailure)
        {
            return Result<TipoBanca>.Failure(camposResult.Error!);
        }

        var banca = new TipoBanca { Codigo = codigoVo };
        banca.AplicarCampos(camposResult.Value!);

        return Result<TipoBanca>.Success(banca);
    }

    /// <summary>
    /// Atualiza os atributos editáveis do tipo de banca. O <c>Codigo</c> e o
    /// <c>Id</c> são <b>imutáveis</b> — este método não os recebe.
    /// </summary>
    public Result Atualizar(
        string? nome,
        string? faseTipica,
        string? descricao)
    {
        Result<CamposResolvidos> camposResult = ValidarComuns(nome, faseTipica, descricao);
        if (camposResult.IsFailure)
        {
            return Result.Failure(camposResult.Error!);
        }

        AplicarCampos(camposResult.Value!);

        return Result.Success();
    }

    private void AplicarCampos(CamposResolvidos campos)
    {
        Nome = campos.Nome;
        FaseTipica = campos.FaseTipica;
        Descricao = campos.Descricao;
    }

    private static Result<CamposResolvidos> ValidarComuns(
        string? nome,
        string? faseTipica,
        string? descricao)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            return Falha(TipoBancaErrorCodes.NomeObrigatorio, "Nome do tipo de banca é obrigatório.");
        }

        string nomeNorm = nome.Trim();
        if (nomeNorm.Length > NomeMaxLength)
        {
            return Falha(TipoBancaErrorCodes.NomeTamanho,
                $"Nome do tipo de banca deve ter no máximo {NomeMaxLength} caracteres.");
        }

        if (faseTipica is not null && faseTipica.Trim().Length > FaseTipicaMaxLength)
        {
            return Falha(TipoBancaErrorCodes.FaseTipicaTamanho,
                $"Fase típica do tipo de banca deve ter no máximo {FaseTipicaMaxLength} caracteres.");
        }

        if (descricao is not null && descricao.Trim().Length > DescricaoMaxLength)
        {
            return Falha(TipoBancaErrorCodes.DescricaoTamanho,
                $"Descrição do tipo de banca deve ter no máximo {DescricaoMaxLength} caracteres.");
        }

        return Result<CamposResolvidos>.Success(new CamposResolvidos(
            nomeNorm,
            NormalizarOpcional(faseTipica),
            NormalizarOpcional(descricao)));
    }

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static Result<CamposResolvidos> Falha(string code, string mensagem) =>
        Result<CamposResolvidos>.Failure(new DomainError(code, mensagem));

    private sealed record CamposResolvidos(
        string Nome,
        string? FaseTipica,
        string? Descricao);
}
