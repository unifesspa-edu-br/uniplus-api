namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O registro único da vigência do termo de responsabilidade: qual versão vale agora e quantas já se publicaram. A
/// vigente é sempre a publicada mais recente (UNI-REQ-0377); não há revogação de versão, e a correção é publicar outra.
/// </summary>
/// <remarks>
/// A linha deste registro é a trava da vigência na ordem única da ADR-0162: a publicação a toma para escrita, e a criação
/// de pedido protegido e a instalação, compartilhada. O registro existe desde a carga inicial, sem versão vigente.
/// </remarks>
public sealed class TermoDeResponsabilidade : EntityBase
{
    /// <summary>Identificador fixo do registro único, gravado pela carga inicial.</summary>
    public static readonly Guid RegistroUnicoId = new("019a0000-0000-7000-8000-0000000000a1");

    private const string MensagemDaVigenteAlterada =
        "Outra versão do termo foi publicada depois que a tela foi aberta. Confira a versão vigente antes de publicar outra.";

    /// <summary>A versão em vigor; nula até a primeira publicação.</summary>
    public Guid? VersaoVigenteId { get; private set; }

    /// <summary>O número da última versão publicada; zero antes da primeira.</summary>
    public int UltimoNumero { get; private set; }

    // Construtor de materialização do EF Core.
    private TermoDeResponsabilidade()
    {
    }

    /// <summary>
    /// Publica a versão seguinte e a torna vigente. Recusa com conflito se a vigente não é a que o publicador viu, e recusa
    /// o texto igual ao da vigente, que não teria o que publicar.
    /// </summary>
    /// <param name="vigente">A versão vigente lida sob a trava; nula antes da primeira publicação.</param>
    /// <param name="vigenteEsperada">A vigente que o publicador viu na tela; nula se ele não viu nenhuma.</param>
    public Result<VersaoDoTermo> Publicar(ConteudoDoTermo conteudo, Identidade autor, VersaoDoTermo? vigente, Guid? vigenteEsperada, DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(conteudo);
        ArgumentNullException.ThrowIfNull(autor);

        if (RecusaDaPublicacao(conteudo, vigente, vigenteEsperada) is { } recusa)
        {
            return Result<VersaoDoTermo>.Failure(recusa);
        }

        VersaoDoTermo nova = VersaoDoTermo.Registrar(Id, UltimoNumero + 1, conteudo, autor, instante);
        UltimoNumero = nova.Numero;
        VersaoVigenteId = nova.Id;

        return Result<VersaoDoTermo>.Success(nova);
    }

    private DomainError? RecusaDaPublicacao(ConteudoDoTermo conteudo, VersaoDoTermo? vigente, Guid? vigenteEsperada)
    {
        if (vigente?.Id != VersaoVigenteId)
        {
            throw new InvalidOperationException($"A versão informada como vigente não é a {VersaoVigenteId} do registro do termo.");
        }

        if (vigenteEsperada != VersaoVigenteId)
        {
            return new DomainError(TermoErrorCodes.VigenteAlterado, MensagemDaVigenteAlterada);
        }

        return vigente is not null && string.Equals(vigente.Resumo, conteudo.Resumo, StringComparison.Ordinal)
            ? new DomainError(TermoErrorCodes.TextoIgualAoVigente, "O texto é igual ao da versão vigente; não há o que publicar.")
            : null;
    }
}
