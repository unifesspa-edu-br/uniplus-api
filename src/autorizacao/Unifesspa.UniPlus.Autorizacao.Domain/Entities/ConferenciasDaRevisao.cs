namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// As recusas do envio da gestão, da decisão da retida e da etapa da proteção de dados, na ordem em que a especificação as fixa.
/// Cada uma confere o pedido inteiro antes de qualquer alteração: o envio é gravado por inteiro ou não é gravado.
/// </summary>
internal static class ConferenciasDaRevisao
{
    public static DomainError? DoEnvioDaGestao(
        RevisaoDasConcessoesDoPerfil revisao, PedidoDoEnvio pedido, Identidade quem, ContextoDaRevisao contexto) =>
        revisao.EnvioDaGestao is not null ? RevisaoDasConcessoesErros.DecisoesJaEnviadas()
        : ItensInvalidos(revisao, pedido.Itens, contexto)
        ?? DecidiuAPropria(pedido.Itens, quem, contexto)
        ?? FaltaDecidir(revisao, pedido.Itens, quem, contexto)
        ?? RecusaSemConfirmacao(revisao, pedido.Itens, pedido.ConfirmouExclusao, contexto);

    public static DomainError? DaRetida(
        RevisaoDasConcessoesDoPerfil revisao, PedidoDaRetida pedido, Identidade quem, ContextoDaRevisao contexto) =>
        ForaDaEtapa(revisao, [pedido.ConcessaoId], SituacaoDaMigracao.Retida, RevisaoDasConcessoesErros.DecisoesNaoEnviadas())
        ?? (pedido.Decisao is null or not (ResultadoDaMigracao.Migrar or ResultadoDaMigracao.Recusar) ? RevisaoDasConcessoesErros.DecisoesInvalidas() : null)
        ?? DecidiuAPropria([new ItemDoEnvio(pedido.ConcessaoId, pedido.Decisao)], quem, contexto)
        ?? (pedido.Decisao == ResultadoDaMigracao.Recusar && !pedido.ConfirmouExclusao ? RevisaoDasConcessoesErros.ExclusaoNaoConfirmada() : null);

    public static DomainError? DaEtapaDaProtecao(
        RevisaoDasConcessoesDoPerfil revisao, PedidoDaEtapaDaProtecao pedido, Identidade quem, ContextoDaRevisao contexto) =>
        ItensDaEtapaInvalidos(revisao, pedido.Itens)
        ?? ForaDaEtapa(revisao, [.. pedido.Itens.Select(static i => i.ConcessaoId)], SituacaoDaMigracao.AguardandoProtecaoDeDados, RevisaoDasConcessoesErros.DecisoesInvalidas())
        ?? DecidiuAPropria(pedido.Itens, quem, contexto)
        ?? DecidiuAGestao(revisao, pedido.Itens, quem)
        ?? (pedido.Itens.Any(static i => i.Decisao == ResultadoDaMigracao.Recusar) && !pedido.ConfirmouExclusao
            ? RevisaoDasConcessoesErros.ExclusaoNaoConfirmada()
            : null);

    private static bool DecisaoDePessoa(ResultadoDaMigracao? decisao) =>
        decisao is ResultadoDaMigracao.Migrar or ResultadoDaMigracao.Recusar;

    // Decisão que uma pessoa não pode dar, concessão repetida ou que não é do perfil.
    private static DomainError? ItensInvalidos(RevisaoDasConcessoesDoPerfil revisao, IReadOnlyList<ItemDoEnvio> itens, ContextoDaRevisao contexto)
    {
        bool invalido = itens.Any(i => !DecisaoDePessoa(i.Decisao) || contexto.Concessao(i.ConcessaoId) is not { } c || c.PerfilId != revisao.PerfilId);

        return invalido || itens.Select(static i => i.ConcessaoId).Distinct().Count() != itens.Count ? RevisaoDasConcessoesErros.DecisoesInvalidas() : null;
    }

    private static DomainError? ItensDaEtapaInvalidos(RevisaoDasConcessoesDoPerfil revisao, IReadOnlyList<ItemDoEnvio> itens)
    {
        bool invalido = itens.Count == 0
            || itens.Any(i => !DecisaoDePessoa(i.Decisao) || revisao.LinhaDe(i.ConcessaoId) is null)
            || itens.Select(static i => i.ConcessaoId).Distinct().Count() != itens.Count;

        return invalido ? RevisaoDasConcessoesErros.DecisoesInvalidas() : null;
    }

    // A linha tem de estar na situação que a etapa decide: a final responde conflito, a de outra etapa, fora da etapa.
    private static DomainError? ForaDaEtapa(
        RevisaoDasConcessoesDoPerfil revisao, IEnumerable<Guid> concessoes, SituacaoDaMigracao esperada, DomainError semLinha)
    {
        return revisao.EnvioDaGestao is null
            ? semLinha
            : concessoes.Select(concessaoId => revisao.LinhaDe(concessaoId) is { } linha ? RecusaDaLinha(linha, esperada) : semLinha).FirstOrDefault(static recusa => recusa is not null);
    }

    private static DomainError? RecusaDaLinha(MigracaoDaConcessao linha, SituacaoDaMigracao esperada) =>
        linha.Situacao == esperada ? null
        : linha.Situacao.EhFinal() ? RevisaoDasConcessoesErros.ConcessaoJaDecidida()
        : RevisaoDasConcessoesErros.ConcessaoForaDaEtapa();

    private static DomainError? DecidiuAPropria(IReadOnlyList<ItemDoEnvio> itens, Identidade quem, ContextoDaRevisao contexto) =>
        itens.Any(i => contexto.Concessao(i.ConcessaoId)?.Beneficiario == quem) ? RevisaoDasConcessoesErros.DecisaoDaPropriaConcessao() : null;

    private static DomainError? FaltaDecidir(
        RevisaoDasConcessoesDoPerfil revisao, IReadOnlyList<ItemDoEnvio> itens, Identidade quem, ContextoDaRevisao contexto)
    {
        HashSet<Guid> decididas = [.. itens.Select(static i => i.ConcessaoId)];
        bool falta = contexto.Concessoes.Any(c =>
            revisao.Elegivel(c, contexto.Agora) && c.Beneficiario != quem && !decididas.Contains(c.Id));

        return falta ? RevisaoDasConcessoesErros.DecisoesIncompletas() : null;
    }

    // Só conta a recusa de quem ainda é elegível: o item que deixou de ser é ignorado.
    private static DomainError? RecusaSemConfirmacao(
        RevisaoDasConcessoesDoPerfil revisao, IReadOnlyList<ItemDoEnvio> itens, bool confirmou, ContextoDaRevisao contexto)
    {
        bool recusa = itens.Any(i =>
            i.Decisao == ResultadoDaMigracao.Recusar
            && contexto.Concessao(i.ConcessaoId) is { } c
            && revisao.Elegivel(c, contexto.Agora));

        return recusa && !confirmou ? RevisaoDasConcessoesErros.ExclusaoNaoConfirmada() : null;
    }

    // A pessoa que decidiu a etapa da gestão não decide a da proteção de dados da mesma concessão.
    private static DomainError? DecidiuAGestao(RevisaoDasConcessoesDoPerfil revisao, IReadOnlyList<ItemDoEnvio> itens, Identidade quem)
    {
        HashSet<Guid> decididas = ConcessoesDecididasNaGestaoPor(revisao, quem);

        return itens.Any(i => decididas.Contains(i.ConcessaoId)) ? RevisaoDasConcessoesErros.MesmaPessoaNasDuasEtapas() : null;
    }

    /// <summary>As concessões cuja etapa da gestão de acessos a pessoa decidiu, e que ela não pode decidir na proteção de dados.</summary>
    public static HashSet<Guid> ConcessoesDecididasNaGestaoPor(RevisaoDasConcessoesDoPerfil revisao, Identidade quem) =>
    [
        .. revisao.Decisoes
            .Where(d => d.Ator == AtorDaMigracao.Humano && d.Etapa == EtapaDaMigracao.GestaoDeAcessos && d.Decisor == quem)
            .Select(static d => d.ConcessaoId),
    ];
}
