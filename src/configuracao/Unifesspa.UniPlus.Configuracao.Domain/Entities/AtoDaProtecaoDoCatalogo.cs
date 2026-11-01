namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// Um ato do circuito de aprovação da proteção de dados de um item do catálogo (ADR-0153): a solicitação, a
/// aprovação, o ajuste recomendado, a reprovação e o aceite da recomendação. Só se acrescenta, nunca se altera nem
/// se apaga: é evidência forense (ADR-0063) de que a proteção de dados exerceu a sua decisão, e sobrevive à
/// edição do item, que reinicia o circuito corrente (<see cref="AprovacaoDaProtecao"/>) mas não este registro.
/// </summary>
/// <remarks>
/// O item não é referenciado por chave estrangeira: ele pode ser desativado ou removido, e o registro continua.
/// Código, nome e proteção são o retrato do instante do ato, porque o item pode mudar depois.
/// </remarks>
public sealed class AtoDaProtecaoDoCatalogo : IForensicEntity
{
    /// <summary>Tamanho máximo do código e do nome do item, o do maior dos cadastros.</summary>
    public const int ItemCodigoMaxLength = 50;

    public const int ItemNomeMaxLength = 200;

    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public CadastroDoCatalogo Cadastro { get; private init; }

    public Guid ItemId { get; private init; }

    public string ItemCodigo { get; private init; } = string.Empty;

    public string ItemNome { get; private init; } = string.Empty;

    public AtoDaProtecao Ato { get; private init; }

    public ClassificacaoDeProtecao ClassificacaoProtecao { get; private init; }

    public HipoteseLegalTratamento HipoteseLegal { get; private init; }

    public string FinalidadeTratamento { get; private init; } = string.Empty;

    /// <summary>Só no ajuste recomendado.</summary>
    public ClassificacaoDeProtecao? ClassificacaoRecomendada { get; private init; }

    /// <summary>No ajuste recomendado, opcional; na reprovação, obrigatória.</summary>
    public string? Justificativa { get; private init; }

    public string AtorEmissor { get; private init; } = string.Empty;

    public string AtorSub { get; private init; } = string.Empty;

    public string? AtorNome { get; private init; }

    public DateTimeOffset Instante { get; private init; }

    // Construtor de materialização do EF Core.
    private AtoDaProtecaoDoCatalogo()
    {
    }

    /// <summary>
    /// Registra o ato com a proteção que o item tinha no instante dele — na decisão, a que o aprovador conferiu. A
    /// classificação recomendada acompanha só o ajuste, e a justificativa só o ajuste e a reprovação.
    /// </summary>
    public static AtoDaProtecaoDoCatalogo Registrar(
        CadastroDoCatalogo cadastro,
        Guid itemId,
        string itemCodigo,
        string itemNome,
        AtoDaProtecao ato,
        ProtecaoDeDados protecao,
        ClassificacaoDeProtecao? classificacaoRecomendada,
        string? justificativa,
        AtorDoAto ator,
        DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(itemCodigo);
        ArgumentNullException.ThrowIfNull(itemNome);
        ArgumentNullException.ThrowIfNull(ator);

        return new AtoDaProtecaoDoCatalogo
        {
            Cadastro = cadastro,
            ItemId = itemId,
            ItemCodigo = itemCodigo,
            ItemNome = itemNome,
            Ato = ato,
            ClassificacaoProtecao = protecao.Classificacao,
            HipoteseLegal = protecao.Hipotese,
            FinalidadeTratamento = protecao.Finalidade,
            ClassificacaoRecomendada = ato == AtoDaProtecao.AjusteRecomendado ? classificacaoRecomendada : null,
            Justificativa = ato is AtoDaProtecao.AjusteRecomendado or AtoDaProtecao.Reprovacao
                ? AprovacaoDaProtecao.NormalizarJustificativa(justificativa)
                : null,
            AtorEmissor = ator.Emissor,
            AtorSub = ator.Sub,
            AtorNome = AtorDoAto.NormalizarNome(ator.Nome),
            Instante = instante,
        };
    }
}
