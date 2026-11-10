namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// Uma implantação do catálogo de permissões do código no banco: a impressão do conteúdo, o instante e as permissões como
/// estavam. A implantação atual é a de maior sequência. Só se acrescenta; a impressão não é única, porque o mesmo conteúdo
/// volta numa reversão da implantação.
/// </summary>
public sealed class ImplantacaoDasPermissoes
{
    private readonly List<PermissaoImplantada> _permissoes = [];

    public Guid Id { get; private init; } = Guid.CreateVersion7();

    /// <summary>A ordem das implantações, dada pelo banco.</summary>
    public long Sequencia { get; private set; }

    /// <summary>SHA-256 em hexadecimal do conteúdo, com 64 caracteres.</summary>
    public string Impressao { get; private init; } = string.Empty;

    public DateTimeOffset ImplantadaEm { get; private init; }

    public IReadOnlyList<PermissaoImplantada> Permissoes => _permissoes;

    // Construtor de materialização do EF Core.
    private ImplantacaoDasPermissoes()
    {
    }

    public static ImplantacaoDasPermissoes Implantar(string impressao, IEnumerable<LinhaDoRetratoDoCatalogo> linhas, DateTimeOffset instante)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(impressao);
        ArgumentNullException.ThrowIfNull(linhas);

        ImplantacaoDasPermissoes catalogo = new() { Impressao = impressao, ImplantadaEm = instante };
        catalogo._permissoes.AddRange(linhas.Select(l =>
            new PermissaoImplantada(catalogo.Id, l.Codigo, l.Modulo, l.Classificacao, l.DeUnidade, l.Reservada)));

        return catalogo;
    }
}
