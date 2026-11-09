namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// As conferências de uma versão contra a que está em vigor. Valem na criação, para recusar cedo, e de novo na entrada
/// em vigor, porque a concessão e o nome podem ter mudado enquanto o documento esperava a verificação.
/// </summary>
internal static class ConferenciasDaVersao
{
    /// <summary>
    /// Alcance diferente do da vigente, ou permissão pessoal ou sensível acrescentada a perfil com concessão não
    /// revogada. Sem versão vigente não há com que comparar: é uma nova primeira versão.
    /// </summary>
    public static MotivoDaRecusaDaVersao? ContraAVigente(
        VersaoDoPerfil versao, VersaoDoPerfil? vigente, bool temConcessaoNaoRevogada, ICatalogoDePermissoesConsultavel catalogo)
    {
        if (vigente is null)
        {
            return null;
        }

        if (AlcanceDasPermissoes.De(versao.Codigos(), catalogo) != AlcanceDasPermissoes.De(vigente.Codigos(), catalogo))
        {
            return MotivoDaRecusaDaVersao.AlcanceAlterado;
        }

        return temConcessaoNaoRevogada && Amplia(versao, vigente, catalogo)
            ? MotivoDaRecusaDaVersao.AmpliacaoExigeProtecaoDeDados
            : null;
    }

    /// <summary>As conferências da entrada em vigor: as da criação e a disponibilidade do nome.</summary>
    public static MotivoDaRecusaDaVersao? DaEntradaEmVigor(
        VersaoDoPerfil versao, VersaoDoPerfil? vigente, ContextoDaVersao contexto, ICatalogoDePermissoesConsultavel catalogo) =>
        ContraAVigente(versao, vigente, contexto.TemConcessaoNaoRevogada, catalogo)
        ?? (contexto.NomeEmUsoPorOutroPerfil ? MotivoDaRecusaDaVersao.NomeIndisponivel : null);

    // Cada permissão pessoal ou sensível acrescentada amplia o acesso de quem já tem a concessão, ainda que o perfil já
    // fosse tão sensível quanto ela. Código fora do catálogo conta como sensível.
    private static bool Amplia(VersaoDoPerfil versao, VersaoDoPerfil vigente, ICatalogoDePermissoesConsultavel catalogo) =>
        versao.AcrescimosSobre(vigente).Any(codigo => catalogo.Sensibilidade(codigo) >= ClassificacaoDeProtecao.Pessoal);
}
