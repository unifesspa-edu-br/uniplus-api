namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// Um envio de decisões da janela: o lote da gestão de acessos, que decide a lista inteira, ou uma remessa da proteção de
/// dados. Só se acrescenta, nunca se altera: é evidência forense (ADR-0063) de quem enviou, do que confirmou e de quantas
/// concessões decidiu.
/// </summary>
public sealed class LoteDaMigracao : IForensicEntity
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid JanelaId { get; private init; }

    public Guid PerfilId { get; private init; }

    /// <summary>A etapa do envio: a gestão de acessos ou a proteção de dados.</summary>
    public EtapaDaMigracao Etapa { get; private init; }

    public string EnviadoPorEmissor { get; private init; } = string.Empty;

    public string EnviadoPorSub { get; private init; } = string.Empty;

    public string? EnviadoPorNome { get; private init; }

    public bool EnviadoPorTemConcessao { get; private init; }

    public bool ConfirmouExclusao { get; private init; }

    /// <summary>Quantas concessões o envio decidiu, as retidas inclusive.</summary>
    public int Quantidade { get; private init; }

    public DateTimeOffset Instante { get; private init; }

    // Construtor de materialização do EF Core.
    private LoteDaMigracao()
    {
    }

    internal static LoteDaMigracao Registrar(
        JanelaDeMigracao janela, EtapaDaMigracao etapa, AutoriaDaVersao autoria, bool confirmouExclusao, int quantidade, DateTimeOffset instante) =>
        new()
        {
            JanelaId = janela.Id,
            PerfilId = janela.PerfilId,
            Etapa = etapa,
            EnviadoPorEmissor = autoria.Pessoa.Emissor,
            EnviadoPorSub = autoria.Pessoa.Sub,
            EnviadoPorNome = autoria.Pessoa.Nome,
            EnviadoPorTemConcessao = autoria.TemConcessao,
            ConfirmouExclusao = confirmouExclusao,
            Quantidade = quantidade,
            Instante = instante,
        };
}
