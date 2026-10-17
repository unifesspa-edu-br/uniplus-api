namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;

/// <summary>
/// Um anexo de <see cref="AtoDoProcesso"/>: referência a um <see cref="DocumentoAPublicar"/>
/// confirmado, com o nome pelo qual o anexo se apresenta ao público.
/// </summary>
/// <remarks>
/// O arquivo e o seu hash continuam sendo do documento a publicar, que é imutável depois de
/// confirmado; aqui só mora o que é do anexo — a posição e o nome de exibição, que pode diferir do
/// nome do arquivo enviado.
/// </remarks>
public sealed class AnexoDoAtoDoProcesso : EntityBase
{
    /// <summary>Tamanho máximo do nome de exibição.</summary>
    public const int NomeDeExibicaoMaxLength = 200;

    public Guid AtoDoProcessoId { get; private set; }
    public Guid DocumentoAPublicarId { get; private set; }
    public string NomeDeExibicao { get; private set; } = string.Empty;

    /// <summary>Posição do anexo na lista do ato, a partir de zero — a ordem em que foram declarados.</summary>
    public int Ordem { get; private set; }

    private AnexoDoAtoDoProcesso() { }

    internal static AnexoDoAtoDoProcesso Criar(Guid atoDoProcessoId, int ordem, Guid documentoAPublicarId, string nomeDeExibicao)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nomeDeExibicao);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(nomeDeExibicao.Length, NomeDeExibicaoMaxLength);
        if (documentoAPublicarId == Guid.Empty)
        {
            throw new ArgumentException("O anexo deve referenciar um documento a publicar.", nameof(documentoAPublicarId));
        }

        return new AnexoDoAtoDoProcesso
        {
            AtoDoProcessoId = atoDoProcessoId,
            DocumentoAPublicarId = documentoAPublicarId,
            NomeDeExibicao = nomeDeExibicao,
            Ordem = ordem,
        };
    }
}
