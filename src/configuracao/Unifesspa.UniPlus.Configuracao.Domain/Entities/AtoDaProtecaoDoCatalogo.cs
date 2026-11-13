namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// Um ato do circuito de aprovação da proteção de dados de um tipo de documento ou fato do candidato (ADR-0153): a
/// solicitação, a aprovação, o ajuste recomendado, a reprovação e o aceite da recomendação. Só se acrescenta, nunca se
/// altera nem se apaga: é evidência forense (ADR-0063) de que o Representante do controlador exerceu a sua decisão, e
/// sobrevive à edição do item, que reinicia o circuito corrente (<see cref="AprovacaoDaProtecao"/>) mas não este
/// registro.
/// </summary>
/// <remarks>
/// O item não é referenciado por chave estrangeira: ele pode ser desativado ou removido, e o registro continua.
/// Código, nome e proteção são o retrato do instante do ato, porque o item pode mudar depois. A decisão guarda também o
/// ciclo e se havia manifestação técnica desfavorável vigente (ADR-0163).
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

    /// <summary>O ciclo de aprovação em que o ato aconteceu.</summary>
    public CicloDaAprovacao Ciclo { get; private init; }

    public string FinalidadeTratamento { get; private init; } = string.Empty;

    /// <summary>Só no ajuste recomendado.</summary>
    public ClassificacaoDeProtecao? ClassificacaoRecomendada { get; private set; }

    /// <summary>
    /// O motivo da decisão: obrigatório na reprovação; opcional na aprovação e no ajuste, salvo diante de manifestação
    /// desfavorável vigente.
    /// </summary>
    public string? Justificativa { get; private set; }

    /// <summary>Se a decisão foi tomada com manifestação técnica desfavorável vigente no ciclo; falso fora das decisões.</summary>
    public bool DianteDeManifestacaoDesfavoravel { get; private set; }

    public string AtorEmissor { get; private init; } = string.Empty;

    public string AtorSub { get; private init; } = string.Empty;

    public string? AtorNome { get; private init; }

    public DateTimeOffset Instante { get; private init; }

    /// <summary>
    /// A ordem em que os atos foram registrados, dada pelo banco: desempata dois atos do mesmo instante sobre o mesmo
    /// item, que a mudança na linha do item serializa pelo token de concorrência.
    /// </summary>
    public long OrdemDeRegistro { get; private set; }

    // Construtor de materialização do EF Core.
    private AtoDaProtecaoDoCatalogo()
    {
    }

    // O retrato do item no instante do ato: o que ele era e em que ciclo estava.
    private AtoDaProtecaoDoCatalogo(RetratoDoItemDaProtecao item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Cadastro = item.Item.Cadastro;
        ItemId = item.Item.ItemId;
        ItemCodigo = item.Codigo;
        ItemNome = item.Nome;
        ClassificacaoProtecao = item.Protecao.Classificacao;
        HipoteseLegal = item.Protecao.Hipotese;
        FinalidadeTratamento = item.Protecao.Finalidade;
        Ciclo = item.Ciclo;
    }

    /// <summary>
    /// Registra a solicitação ou o aceite, com o retrato do item no instante do ato: sem classificação recomendada nem
    /// justificativa, que são da decisão.
    /// </summary>
    /// <exception cref="ArgumentException">Se o ato é uma decisão: a decisão se registra por <see cref="RegistrarDecisao"/>.</exception>
    public static AtoDaProtecaoDoCatalogo Registrar(RetratoDoItemDaProtecao item, AtoDaProtecao ato, AtorDoAto ator, DateTimeOffset instante)
    {
        if (AtosDaProtecao.Decisoes.Contains(ato) || ato == AtoDaProtecao.Nenhum)
        {
            throw new ArgumentException("A decisão se registra com os seus fundamentos.", nameof(ato));
        }

        return Retratado(item, ato, ator, instante);
    }

    /// <summary>
    /// Registra a decisão com a proteção que o Representante do controlador conferiu, a classificação recomendada (só no ajuste), a
    /// justificativa, o ciclo e se havia manifestação desfavorável vigente.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Se a aprovação ou o ajuste diante de manifestação desfavorável vigente não traz justificativa: a regra é conferida
    /// antes, por <see cref="DecisaoFundamentada.Conferir"/>, e o banco a recusa também.
    /// </exception>
    public static AtoDaProtecaoDoCatalogo RegistrarDecisao(
        RetratoDoItemDaProtecao item, DecisaoFundamentada decisao, AtorDoAto ator, DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(decisao);
        if (item.ManifestacaoDesfavoravelVigente && decisao.FaltaJustificativaDianteDeManifestacaoDesfavoravel)
        {
            throw new InvalidOperationException("A decisão diante de manifestação desfavorável vigente exige a justificativa.");
        }

        // Os fundamentos só a decisão os tem; ficam fixados aqui, antes de o ato sair da fábrica.
        AtoDaProtecaoDoCatalogo ato = Retratado(item, decisao.Ato, ator, instante);
        ato.ClassificacaoRecomendada = decisao.Ato == AtoDaProtecao.AjusteRecomendado ? decisao.ClassificacaoRecomendada : null;
        ato.Justificativa = decisao.Justificativa?.Valor;
        ato.DianteDeManifestacaoDesfavoravel = item.ManifestacaoDesfavoravelVigente;
        return ato;
    }

    private static AtoDaProtecaoDoCatalogo Retratado(RetratoDoItemDaProtecao item, AtoDaProtecao ato, AtorDoAto ator, DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(ator);

        return new AtoDaProtecaoDoCatalogo(item)
        {
            Ato = ato,
            AtorEmissor = ator.Emissor,
            AtorSub = ator.Sub,
            AtorNome = AtorDoAto.NormalizarNome(ator.Nome),
            Instante = instante,
        };
    }
}
