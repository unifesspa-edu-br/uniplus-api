namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// O comprovante que ocupa uma posição na ordem de emissão da inscrição, com quantos ela já emitiu: a posição é o número
/// público do comprovante, e o total diz se é o último, que é o que vale.
/// </summary>
/// <param name="Numero">A posição na ordem de emissão, a partir de 1.</param>
/// <param name="Total">Quantos comprovantes a inscrição emitiu.</param>
public sealed record ComprovanteNaPosicao(ComprovanteInscricao Comprovante, int Numero, int Total)
{
    /// <summary>O último comprovante emitido é o que vale; os anteriores foram substituídos por correção.</summary>
    public bool Vale => Numero == Total;

    /// <summary>O número do comprovante anterior, ou nulo no primeiro.</summary>
    public int? NumeroAnterior => Numero > 1 ? Numero - 1 : null;

    /// <summary>O número do comprovante seguinte, ou nulo no último.</summary>
    public int? NumeroSeguinte => Vale ? null : Numero + 1;
}

/// <summary>Quando uma inscrição emitiu um comprovante: o que a lista de inscrições precisa, sem o conteúdo nem a decifra.</summary>
public sealed record EmissaoDoComprovante(Guid InscricaoId, DateTimeOffset Instante);
