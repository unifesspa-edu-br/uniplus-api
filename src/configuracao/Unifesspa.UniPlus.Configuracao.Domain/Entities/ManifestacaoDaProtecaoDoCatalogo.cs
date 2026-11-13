namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// Um evento de manifestação técnica do Encarregado de dados sobre a proteção de um item do catálogo, num ciclo de
/// aprovação (UNI-REQ-0373, ADR-0163): o registro inicial ou a substituição da manifestação vigente da mesma pessoa no
/// mesmo ciclo. Só se acrescenta, nunca se altera nem se apaga: a substituição é um evento novo que aponta para a
/// anterior, e o reinício do ciclo a encerra sem apagá-la. É evidência forense (ADR-0063).
/// </summary>
/// <remarks>
/// O item não é referenciado por chave estrangeira, como no ato: ele pode ser desativado ou removido, e o registro
/// continua. O registro passa por <see cref="ManifestacoesDoCiclo.Registrar"/>, que confere o ciclo e a situação do
/// item e atualiza o circuito na mesma transação.
/// </remarks>
public sealed class ManifestacaoDaProtecaoDoCatalogo : IForensicEntity
{
    // O item e o autor são gravados coluna a coluna; o domínio os expõe como value objects.
    private readonly CadastroDoCatalogo _cadastro;
    private readonly Guid _itemId;
    private readonly string _autorEmissor = string.Empty;
    private readonly string _autorSub = string.Empty;
    private readonly string? _autorNome;

    // Construtor de materialização do EF Core.
    private ManifestacaoDaProtecaoDoCatalogo()
    {
    }

    // Sobre qual item e de quem é a manifestação.
    private ManifestacaoDaProtecaoDoCatalogo(ItemDaProtecao item, AtorDoAto autor)
    {
        _cadastro = item.Cadastro;
        _itemId = item.ItemId;
        _autorEmissor = autor.Emissor;
        _autorSub = autor.Sub;
        _autorNome = AtorDoAto.NormalizarNome(autor.Nome);
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public ItemDaProtecao Item => new(_cadastro, _itemId);

    public CicloDaAprovacao Ciclo { get; private init; }

    public ParecerDaManifestacao Parecer { get; private init; }

    /// <summary>Obrigatório nos pareceres recomenda ajuste e inadequada; opcional no adequada.</summary>
    public Fundamentacao? Texto { get; private init; }

    /// <summary>A manifestação vigente da mesma pessoa, no mesmo ciclo, que esta substitui; nulo no registro inicial.</summary>
    public Guid? SubstituiId { get; private init; }

    /// <summary>Quem se manifestou: o emissor e o <c>sub</c> do token e o nome no instante da manifestação.</summary>
    public AtorDoAto Autor => new(_autorEmissor, _autorSub, _autorNome);

    public DateTimeOffset Instante { get; private init; }

    /// <summary>Recomenda ajuste ou inadequada.</summary>
    public bool Desfavoravel => PareceresDaManifestacao.EhDesfavoravel(Parecer);

    /// <summary>Se a manifestação é da pessoa que <paramref name="outro"/> identifica, pelo emissor e pelo <c>sub</c>.</summary>
    public bool DaMesmaPessoa(AtorDoAto outro)
    {
        ArgumentNullException.ThrowIfNull(outro);
        return string.Equals(_autorEmissor, outro.Emissor, StringComparison.Ordinal)
            && string.Equals(_autorSub, outro.Sub, StringComparison.Ordinal);
    }

    /// <summary>
    /// O evento da manifestação de <paramref name="autor"/> no ciclo, substituindo <paramref name="substituida"/> quando
    /// ela existe. Quem registra é <see cref="ManifestacoesDoCiclo.Registrar"/>, que escolhe a substituída entre as
    /// vigentes e atualiza o circuito do item.
    /// </summary>
    /// <exception cref="ArgumentException">Se a substituída é de outro item, de outro ciclo ou de outra pessoa.</exception>
    public static ManifestacaoDaProtecaoDoCatalogo Registrar(
        ItemDaProtecao item,
        CicloDaAprovacao ciclo,
        ConteudoDaManifestacao conteudo,
        AtorDoAto autor,
        ManifestacaoDaProtecaoDoCatalogo? substituida,
        DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(conteudo);
        ArgumentNullException.ThrowIfNull(autor);
        ExigirSubstituivel(substituida, item, ciclo, autor);

        return new ManifestacaoDaProtecaoDoCatalogo(item, autor)
        {
            Ciclo = ciclo,
            Parecer = conteudo.Parecer,
            Texto = conteudo.Texto,
            SubstituiId = substituida?.Id,
            Instante = instante,
        };
    }

    private static void ExigirSubstituivel(
        ManifestacaoDaProtecaoDoCatalogo? substituida, ItemDaProtecao item, CicloDaAprovacao ciclo, AtorDoAto autor)
    {
        if (substituida is not null && (substituida.Item != item || substituida.Ciclo != ciclo || !substituida.DaMesmaPessoa(autor)))
        {
            throw new ArgumentException("A manifestação substituída é de outro item, de outro ciclo ou de outra pessoa.", nameof(substituida));
        }
    }
}
