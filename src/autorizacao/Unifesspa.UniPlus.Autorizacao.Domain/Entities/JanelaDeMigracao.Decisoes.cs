namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>As decisões sobre as concessões da janela: o lote da gestão, a retida e a etapa da proteção de dados.</summary>
public sealed partial class JanelaDeMigracao
{
    /// <summary>
    /// O lote da gestão de acessos: decide cada elegível, menos as concessões do próprio gestor, que ficam retidas. O
    /// envio só vale completo e é atômico: toda recusa sai antes da primeira alteração.
    /// </summary>
    public Result EnviarLote(PedidoDoLote pedido, AutoriaDaVersao autoria, ContextoDaMigracao contexto)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        ArgumentNullException.ThrowIfNull(autoria);
        ArgumentNullException.ThrowIfNull(contexto);

        AutoriaDaVersao quem = ComConcessaoDoPerfil(autoria, contexto);
        if (ConferenciasDaMigracao.DoLote(this, pedido, quem.Pessoa, contexto) is { } recusa)
        {
            return Result.Failure(recusa);
        }

        List<Concessao> elegiveis = [.. contexto.Concessoes.Where(c => Elegivel(c, contexto.Agora)).OrderBy(static c => c.Id)];
        LoteDaMigracao lote = LoteDaMigracao.Registrar(
            this, EtapaDaMigracao.GestaoDeAcessos, quem, pedido.ConfirmouExclusao, elegiveis.Count, contexto.Agora);
        Acrescentar(lote);
        AplicarOLote(pedido, elegiveis, lote, new EfeitosDaDecisaoDaMigracao(this, contexto, quem));
        EncerrarSeConcluida(contexto);

        return Result.Success();
    }

    /// <summary>A concessão retida, que outra pessoa decide depois do lote; o que o lote decidiu não muda.</summary>
    public Result DecidirRetida(PedidoDaRetida pedido, AutoriaDaVersao autoria, ContextoDaMigracao contexto)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        ArgumentNullException.ThrowIfNull(autoria);
        ArgumentNullException.ThrowIfNull(contexto);

        AutoriaDaVersao quem = ComConcessaoDoPerfil(autoria, contexto);
        if (ConferenciasDaMigracao.DaRetida(this, pedido, quem.Pessoa, contexto) is { } recusa)
        {
            return Result.Failure(recusa);
        }

        new EfeitosDaDecisaoDaMigracao(this, contexto, quem).DaGestao(contexto.Concessao(pedido.ConcessaoId)!, pedido.Decisao!.Value, null);
        EncerrarSeConcluida(contexto);

        return Result.Success();
    }

    /// <summary>A etapa da proteção de dados, de uma concessão a várias; migra para a versão-alvo de agora ou revoga na hora.</summary>
    public Result DecidirEtapaDaProtecao(PedidoDaEtapaDaProtecao pedido, AutoriaDaVersao autoria, ContextoDaMigracao contexto)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        ArgumentNullException.ThrowIfNull(autoria);
        ArgumentNullException.ThrowIfNull(contexto);

        AutoriaDaVersao quem = ComConcessaoDoPerfil(autoria, contexto);
        if (ConferenciasDaMigracao.DaEtapaDaProtecao(this, pedido, quem.Pessoa, contexto) is { } recusa)
        {
            return Result.Failure(recusa);
        }

        LoteDaMigracao envio = LoteDaMigracao.Registrar(
            this, EtapaDaMigracao.ProtecaoDeDados, quem, pedido.ConfirmouExclusao, pedido.Itens.Count, contexto.Agora);
        Acrescentar(envio);
        EfeitosDaDecisaoDaMigracao efeitos = new(this, contexto, quem);
        pedido.Itens.ToList().ForEach(i => efeitos.DaProtecao(contexto.Concessao(i.ConcessaoId)!, i.Decisao!.Value, envio));
        EncerrarSeConcluida(contexto);

        return Result.Success();
    }

    // A decisão da gestão, por elegível: as do próprio gestor ficam retidas; o item que já não é elegível é ignorado.
    private static void AplicarOLote(
        PedidoDoLote pedido, IReadOnlyList<Concessao> elegiveis, LoteDaMigracao lote, EfeitosDaDecisaoDaMigracao efeitos)
    {
        Dictionary<Guid, ResultadoDaMigracao> decisoes = pedido.Itens.ToDictionary(static i => i.ConcessaoId, static i => i.Decisao!.Value);

        foreach (Concessao concessao in elegiveis)
        {
            if (decisoes.TryGetValue(concessao.Id, out ResultadoDaMigracao decisao))
            {
                efeitos.DaGestao(concessao, decisao, lote);
            }
            else
            {
                efeitos.Reter(concessao);
            }
        }
    }

    // Ter o perfil não impede o ato, mas a trilha registra: a conferência vale no instante da decisão.
    private static AutoriaDaVersao ComConcessaoDoPerfil(AutoriaDaVersao autoria, ContextoDaMigracao contexto) =>
        autoria with { TemConcessao = contexto.TemConcessaoDoPerfil(autoria.Pessoa) };
}
