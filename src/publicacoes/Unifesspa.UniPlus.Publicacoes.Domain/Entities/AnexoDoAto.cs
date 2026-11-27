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
    /// Todas as recusas da lista de anexos de um ato, na ordem: a quantidade excedida (campo
    /// <c>anexos</c>) e, para cada anexo na posição de envio, o elemento nulo ou então o nome, o tipo de
    /// mídia, o hash e o nome repetido. A quantidade excedida não dispensa a conferência de cada anexo.
    /// É a regra única do que um anexo precisa ter: <see cref="Criar"/> a reaplica como última linha.
    /// </summary>
    public static IReadOnlyList<FieldError> Recusas(IReadOnlyList<(string? Nome, string? TipoMidia, string? Hash)?> anexos)
    {
        ArgumentNullException.ThrowIfNull(anexos);

        FieldError[] daLista = anexos.Count > QuantidadeMaxima
            ? [new("anexos", AnexoDoAtoErros.QuantidadeExcedida())]
            : [];

        return [.. daLista, .. RecusasDosItens(anexos)];
    }

    internal static IReadOnlyList<(string? Nome, string? TipoMidia, string? Hash)?> Declarados(
        IEnumerable<(string Nome, string TipoMidia, string Hash)> anexos) =>
        [.. anexos.Select(anexo => ((string? Nome, string? TipoMidia, string? Hash)?)anexo)];

    /// <summary>
    /// Cria o anexo de <paramref name="ato"/> na posição <paramref name="ordem"/>. Última
    /// linha das invariantes: a recusa é a primeira de <see cref="Recusas"/>.
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
            Nome = NomeDoAnexoPublicado.Normalizar(nome),
            TipoMidia = NormalizarTipoMidia(tipoMidia),
            Hash = hash,
        };
    }

    private static void ExigirAceitavel(string nome, string tipoMidia, string hash)
    {
        if (RecusasDoAnexo((nome, tipoMidia, hash)).FirstOrDefault() is { } recusa)
        {
            throw new ArgumentException(recusa.Error.Message, recusa.Field);
        }
    }

    private static IEnumerable<FieldError> RecusasDosItens(IReadOnlyList<(string? Nome, string? TipoMidia, string? Hash)?> anexos)
    {
        HashSet<string> nomesJaVistos = new(NomeDoAnexoPublicado.Comparador);
        return anexos.SelectMany((anexo, posicao) =>
            RecusasDoItem(anexo, nomesJaVistos).Select(recusa => recusa.NoCaminhoDe($"anexos[{posicao}]")));
    }

    private static IEnumerable<FieldError> RecusasDoItem(
        (string? Nome, string? TipoMidia, string? Hash)? anexo, HashSet<string> nomesJaVistos) =>
        anexo is { } declarado
            ? [.. RecusasDoAnexo(declarado), .. RecusaDoNomeRepetido(declarado.Nome, nomesJaVistos)]
            : [new FieldError(string.Empty, AnexoDoAtoErros.Nulo())];

    private static IEnumerable<FieldError> RecusasDoAnexo((string? Nome, string? TipoMidia, string? Hash) anexo) =>
        new FieldError?[]
        {
            NomeValido(NomeDoAnexoPublicado.Normalizar(anexo.Nome)) ? null : new("nome", AnexoDoAtoErros.NomeInvalido()),
            TipoMidiaValido(NormalizarTipoMidia(anexo.TipoMidia)) ? null : new("tipoMidia", AnexoDoAtoErros.TipoMidiaInvalido()),
            HashSha256.TemFormatoValido(anexo.Hash) ? null : new("hash", AnexoDoAtoErros.HashInvalido()),
        }.OfType<FieldError>();

    private static IEnumerable<FieldError> RecusaDoNomeRepetido(string? nome, HashSet<string> nomesJaVistos)
    {
        string nomeNormalizado = NomeDoAnexoPublicado.Normalizar(nome);
        if (NomeValido(nomeNormalizado) && !nomesJaVistos.Add(nomeNormalizado))
        {
            yield return new("nome", AnexoDoAtoErros.NomeDuplicado());
        }
    }

    private static bool NomeValido(string nomeNormalizado) =>
        nomeNormalizado.Length is > 0 and <= NomeMaxLength;

    private static string NormalizarTipoMidia(string? tipoMidia) =>
        tipoMidia?.Trim().ToLower(CultureInfo.InvariantCulture) ?? string.Empty;

    private static bool TipoMidiaValido(string tipoMidia) =>
        tipoMidia.Length <= TipoMidiaMaxLength && FormatoDoTipoMidia().IsMatch(tipoMidia);

    /// <summary>Forma <c>tipo/subtipo</c> do tipo de mídia, já em minúsculas.</summary>
    [GeneratedRegex(@"^[a-z0-9][a-z0-9.+_-]*/[a-z0-9][a-z0-9.+_-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex FormatoDoTipoMidia();
}
