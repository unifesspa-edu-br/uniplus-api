namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Motivo institucional de uma decisão individual, no cadastro único organizado por tipo
/// (UNI-REQ-0283): isenção da taxa, homologação das inscrições, análise documental,
/// heteroidentificação, avaliação biopsicossocial e classificação. Quem decide escolhe o motivo
/// entre os que o edital disponibilizou; não há motivo "outro" nem cadastro na hora da decisão
/// (UNI-REQ-0284, UNI-REQ-0286).
/// </summary>
/// <remarks>
/// <para>
/// <b>Um sentido por motivo.</b> O motivo é favorável ou desfavorável à pessoa, nunca os dois
/// (UNI-REQ-0121, generalizado pelo UNI-REQ-0283): "renda acima do limite" não defere, e a
/// obrigação de escolher no cadastro é o que impede quem decide de citá-lo do lado errado.
/// </para>
/// <para>
/// <b>O tipo decide o que mais o cadastro exige</b>, por uma política por tipo
/// (<see cref="IPoliticaDoTipoDeMotivo"/>): a isenção exige o fundamento como subclassificação,
/// os demais tipos a recusam.
/// </para>
/// <para>
/// <b>Desativar não apaga</b> (UNI-REQ-0122). A retirada é a desativação, que impede a entrada em
/// novas publicações e preserva o que já o escolheu. Por isso a entidade não deriva de
/// <see cref="SoftDeletableEntity"/>: não há remoção a modelar.
/// </para>
/// <para>
/// <b>Só a descrição se edita.</b> Tipo, código, subclassificação e sentido nascem na criação e
/// não mudam: quem precisa de outro cria outro motivo. O processo que o escolheu guarda cópia
/// por valor, então editar a descrição depois não alcança o processo publicado.
/// </para>
/// </remarks>
public sealed class MotivoDeDecisao : EntityBase, IAuditableEntity
{
    public TipoMotivoDeDecisao Tipo { get; private set; }

    public CodigoMotivoDecisao Codigo { get; private set; } = null!;

    public string Descricao { get; private set; } = string.Empty;

    /// <summary>Subclassificação que o tipo define (o fundamento, na isenção); nula nos demais tipos.</summary>
    public string? Subclassificacao { get; private set; }

    /// <summary>Se o motivo é favorável ou desfavorável à pessoa. Nunca <see cref="SentidoDoMotivo.Nenhum"/>.</summary>
    public SentidoDoMotivo Sentido { get; private set; }

    /// <summary>Motivo ativo entra em novas publicações; desativado, não. Nasce ativo.</summary>
    public bool Ativo { get; private set; } = true;

    public string? CreatedBy { get; private set; }

    public string? UpdatedBy { get; private set; }

    // Construtor de materialização do EF Core.
    private MotivoDeDecisao()
    {
    }

    /// <summary>
    /// Cria um motivo, já ativo, acumulando toda violação independente em vez de parar na
    /// primeira (ADR-0125). A unicidade do código dentro do tipo é do handler, com proteção de
    /// corrida no índice único.
    /// </summary>
    public static Result<MotivoDeDecisao> Criar(CamposDoMotivo campos)
    {
        ArgumentNullException.ThrowIfNull(campos);

        Result<CamposValidados> validados = ValidacaoDoMotivo.Validar(campos);
        if (validados.IsFailure)
        {
            return Result<MotivoDeDecisao>.ValidationFailure(validados.Errors);
        }

        CamposValidados valor = validados.Value!;
        return Result<MotivoDeDecisao>.Success(new MotivoDeDecisao
        {
            Tipo = valor.Tipo,
            Codigo = valor.Codigo,
            Descricao = valor.Descricao,
            Subclassificacao = valor.Subclassificacao,
            Sentido = valor.Sentido,
        });
    }

    /// <summary>
    /// Atualiza a descrição, único campo editável. Não altera <see cref="Ativo"/>: reativar e
    /// desativar são operações próprias, com a sua trilha, e escondê-las na edição faria uma
    /// retirada do cadastro passar por correção de texto.
    /// </summary>
    public Result Atualizar(string? descricao)
    {
        Result<string> descricaoValidada = ValidacaoDoMotivo.ValidarDescricao(descricao);
        if (descricaoValidada.IsFailure)
        {
            return Result.ValidationFailure(descricaoValidada.Errors);
        }

        Descricao = descricaoValidada.Value!;
        return Result.Success();
    }

    /// <summary>
    /// Reativa o motivo. Reativar o que já está ativo é recusado: a operação registra ator e
    /// instante, e aceitar o nada gravaria uma reativação que não mudou estado algum.
    /// </summary>
    public Result Reativar()
    {
        if (Ativo)
        {
            return Result.Failure(new DomainError(MotivoDeDecisaoErrorCodes.JaAtivo, "O motivo de decisão já está ativo."));
        }

        Ativo = true;
        return Result.Success();
    }

    /// <summary>
    /// Desativa o motivo. O efeito é prospectivo: ele deixa de entrar em novas publicações e
    /// continua utilizável no processo publicado que já o escolheu (UNI-REQ-0122).
    /// </summary>
    public Result Desativar()
    {
        if (!Ativo)
        {
            return Result.Failure(new DomainError(MotivoDeDecisaoErrorCodes.JaInativo, "O motivo de decisão já está inativo."));
        }

        Ativo = false;
        return Result.Success();
    }
}
