namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// O que uma decisão da revisão faz a uma concessão: a migra para a versão-alvo, a revoga, a deixa esperando a etapa de
/// proteção de dados ou a retém, e grava a linha da concessão e a decisão da trilha com o que foi avaliado. O mesmo efeito
/// vale para o envio da gestão, para a retida e para a etapa de proteção de dados.
/// </summary>
internal sealed class AplicacaoDaDecisaoDaRevisao
{
    private const string MotivoDaRecusa = "recusado na migração";

    private readonly RevisaoDasConcessoesDoPerfil _revisao;
    private readonly ContextoDaRevisao _contexto;
    private readonly AutoriaDaVersao _autoria;

    public AplicacaoDaDecisaoDaRevisao(RevisaoDasConcessoesDoPerfil revisao, ContextoDaRevisao contexto, AutoriaDaVersao autoria)
    {
        _revisao = revisao;
        _contexto = contexto;
        _autoria = autoria;
    }

    /// <summary>A concessão do próprio gestor que enviou as decisões fica retida: só o sistema registra, e quem enviou nunca decide.</summary>
    public void Reter(Concessao concessao)
    {
        ComposicaoDoEnvio composicao = _contexto.ComposicaoDe(concessao);

        _revisao.Registrar(concessao.Id, SituacaoDaMigracao.Retida, composicao.ExigeProtecaoDeDados, _contexto.Agora);
        _revisao.Acrescentar(DecisaoDaMigracao.DoSistema(_revisao, concessao.Id, ResultadoDaMigracao.Reter, composicao, _contexto.Agora));
    }

    /// <summary>
    /// A decisão da gestão de acessos, do envio ou da retida. Migrar a concessão cujo acréscimo é protegido a deixa
    /// aguardando o Representante do controlador, inclusive quando o beneficiário é Representante (ADR-0162).
    /// </summary>
    public void DaGestao(Concessao concessao, ResultadoDaMigracao resultado, EnvioDeDecisoesDaRevisao? envio)
    {
        ComposicaoDoEnvio composicao = _contexto.ComposicaoDe(concessao);

        if (resultado == ResultadoDaMigracao.Recusar)
        {
            Recusar(concessao, composicao, EtapaDaMigracao.GestaoDeAcessos, envio);
        }
        else if (composicao.ExigeProtecaoDeDados)
        {
            Aguardar(concessao, composicao, envio);
        }
        else
        {
            Migrar(concessao, composicao, new DecisaoHumana(EtapaDaMigracao.GestaoDeAcessos, resultado, _autoria, envio));
        }
    }

    /// <summary>A decisão do Representante do controlador: migrar leva à versão-alvo de agora; recusar revoga na hora.</summary>
    public void DaProtecao(Concessao concessao, ResultadoDaMigracao resultado, EnvioDeDecisoesDaRevisao envio)
    {
        ComposicaoDoEnvio composicao = _contexto.ComposicaoDe(concessao);

        if (resultado == ResultadoDaMigracao.Recusar)
        {
            Recusar(concessao, composicao, EtapaDaMigracao.ProtecaoDeDados, envio);
        }
        else
        {
            Migrar(concessao, composicao, new DecisaoHumana(EtapaDaMigracao.ProtecaoDeDados, resultado, _autoria, envio));
        }
    }

    private void Migrar(Concessao concessao, ComposicaoDoEnvio composicao, DecisaoHumana decisao)
    {
        concessao.MigrarParaVersao(_contexto.Versao(concessao.VersaoId!.Value), _contexto.Versao(_revisao.VersaoAlvoId));
        Gravar(concessao, SituacaoDaMigracao.Migrada, composicao, decisao);
    }

    private void Recusar(Concessao concessao, ComposicaoDoEnvio composicao, EtapaDaMigracao etapa, EnvioDeDecisoesDaRevisao? envio)
    {
        concessao.RevogarPelaMigracao(_revisao.Id, MotivoDaRecusa, _contexto.Agora);
        Gravar(concessao, SituacaoDaMigracao.Recusada, composicao, new DecisaoHumana(etapa, ResultadoDaMigracao.Recusar, _autoria, envio));
    }

    private void Aguardar(Concessao concessao, ComposicaoDoEnvio composicao, EnvioDeDecisoesDaRevisao? envio) =>
        Gravar(
            concessao,
            SituacaoDaMigracao.AguardandoProtecaoDeDados,
            composicao,
            new DecisaoHumana(EtapaDaMigracao.GestaoDeAcessos, ResultadoDaMigracao.Migrar, _autoria, envio));

    private void Gravar(Concessao concessao, SituacaoDaMigracao situacao, ComposicaoDoEnvio composicao, DecisaoHumana decisao)
    {
        _revisao.Registrar(concessao.Id, situacao, composicao.ExigeProtecaoDeDados, _contexto.Agora);
        _revisao.Acrescentar(DecisaoDaMigracao.Humana(_revisao, concessao.Id, decisao, composicao, _contexto.Agora));
    }
}
