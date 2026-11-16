namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// As decisões de um pedido e o que se pergunta a elas: quem aprovou como gestor, qual foi a última aprovação dele e se o
/// perfil que ele avaliou é o de agora. Opera sobre a coleção que o agregado guarda.
/// </summary>
internal readonly record struct DecisoesDoPedido(Guid PedidoId, List<DecisaoDoPedido> Itens)
{
    internal void Registrar(
        EtapaDoPedido etapa, AtoDoAgente ato, ResultadoDaDecisao resultado, string? motivo, PerfilAvaliado perfil, string? dispensa = null) =>
        Itens.Add(DecisaoDoPedido.Registrar(PedidoId, etapa, ato, resultado, motivo, perfil, dispensa));

    internal bool AprovouComoGestor(Identidade decisor) => Itens.Exists(d =>
        d.Etapa == EtapaDoPedido.Gestor && d.Resultado == ResultadoDaDecisao.Aprovado && d.Decisor == decisor);

    internal bool MesmoPerfilQueOGestorAvaliou(PerfilAvaliado perfil) =>
        UltimaAprovacaoDoGestor is { } gestor
        && gestor.PermissoesAvaliadas.SequenceEqual(perfil.Permissoes)
        && gestor.SensibilidadeAvaliada == perfil.Sensibilidade;

    // A mais recente é a de maior instante e, no mesmo instante, a registrada por último; relógio que regride vale pelo instante.
    private DecisaoDoPedido? UltimaAprovacaoDoGestor => Itens
        .Where(d => d.Etapa == EtapaDoPedido.Gestor && d.Resultado == ResultadoDaDecisao.Aprovado)
        .OrderBy(d => d.Instante)
        .ThenBy(d => d.PosicaoNoInstante)
        .LastOrDefault();
}
