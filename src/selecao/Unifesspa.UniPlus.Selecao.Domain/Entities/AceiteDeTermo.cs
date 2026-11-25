namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>O aceite de um termo, com a versão e o hash que a inscrição carrega da versão da inscrição.</summary>
public sealed record AceiteDeTermo(string Codigo, Guid VersaoId, string HashVersao)
{
    /// <summary>O hash identifica o conteúdo exato, inclusive maiúsculas e minúsculas.</summary>
    public bool ValeParaAVersao(TermoAdmitido termo)
    {
        ArgumentNullException.ThrowIfNull(termo);
        return VersaoId == termo.VersaoId && string.Equals(HashVersao, termo.HashVersao, StringComparison.Ordinal);
    }
}
