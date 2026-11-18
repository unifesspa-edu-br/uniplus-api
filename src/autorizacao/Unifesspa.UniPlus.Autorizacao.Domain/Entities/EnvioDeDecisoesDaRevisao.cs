namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// Um envio de decisões da revisão: o da gestão de acessos, que decide a lista inteira, ou uma remessa da proteção de
/// dados. Só se acrescenta, nunca se altera: é evidência forense (ADR-0063) de quem enviou, do que confirmou e de quantas
/// concessões decidiu.
/// </summary>
public sealed class EnvioDeDecisoesDaRevisao : IForensicEntity
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid RevisaoId { get; private init; }

    public Guid PerfilId { get; private init; }

    /// <summary>A etapa do envio: a gestão de acessos ou a proteção de dados.</summary>
    public EtapaDaMigracao Etapa { get; private init; }

    public string EnviadoPorEmissor { get; private init; } = string.Empty;

    public string EnviadoPorContaId { get; private init; } = string.Empty;

    public string? EnviadoPorNome { get; private init; }

    public bool EnviadoPorTemConcessao { get; private init; }

    public bool ConfirmouExclusao { get; private init; }

    /// <summary>Quantas concessões o envio decidiu, as retidas inclusive.</summary>
    public int Quantidade { get; private init; }

    public DateTimeOffset Instante { get; private init; }

    // Construtor de materialização do EF Core.
    private EnvioDeDecisoesDaRevisao()
    {
    }

    internal static EnvioDeDecisoesDaRevisao Registrar(
        RevisaoDasConcessoesDoPerfil revisao, EtapaDaMigracao etapa, AutoriaDaVersao autoria, bool confirmouExclusao, int quantidade, DateTimeOffset instante) =>
        new()
        {
            RevisaoId = revisao.Id,
            PerfilId = revisao.PerfilId,
            Etapa = etapa,
            EnviadoPorEmissor = autoria.Pessoa.Emissor,
            EnviadoPorContaId = autoria.Pessoa.ContaId,
            EnviadoPorNome = autoria.Pessoa.Nome,
            EnviadoPorTemConcessao = autoria.TemConcessao,
            ConfirmouExclusao = confirmouExclusao,
            Quantidade = quantidade,
            Instante = instante,
        };
}
