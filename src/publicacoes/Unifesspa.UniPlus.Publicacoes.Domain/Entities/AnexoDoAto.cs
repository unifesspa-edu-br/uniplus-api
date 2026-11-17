namespace Unifesspa.UniPlus.Publicacoes.Domain.Entities;

using System.Globalization;
using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

/// <summary>
/// Arquivo anexo de um ato publicado: o nome com que foi publicado, o tipo de mídia e o
/// hash SHA-256 do conteúdo. Sem o hash de cada anexo, o hash do documento principal
/// não prova que o ato publicado é o mesmo ato que se registrou.
/// </summary>
/// <remarks>
/// <para>
/// Append-only (<see cref="IForensicEntity"/>, ADR-0063), como o ato que o carrega: nasce
/// no registro do ato, na mesma transação, e não se muta nem se remove. Um anexo trocado
/// depois da publicação é outro ato, não uma correção do primeiro.
/// </para>
/// <para>
/// <see cref="Ordem"/> guarda a posição em que o anexo foi enviado: a leitura devolve os
/// anexos nessa ordem, e o banco não garante ordem a quem não a pede.
/// </para>
/// <para>
/// Publicações recebe nome, tipo de mídia e hash prontos e não conhece o arquivo nem o
/// domínio que o produziu (ADR-0105).
/// </para>
/// </remarks>
public sealed partial class AnexoDoAto : IForensicEntity
{
    public const int NomeMaxLength = 255;
    public const int TipoMidiaMaxLength = 255;

    /// <summary>Máximo de anexos por ato.</summary>
    public const int QuantidadeMaxima = 50;

    public Guid Id { get; private init; } = Guid.CreateVersion7();

    /// <summary>Ato a que o anexo pertence. Única chave estrangeira da entidade.</summary>
    public Guid AtoId { get; private init; }

    /// <summary>Posição do anexo na lista enviada, a partir de zero.</summary>
    public int Ordem { get; private init; }

    /// <summary>Nome do anexo, único dentro do ato.</summary>
    public string Nome { get; private init; } = null!;

    /// <summary>Tipo de mídia em minúsculas (ex.: <c>application/pdf</c>).</summary>
    public string TipoMidia { get; private init; } = null!;

    /// <summary>Hash SHA-256 do conteúdo, em hexadecimal minúsculo.</summary>
    public string Hash { get; private init; } = null!;

    // EF Core materialization
    private AnexoDoAto()
    {
    }

    /// <summary>
    /// Confere a lista de anexos de um ato e devolve a primeira recusa, ou nulo quando todos
    /// são aceitáveis. É a regra única do que um anexo precisa ter: o handler a aplica para
    /// devolver o erro nomeado, e <see cref="Criar"/> a reaplica como última linha.
    /// </summary>
    public static DomainError? Conferir(IReadOnlyCollection<(string Nome, string TipoMidia, string Hash)> anexos)
    {
        ArgumentNullException.ThrowIfNull(anexos);

        return anexos.Count > QuantidadeMaxima
            ? AnexoDoAtoErros.QuantidadeExcedida(anexos.Count)
            : PrimeiraRecusaEntreOsAnexos(anexos);
    }

    /// <summary>
    /// Cria o anexo de <paramref name="ato"/> na posição <paramref name="ordem"/>. Última
    /// linha das invariantes: a recusa nomeada é devolvida por <see cref="Conferir"/>.
    /// </summary>
    public static AnexoDoAto Criar(AtoNormativo ato, int ordem, string nome, string tipoMidia, string hash)
    {
        ArgumentNullException.ThrowIfNull(ato);
        ArgumentOutOfRangeException.ThrowIfNegative(ordem);
        ExigirAceitavel(nome, tipoMidia, hash);

        return new AnexoDoAto
        {
            AtoId = ato.Id,
            Ordem = ordem,
            Nome = nome.Trim(),
            TipoMidia = NormalizarTipoMidia(tipoMidia),
            Hash = hash,
        };
    }

    private static void ExigirAceitavel(string nome, string tipoMidia, string hash)
    {
        ArgumentNullException.ThrowIfNull(nome);
        ArgumentNullException.ThrowIfNull(tipoMidia);

        if (Conferir([(nome, tipoMidia, hash)]) is { } recusa)
        {
            throw new ArgumentException(recusa.Message, nameof(nome));
        }
    }

    /// <summary>
    /// A primeira recusa entre os anexos, na ordem de envio: a do próprio anexo (nome, tipo de
    /// mídia, hash) e, depois, a do nome já usado por um anexo anterior.
    /// </summary>
    private static DomainError? PrimeiraRecusaEntreOsAnexos(IEnumerable<(string Nome, string TipoMidia, string Hash)> anexos)
    {
        HashSet<string> nomes = new(NomeDoAnexoPublicado.Comparador);

        foreach ((string Nome, string TipoMidia, string Hash) anexo in anexos)
        {
            if ((RecusaDoAnexo(anexo) ?? RecusaDoNomeRepetido(anexo.Nome, nomes)) is { } recusa)
            {
                return recusa;
            }
        }

        return null;
    }

    private static DomainError? RecusaDoAnexo((string Nome, string TipoMidia, string Hash) anexo)
    {
        string nome = NomeDoAnexoPublicado.Normalizar(anexo.Nome);
        if (nome.Length == 0 || nome.Length > NomeMaxLength)
        {
            return AnexoDoAtoErros.NomeInvalido();
        }

        if (!TipoMidiaValido(NormalizarTipoMidia(anexo.TipoMidia)))
        {
            return AnexoDoAtoErros.TipoMidiaInvalido(nome);
        }

        return HashSha256.TemFormatoValido(anexo.Hash) ? null : AnexoDoAtoErros.HashInvalido(nome);
    }

    private static DomainError? RecusaDoNomeRepetido(string nome, HashSet<string> nomesJaVistos)
    {
        string nomeNormalizado = NomeDoAnexoPublicado.Normalizar(nome);
        return nomesJaVistos.Add(nomeNormalizado) ? null : AnexoDoAtoErros.NomeDuplicado(nomeNormalizado);
    }

    private static string NormalizarTipoMidia(string? tipoMidia) =>
        tipoMidia?.Trim().ToLower(CultureInfo.InvariantCulture) ?? string.Empty;

    private static bool TipoMidiaValido(string tipoMidia) =>
        tipoMidia.Length <= TipoMidiaMaxLength && FormatoDoTipoMidia().IsMatch(tipoMidia);

    /// <summary>Forma <c>tipo/subtipo</c> do tipo de mídia, já em minúsculas.</summary>
    [GeneratedRegex(@"^[a-z0-9][a-z0-9.+_-]*/[a-z0-9][a-z0-9.+_-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex FormatoDoTipoMidia();
}
