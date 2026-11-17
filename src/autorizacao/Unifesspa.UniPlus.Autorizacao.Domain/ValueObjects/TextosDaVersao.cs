namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Os três textos de uma versão do perfil, já normalizados: nome, descrição e protocolo do documento instituidor. O texto
/// que não cabe nos limites fica nulo, e cada texto nulo dá o seu erro, na ordem em que o conteúdo da versão os acumula.
/// </summary>
internal sealed record TextosDaVersao(string? Nome, string? Descricao, string? Protocolo)
{
    private const int NomeMinLength = 3;
    private const int NomeMaxLength = 120;
    private const int DescricaoMinLength = 3;
    private const int DescricaoMaxLength = 500;
    private const int ProtocoloMinLength = 1;
    private const int ProtocoloMaxLength = 60;

    public static TextosDaVersao Normalizar(string? nome, string? descricao, string? protocolo) =>
        new(
            TextoLivre.Normalizar(nome, NomeMinLength, NomeMaxLength),
            TextoLivre.Normalizar(descricao, DescricaoMinLength, DescricaoMaxLength),
            TextoLivre.Normalizar(protocolo, ProtocoloMinLength, ProtocoloMaxLength));

    /// <summary>Os erros de cada texto que não cabe, na ordem de nome, descrição e protocolo.</summary>
    public IEnumerable<FieldError> Erros() =>
        new[]
        {
            Nome is null
                ? ConteudoDaVersao.Erro("nome", PerfilErrorCodes.NomeInvalido, $"O nome do perfil é obrigatório e deve ter entre {NomeMinLength} e {NomeMaxLength} caracteres.")
                : null,
            Descricao is null
                ? ConteudoDaVersao.Erro("descricao", PerfilErrorCodes.DescricaoInvalida, $"A descrição do perfil é obrigatória e deve ter entre {DescricaoMinLength} e {DescricaoMaxLength} caracteres.")
                : null,
            Protocolo is null
                ? ConteudoDaVersao.Erro("protocolo", PerfilErrorCodes.ProtocoloInvalido, $"O protocolo do documento instituidor é obrigatório e deve ter até {ProtocoloMaxLength} caracteres.")
                : null,
        }.OfType<FieldError>();
}
