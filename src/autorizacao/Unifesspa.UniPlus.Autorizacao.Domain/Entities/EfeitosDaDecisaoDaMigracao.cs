namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// O que uma decisão da janela faz a uma concessão: a migra para a versão-alvo, a revoga, a deixa esperando a proteção de
/// dados ou a retém, e grava a linha da concessão e a decisão da trilha com o que foi avaliado. O mesmo efeito vale para
/// o lote, para a retida e para a etapa da proteção de dados.
/// </summary>
internal sealed class EfeitosDaDecisaoDaMigracao
{
    private const string MotivoDaRecusa = "recusado na migração";

    private readonly JanelaDeMigracao _janela;
    private readonly ContextoDaMigracao _contexto;
    private readonly AutoriaDaVersao _autoria;

    public EfeitosDaDecisaoDaMigracao(JanelaDeMigracao janela, ContextoDaMigracao contexto, AutoriaDaVersao autoria)
    {
        _janela = janela;
        _contexto = contexto;
        _autoria = autoria;
    }

    /// <summary>A concessão do próprio gestor que enviou o lote fica retida: só o sistema registra, e quem enviou nunca decide.</summary>
    public void Reter(Concessao concessao)
    {
        ComposicaoDoLote composicao = _contexto.ComposicaoDe(concessao);

        _janela.Registrar(concessao.Id, SituacaoDaMigracao.Retida, composicao.ExigeProtecaoDeDados, null, _contexto.Agora);
        _janela.Acrescentar(DecisaoDaMigracao.DoSistema(_janela, concessao.Id, ResultadoDaMigracao.Reter, composicao, _contexto.Agora));
    }

    /// <summary>
    /// A decisão da gestão de acessos, do lote ou da retida. Migrar a concessão cujo acréscimo é protegido a deixa
    /// aguardando a proteção de dados, salvo se o beneficiário já tem o perfil dela.
    /// </summary>
    public void DaGestao(Concessao concessao, ResultadoDaMigracao resultado, LoteDaMigracao? envio)
    {
        ComposicaoDoLote composicao = _contexto.ComposicaoDe(concessao);
        string? dispensa = DispensaDe(concessao, composicao);

        if (resultado == ResultadoDaMigracao.Recusar)
        {
            Recusar(concessao, composicao, EtapaDaMigracao.GestaoDeAcessos, envio);
        }
        else if (composicao.ExigeProtecaoDeDados && dispensa is null)
        {
            Aguardar(concessao, composicao, envio);
        }
        else
        {
            Migrar(concessao, composicao, new DecisaoHumana(EtapaDaMigracao.GestaoDeAcessos, resultado, _autoria, envio, dispensa));
        }
    }

    // Quem já tem o perfil da proteção de dados dispensa a segunda etapa do acréscimo protegido.
    private string? DispensaDe(Concessao concessao, ComposicaoDoLote composicao) =>
        composicao.ExigeProtecaoDeDados && _contexto.TemProtecaoDeDados(concessao.Beneficiario)
            ? MotivosDeDispensa.BeneficiarioJaTemProtecaoDeDados
            : null;

    /// <summary>A decisão da proteção de dados: migrar leva à versão-alvo de agora; recusar revoga na hora.</summary>
    public void DaProtecao(Concessao concessao, ResultadoDaMigracao resultado, LoteDaMigracao envio)
    {
        ComposicaoDoLote composicao = _contexto.ComposicaoDe(concessao);

        if (resultado == ResultadoDaMigracao.Recusar)
        {
            Recusar(concessao, composicao, EtapaDaMigracao.ProtecaoDeDados, envio);
        }
        else
        {
            Migrar(concessao, composicao, new DecisaoHumana(EtapaDaMigracao.ProtecaoDeDados, resultado, _autoria, envio, null));
        }
    }

    private void Migrar(Concessao concessao, ComposicaoDoLote composicao, DecisaoHumana decisao)
    {
        concessao.MigrarParaVersao(_contexto.Versao(concessao.VersaoId!.Value), _contexto.Versao(_janela.VersaoAlvoId));
        Gravar(concessao, SituacaoDaMigracao.Migrada, composicao, decisao);
    }

    private void Recusar(Concessao concessao, ComposicaoDoLote composicao, EtapaDaMigracao etapa, LoteDaMigracao? envio)
    {
        concessao.RevogarPelaMigracao(_janela.Id, MotivoDaRecusa, _contexto.Agora);
        Gravar(concessao, SituacaoDaMigracao.Recusada, composicao, new DecisaoHumana(etapa, ResultadoDaMigracao.Recusar, _autoria, envio, null));
    }

    private void Aguardar(Concessao concessao, ComposicaoDoLote composicao, LoteDaMigracao? envio) =>
        Gravar(
            concessao,
            SituacaoDaMigracao.AguardandoProtecaoDeDados,
            composicao,
            new DecisaoHumana(EtapaDaMigracao.GestaoDeAcessos, ResultadoDaMigracao.Migrar, _autoria, envio, null));

    private void Gravar(Concessao concessao, SituacaoDaMigracao situacao, ComposicaoDoLote composicao, DecisaoHumana decisao)
    {
        _janela.Registrar(concessao.Id, situacao, composicao.ExigeProtecaoDeDados, decisao.Dispensa, _contexto.Agora);
        _janela.Acrescentar(DecisaoDaMigracao.Humana(_janela, concessao.Id, decisao, composicao, _contexto.Agora));
    }
}
