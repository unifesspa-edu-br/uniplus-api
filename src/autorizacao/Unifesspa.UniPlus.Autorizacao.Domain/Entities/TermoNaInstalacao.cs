namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// A exceção do termo de responsabilidade na instalação: a instalação concede o perfil de gestor sem pedido e, portanto,
/// sem aceite. Como o termo só é publicado por gestor e a instalação só acontece sem gestor, ela antecede qualquer
/// publicação; se, por estado anômalo, já houver versão vigente, a exceção a registra e a instalação segue (ADR-0162).
/// Só se acrescenta: é evidência forense (ADR-0063).
/// </summary>
public sealed class TermoNaInstalacao : IForensicEntity
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid InstalacaoId { get; private init; }

    public SituacaoDoTermoNaInstalacao Situacao { get; private init; }

    /// <summary>A versão vigente lida na confirmação; nula quando não havia termo publicado.</summary>
    public Guid? VersaoDoTermoId { get; private init; }

    public int? Numero { get; private init; }

    public DateTimeOffset RegistradaEm { get; private init; }

    // Construtor de materialização do EF Core.
    private TermoNaInstalacao()
    {
    }

    /// <summary>Registra a exceção da instalação confirmada, com a vigência lida sob a trava compartilhada do termo.</summary>
    public static TermoNaInstalacao Registrar(Guid instalacaoId, VersaoDoTermo? vigente, DateTimeOffset instante) =>
        new()
        {
            InstalacaoId = instalacaoId,
            Situacao = vigente is null ? SituacaoDoTermoNaInstalacao.SemTermoPublicado : SituacaoDoTermoNaInstalacao.TermoVigenteSemAceite,
            VersaoDoTermoId = vigente?.Id,
            Numero = vigente?.Numero,
            RegistradaEm = instante,
        };
}
