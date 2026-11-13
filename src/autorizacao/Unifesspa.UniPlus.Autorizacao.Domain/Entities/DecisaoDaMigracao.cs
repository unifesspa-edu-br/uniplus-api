namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// Decisão sobre uma concessão da janela de migração, de uma pessoa ou do sistema. Só se acrescenta, nunca se altera: é
/// evidência forense (ADR-0063), com quem decidiu, a etapa, a composição avaliada e se quem decidiu
/// tem o perfil, que é o risco que a ADR-0157 aceita. O decisor humano e o sistema ficam em ramos separados.
/// </summary>
public sealed class DecisaoDaMigracao : IForensicEntity
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid JanelaId { get; private set; }

    public Guid PerfilId { get; private set; }

    public Guid ConcessaoId { get; private set; }

    /// <summary>O lote da gestão a que a decisão pertence; nulo só na decisão do sistema de uma janela sem lote.</summary>
    public Guid? LoteDeOrigemId { get; private set; }

    public EtapaDaMigracao? LoteDeOrigemEtapa { get; private set; }

    /// <summary>O envio que produziu a decisão; nulo na retenção, na decisão da retida e nas do sistema.</summary>
    public Guid? EnvioId { get; private set; }

    public EtapaDaMigracao? EnvioEtapa { get; private set; }

    public EtapaDaMigracao Etapa { get; private set; }

    public ResultadoDaMigracao Resultado { get; private set; }

    public AtorDaMigracao Ator { get; private set; }

    public string? DecisorEmissor { get; private set; }

    public string? DecisorSub { get; private set; }

    public string? DecisorNome { get; private set; }

    public bool? DecisorTemConcessao { get; private set; }

    public Guid VersaoAvaliadaId { get; private set; }

    public string[] ComposicaoAvaliada { get; private set; } = [];

    public string[] AcrescimoAvaliado { get; private set; } = [];

    public DateTimeOffset Prazo { get; private set; }

    public DateTimeOffset Instante { get; private set; }

    /// <summary>
    /// A ordem em que as decisões foram registradas, dada pelo banco: desempata duas decisões da mesma concessão do mesmo
    /// instante, que o lote da janela serializa sob a trava das concessões.
    /// </summary>
    public long OrdemDeRegistro { get; private set; }

    public Identidade? Decisor =>
        DecisorEmissor is null || DecisorSub is null ? null : new Identidade(DecisorEmissor, DecisorSub, DecisorNome);

    // Construtor de materialização do EF Core.
    private DecisaoDaMigracao()
    {
    }

    internal static DecisaoDaMigracao Humana(
        JanelaDeMigracao janela, Guid concessaoId, DecisaoHumana decisao, ComposicaoDoLote composicao, DateTimeOffset instante)
    {
        DecisaoDaMigracao nova = Nova(janela, concessaoId, composicao, instante);
        nova.Etapa = decisao.Etapa;
        nova.Resultado = decisao.Resultado;
        nova.Ator = AtorDaMigracao.Humano;
        nova.DecisorEmissor = decisao.Autoria.Pessoa.Emissor;
        nova.DecisorSub = decisao.Autoria.Pessoa.Sub;
        nova.DecisorNome = decisao.Autoria.Pessoa.Nome;
        nova.DecisorTemConcessao = decisao.Autoria.TemConcessao;
        nova.EnvioId = decisao.Envio?.Id;
        nova.EnvioEtapa = decisao.Envio?.Etapa;

        return nova;
    }

    internal static DecisaoDaMigracao DoSistema(
        JanelaDeMigracao janela, Guid concessaoId, ResultadoDaMigracao resultado, ComposicaoDoLote composicao, DateTimeOffset instante)
    {
        DecisaoDaMigracao nova = Nova(janela, concessaoId, composicao, instante);
        nova.Etapa = resultado == ResultadoDaMigracao.Reter ? EtapaDaMigracao.GestaoDeAcessos : EtapaDaMigracao.Sistema;
        nova.Resultado = resultado;
        nova.Ator = AtorDaMigracao.Sistema;

        return nova;
    }

    private static DecisaoDaMigracao Nova(
        JanelaDeMigracao janela, Guid concessaoId, ComposicaoDoLote composicao, DateTimeOffset instante) =>
        new()
        {
            JanelaId = janela.Id,
            PerfilId = janela.PerfilId,
            ConcessaoId = concessaoId,
            LoteDeOrigemId = janela.LoteDaGestao?.Id,
            LoteDeOrigemEtapa = janela.LoteDaGestao?.Etapa,
            VersaoAvaliadaId = janela.VersaoAlvoId,
            ComposicaoAvaliada = [.. composicao.Avaliada],
            AcrescimoAvaliado = [.. composicao.Acrescimo],
            Prazo = janela.Prazo,
            Instante = instante,
        };
}
