namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Os passos de uma decisão sobre uma etapa da aprovação, na ordem que a especificação fixa: o prazo e o recálculo da
/// classe, que gravam mesmo recusando; depois a precedência, a etapa, a segregação e as conferências; por fim o
/// registro da decisão e, se ela completa a aprovação, a troca de vigência.
/// </summary>
internal sealed class PassosDaDecisaoDaVersao
{
    private readonly Perfil _perfil;
    private readonly VersaoDoPerfil? _vigente;
    private readonly VersaoDoPerfil _versao;
    private readonly PedidoDeDecisaoDaVersao _pedida;
    private readonly ContextoDaVersao _contexto;
    private readonly ICatalogoDePermissoesConsultavel _catalogo;

    public PassosDaDecisaoDaVersao(
        Perfil perfil,
        VersaoDoPerfil? vigente,
        VersaoDoPerfil versao,
        PedidoDeDecisaoDaVersao pedida,
        ContextoDaVersao contexto,
        ICatalogoDePermissoesConsultavel catalogo)
    {
        _perfil = perfil;
        _vigente = vigente;
        _versao = versao;
        _pedida = pedida;
        _contexto = contexto;
        _catalogo = catalogo;
    }

    public DesfechoDaVersao Executar()
    {
        if (_versao.Pendente && (Prazo() ?? Recalculo()) is { } gravado)
        {
            return gravado;
        }

        DomainError? recusa = Precedencia() ?? EtapaFora() ?? Segregacao() ?? Conferencias();

        return recusa is null ? Registrar() : DesfechoDaVersao.Recusa(recusa);
    }

    private DiferencaDaVersao Diferenca() =>
        DiferencaDaVersao.Calcular(_versao.Codigos(), _vigente?.Codigos(), _catalogo);

    private DesfechoDaVersao? Prazo()
    {
        if (!_versao.Expirou(_pedida.Instante))
        {
            return null;
        }

        _versao.Expirar();

        return DesfechoDaVersao.RecusaGravada(PerfilErros.VersaoExpirada());
    }

    private DesfechoDaVersao? Recalculo()
    {
        bool exige = Diferenca().ExigeProtecaoDeDados;
        if (exige == _versao.ExigeProtecaoDeDados)
        {
            return null;
        }

        IReadOnlyList<DecisaoDaVersao> descartadas = _versao.DecisoesDoCiclo();

        return DesfechoDaVersao.AprovacoesDescartadas(
            PerfilErros.AprovacoesDescartadas(), _versao.SubirCiclo(exige, _pedida.Instante), descartadas);
    }

    private DomainError? Precedencia() =>
        _versao.Situacao == SituacaoDaVersao.Expirada ? PerfilErros.VersaoExpirada()
        : _versao.DecisaoDaEtapa(_pedida.Etapa) is not null ? PerfilErros.EtapaJaDecidida()
        : _versao.Pendente ? null
        : PerfilErros.VersaoNaoPendente();

    // Depois da gestão, a etapa que falta é a da proteção de dados; se a versão não a exige, a gestão já era a final.
    private DomainError? EtapaFora()
    {
        EtapaDaVersao atual = _versao.AprovadaPelaGestao && Diferenca().ExigeProtecaoDeDados
            ? EtapaDaVersao.ProtecaoDeDados
            : EtapaDaVersao.GestaoDeAcessos;

        return atual == _pedida.Etapa ? null : PerfilErros.EtapaInvalida();
    }

    private DomainError? Segregacao() =>
        SegregacaoDaVersao.Conferir(_versao, _pedida.Autoria.Pessoa, _pedida.Etapa);

    private DomainError? Conferencias() =>
        _pedida.Resultado == ResultadoDaDecisao.Aprovado
            ? ConferenciasDaVersao.DaAprovacao(_versao, _vigente, _contexto, _catalogo)
            : _pedida.Motivo is null ? PerfilErros.MotivoInvalido(MotivosDaVersao.MaxLength) : null;

    private DesfechoDaVersao Registrar()
    {
        DiferencaDaVersao diferenca = Diferenca();
        _versao.RegistrarDecisao(DecisaoDaVersao.Registrar(_versao, _pedida, diferenca));

        if (_pedida.Resultado == ResultadoDaDecisao.Recusado)
        {
            _versao.Recusar();
            return DesfechoDaVersao.Concluido();
        }

        bool completa = _pedida.Etapa == EtapaDaVersao.ProtecaoDeDados || !diferenca.ExigeProtecaoDeDados;

        return DesfechoDaVersao.Concluido(completa ? _perfil.TrocarVigencia(_versao, _pedida.Instante, _catalogo) : null);
    }
}
