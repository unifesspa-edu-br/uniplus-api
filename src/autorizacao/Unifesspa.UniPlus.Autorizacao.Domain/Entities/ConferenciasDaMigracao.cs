namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// As recusas do lote, da decisão da retida e da etapa da proteção de dados, na ordem em que a especificação as fixa.
/// Cada uma confere o pedido inteiro antes de qualquer alteração: o envio é gravado por inteiro ou não é gravado.
/// </summary>
internal static class ConferenciasDaMigracao
{
    public static DomainError? DoLote(
        JanelaDeMigracao janela, PedidoDoLote pedido, Identidade quem, ContextoDaMigracao contexto) =>
        janela.LoteDaGestao is not null ? MigracaoErros.LoteJaEnviado()
        : ItensInvalidos(janela, pedido.Itens, contexto)
        ?? DecidiuAPropria(pedido.Itens, quem, contexto)
        ?? FaltaDecidir(janela, pedido.Itens, quem, contexto)
        ?? RecusaSemConfirmacao(janela, pedido.Itens, pedido.ConfirmouExclusao, contexto);

    public static DomainError? DaRetida(
        JanelaDeMigracao janela, PedidoDaRetida pedido, Identidade quem, ContextoDaMigracao contexto) =>
        ForaDaEtapa(janela, [pedido.ConcessaoId], SituacaoDaMigracao.Retida, MigracaoErros.LoteNaoEnviado())
        ?? (pedido.Decisao is null or not (ResultadoDaMigracao.Migrar or ResultadoDaMigracao.Recusar) ? MigracaoErros.LoteInvalido() : null)
        ?? DecidiuAPropria([new ItemDoLote(pedido.ConcessaoId, pedido.Decisao)], quem, contexto)
        ?? (pedido.Decisao == ResultadoDaMigracao.Recusar && !pedido.ConfirmouExclusao ? MigracaoErros.ExclusaoNaoConfirmada() : null);

    public static DomainError? DaEtapaDaProtecao(
        JanelaDeMigracao janela, PedidoDaEtapaDaProtecao pedido, Identidade quem, ContextoDaMigracao contexto) =>
        ItensDaEtapaInvalidos(janela, pedido.Itens)
        ?? ForaDaEtapa(janela, [.. pedido.Itens.Select(static i => i.ConcessaoId)], SituacaoDaMigracao.AguardandoProtecaoDeDados, MigracaoErros.LoteInvalido())
        ?? DecidiuAPropria(pedido.Itens, quem, contexto)
        ?? DecidiuAGestao(janela, pedido.Itens, quem)
        ?? (pedido.Itens.Any(static i => i.Decisao == ResultadoDaMigracao.Recusar) && !pedido.ConfirmouExclusao
            ? MigracaoErros.ExclusaoNaoConfirmada()
            : null);

    private static bool DecisaoDePessoa(ResultadoDaMigracao? decisao) =>
        decisao is ResultadoDaMigracao.Migrar or ResultadoDaMigracao.Recusar;

    // Decisão que uma pessoa não pode dar, concessão repetida ou que não é do perfil.
    private static DomainError? ItensInvalidos(JanelaDeMigracao janela, IReadOnlyList<ItemDoLote> itens, ContextoDaMigracao contexto)
    {
        bool invalido = itens.Any(i => !DecisaoDePessoa(i.Decisao) || contexto.Concessao(i.ConcessaoId) is not { } c || c.PerfilId != janela.PerfilId);

        return invalido || itens.Select(static i => i.ConcessaoId).Distinct().Count() != itens.Count ? MigracaoErros.LoteInvalido() : null;
    }

    private static DomainError? ItensDaEtapaInvalidos(JanelaDeMigracao janela, IReadOnlyList<ItemDoLote> itens)
    {
        bool invalido = itens.Count == 0
            || itens.Any(i => !DecisaoDePessoa(i.Decisao) || janela.LinhaDe(i.ConcessaoId) is null)
            || itens.Select(static i => i.ConcessaoId).Distinct().Count() != itens.Count;

        return invalido ? MigracaoErros.LoteInvalido() : null;
    }

    // A linha tem de estar na situação que a etapa decide: a final responde conflito, a de outra etapa, fora da etapa.
    private static DomainError? ForaDaEtapa(
        JanelaDeMigracao janela, IEnumerable<Guid> concessoes, SituacaoDaMigracao esperada, DomainError semLinha)
    {
        if (janela.LoteDaGestao is null)
        {
            return semLinha;
        }

        foreach (Guid concessaoId in concessoes)
        {
            DomainError? recusa = janela.LinhaDe(concessaoId) is { } linha ? RecusaDaLinha(linha, esperada) : semLinha;
            if (recusa is not null)
            {
                return recusa;
            }
        }

        return null;
    }

    private static DomainError? RecusaDaLinha(MigracaoDaConcessao linha, SituacaoDaMigracao esperada) =>
        linha.Situacao == esperada ? null
        : linha.Situacao.EhFinal() ? MigracaoErros.ConcessaoJaDecidida()
        : MigracaoErros.ConcessaoForaDaEtapa();

    private static DomainError? DecidiuAPropria(IReadOnlyList<ItemDoLote> itens, Identidade quem, ContextoDaMigracao contexto) =>
        itens.Any(i => contexto.Concessao(i.ConcessaoId)?.Beneficiario == quem) ? MigracaoErros.DecisaoDaPropriaConcessao() : null;

    private static DomainError? FaltaDecidir(
        JanelaDeMigracao janela, IReadOnlyList<ItemDoLote> itens, Identidade quem, ContextoDaMigracao contexto)
    {
        HashSet<Guid> decididas = [.. itens.Select(static i => i.ConcessaoId)];
        bool falta = contexto.Concessoes.Any(c =>
            janela.Elegivel(c, contexto.Agora) && c.Beneficiario != quem && !decididas.Contains(c.Id));

        return falta ? MigracaoErros.LoteIncompleto() : null;
    }

    // Só conta a recusa de quem ainda é elegível: o item que deixou de ser é ignorado.
    private static DomainError? RecusaSemConfirmacao(
        JanelaDeMigracao janela, IReadOnlyList<ItemDoLote> itens, bool confirmou, ContextoDaMigracao contexto)
    {
        bool recusa = itens.Any(i =>
            i.Decisao == ResultadoDaMigracao.Recusar
            && contexto.Concessao(i.ConcessaoId) is { } c
            && janela.Elegivel(c, contexto.Agora));

        return recusa && !confirmou ? MigracaoErros.ExclusaoNaoConfirmada() : null;
    }

    // A pessoa que decidiu a etapa da gestão não decide a da proteção de dados da mesma concessão.
    private static DomainError? DecidiuAGestao(JanelaDeMigracao janela, IReadOnlyList<ItemDoLote> itens, Identidade quem)
    {
        HashSet<Guid> concessoes = [.. itens.Select(static i => i.ConcessaoId)];
        bool mesma = janela.Decisoes.Any(d =>
            concessoes.Contains(d.ConcessaoId)
            && d.Ator == AtorDaMigracao.Humano
            && d.Etapa == EtapaDaMigracao.GestaoDeAcessos
            && d.Decisor == quem);

        return mesma ? MigracaoErros.MesmaPessoaNasDuasEtapas() : null;
    }
}
