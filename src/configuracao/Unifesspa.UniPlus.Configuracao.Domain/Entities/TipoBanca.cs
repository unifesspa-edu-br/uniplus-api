namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Tipo de banca (UNI-REQ-0139): cadastro classificatório das bancas que atuam na
/// seleção (análise documental, entrevista, correção de redações, análise de
/// recursos, heteroidentificação étnico-racial e avaliação biopsicossocial). Dado
/// institucional de referência sem PII (LGPD inaplicável). A <b>composição</b> de
/// uma banca (membros, atas, deliberações) é matéria de um incremento futuro e
/// <b>não</b> é modelada aqui — este cadastro entrega apenas o <b>tipo</b> da banca.
/// </summary>
/// <remarks>
/// <para>O <see cref="Codigo"/> (value object <see cref="CodigoBanca"/>) é a chave
/// natural, único entre bancas vivas (índice único parcial <c>WHERE is_deleted =
/// false</c>) e <b>imutável</b>. Além do formato, deve pertencer ao conjunto
/// canônico das seis bancas (<see cref="TipoBancaCatalogo"/>).</para>
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
    /// Valida o código (formato + pertença ao conjunto canônico das seis
    /// bancas), sem mutar nada — existe para o handler de criação decidir se vale
    /// a pena consultar a unicidade antes mesmo de chamar <see cref="Criar"/>, que
    /// revalida por conta própria e nunca confia num resultado calculado por fora.
    /// </summary>
    public static Result<CodigoBanca> ValidarCodigo(string? codigo)
    {
        Result<CodigoBanca> codigoResult = CodigoBanca.Criar(codigo);
        if (codigoResult.IsFailure)
        {
            return Result<CodigoBanca>.ValidationFailure([new("codigo", codigoResult.Error!)]);
        }

        CodigoBanca codigoVo = codigoResult.Value!;
        if (!TipoBancaCatalogo.EhCanonico(codigoVo.Valor))
        {
            // Mensagem genérica de propósito (ADR-0023): nunca ecoar o dado rejeitado.
            return Result<CodigoBanca>.ValidationFailure([new("codigo", new DomainError(
                TipoBancaErrorCodes.CodigoForaDoConjuntoCanonico,
                "Código do tipo de banca não pertence ao conjunto canônico das seis bancas."))]);
        }

        return Result<CodigoBanca>.Success(codigoVo);
    }

    /// <summary>
    /// Valida os três campos comuns a Criar e Atualizar (nome, fase típica,
    /// descrição), acumulando toda violação independente em vez de parar na
    /// primeira — sem mutar nada. Os limites de tamanho valem sobre o valor já
    /// normalizado (<c>Trim</c>), a mesma medida que <see cref="AplicarCampos"/>
    /// persiste.
    /// </summary>
    public static Result<(string Nome, string? FaseTipica, string? Descricao)> ValidarCamposComuns(
        string? nome, string? faseTipica, string? descricao)
    {
        List<FieldError> erros = [];

        string? nomeNorm = null;
        if (string.IsNullOrWhiteSpace(nome))
        {
            erros.Add(new("nome", new DomainError(
                TipoBancaErrorCodes.NomeObrigatorio, "Nome do tipo de banca é obrigatório.")));
        }
        else
        {
            nomeNorm = nome.Trim();
            if (nomeNorm.Length > NomeMaxLength)
            {
                erros.Add(new("nome", new DomainError(
                    TipoBancaErrorCodes.NomeTamanho,
                    $"Nome do tipo de banca deve ter no máximo {NomeMaxLength} caracteres.")));
                nomeNorm = null;
            }
        }

        string? faseTipicaNorm = NormalizarOpcional(faseTipica);
        if (faseTipicaNorm is not null && faseTipicaNorm.Length > FaseTipicaMaxLength)
        {
            erros.Add(new("faseTipica", new DomainError(
                TipoBancaErrorCodes.FaseTipicaTamanho,
                $"Fase típica do tipo de banca deve ter no máximo {FaseTipicaMaxLength} caracteres.")));
        }

        string? descricaoNorm = NormalizarOpcional(descricao);
        if (descricaoNorm is not null && descricaoNorm.Length > DescricaoMaxLength)
        {
            erros.Add(new("descricao", new DomainError(
                TipoBancaErrorCodes.DescricaoTamanho,
                $"Descrição do tipo de banca deve ter no máximo {DescricaoMaxLength} caracteres.")));
        }

        if (erros.Count > 0)
        {
            return Result<(string, string?, string?)>.ValidationFailure(erros);
        }

        return Result<(string, string?, string?)>.Success((nomeNorm!, faseTipicaNorm, descricaoNorm));
    }

    /// <summary>
    /// Cria um novo tipo de banca. Revalida <paramref name="codigo"/> via
    /// <see cref="ValidarCodigo"/> e os demais campos via
    /// <see cref="ValidarCamposComuns"/>, acumulando toda violação no mesmo lote.
    /// A unicidade do código entre vivos é responsabilidade do handler.
    /// </summary>
    public static Result<TipoBanca> Criar(
        string? codigo,
        string? nome,
        string? faseTipica,
        string? descricao)
    {
        List<FieldError> erros = [];

        Result<CodigoBanca> codigoResult = ValidarCodigo(codigo);
        if (codigoResult.IsFailure)
        {
            erros.AddRange(codigoResult.Errors);
        }

        Result<(string Nome, string? FaseTipica, string? Descricao)> camposResult =
            ValidarCamposComuns(nome, faseTipica, descricao);
        if (camposResult.IsFailure)
        {
            erros.AddRange(camposResult.Errors);
        }

        if (erros.Count > 0)
        {
            return Result<TipoBanca>.ValidationFailure(erros);
        }

        var banca = new TipoBanca { Codigo = codigoResult.Value! };
        banca.AplicarCampos(camposResult.Value);

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
        Result<(string Nome, string? FaseTipica, string? Descricao)> camposResult =
            ValidarCamposComuns(nome, faseTipica, descricao);
        if (camposResult.IsFailure)
        {
            return Result.ValidationFailure(camposResult.Errors);
        }

        AplicarCampos(camposResult.Value);

        return Result.Success();
    }

    private void AplicarCampos((string Nome, string? FaseTipica, string? Descricao) campos)
    {
        Nome = campos.Nome;
        FaseTipica = campos.FaseTipica;
        Descricao = campos.Descricao;
    }

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
