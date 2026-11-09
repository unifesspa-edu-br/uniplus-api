namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A segregação de funções da aprovação de uma versão: quem a criou não a decide e a mesma pessoa não decide as duas
/// etapas do ciclo. Ter concessão do perfil não impede nada: a decisão só registra o fato.
/// </summary>
internal static class SegregacaoDaVersao
{
    public static DomainError? Conferir(VersaoDoPerfil versao, Identidade decisor, EtapaDaVersao etapa)
    {
        if (versao.CriadaPor == decisor)
        {
            return PerfilErros.DecisorEAutor();
        }

        return etapa == EtapaDaVersao.ProtecaoDeDados && versao.DecisaoDaEtapa(EtapaDaVersao.GestaoDeAcessos)?.Decisor == decisor
            ? PerfilErros.MesmoDecisorNasDuasEtapas()
            : null;
    }
}
