namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Restauracao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O ciclo de vida da sessão editorial sobre a configuração de um certame publicado (ADR-0110): abrir,
/// descartar e restaurar a configuração congelada. Decide e devolve; quem atribui o rascunho e o carimbo
/// da restauração é a raiz. O <see cref="ProcessoSeletivo.Status"/> não muda com a sessão: é a existência
/// dela, e não um status, que autoriza mutar um processo publicado.
/// </summary>
internal static class SessaoDeRetificacao
{
    /// <summary>
    /// Abre a sessão sobre um processo publicado. A versão-base é a que o handler elegeu como vigente
    /// (a raiz não conhece a cadeia), e a sessão nasce com os fatos que as versões publicadas coletaram.
    /// </summary>
    /// <param name="identificadorDaVersaoBase">O identificador legível constante da versão-base, quando ela o tem.</param>
    /// <param name="abertoPorSub">Sub do usuário autenticado (via <c>IUserContext</c>, nunca input do comando).</param>
    /// <param name="abertoEm">Instante lido do relógio injetado (ADR-0068).</param>
    internal static Result<RascunhoRetificacao> Abrir(
        EstadoDaConfiguracao estado,
        string motivo,
        VersaoConfiguracao versaoBase,
        IdentificadorLegivel? identificadorDaVersaoBase,
        FatosDasVersoesPublicadas fatosDasVersoesPublicadas,
        string abertoPorSub,
        DateTimeOffset abertoEm)
    {
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentNullException.ThrowIfNull(versaoBase);
        ArgumentNullException.ThrowIfNull(fatosDasVersoesPublicadas);

        DomainError? recusa = estado.Status != StatusProcesso.Publicado
            ? new DomainError(
                "ProcessoSeletivo.TransicaoInvalida",
                $"Só é possível retificar um processo publicado — status atual: {estado.Status}.")
            : RecusaDaVersaoDeOutroProcesso(estado, versaoBase)
                ?? (estado.Rascunho is not null ? GuardaDeEdicao.RetificacaoJaAberta() : null);

        return recusa is not null
            ? Result<RascunhoRetificacao>.Failure(recusa)
            : RascunhoRetificacao.Criar(
                estado.ProcessoId, motivo, versaoBase, identificadorDaVersaoBase is not null, fatosDasVersoesPublicadas, abertoPorSub, abertoEm);
    }

    /// <summary>
    /// O que impede descartar a sessão, ou nulo. O descarte exige o carimbo de que a configuração
    /// congelada foi reposta e provada, e que ele é o da versão-base desta sessão: um teste textual de
    /// callers só diz que ninguém descarta sem repor, e o carimbo torna isso irrepresentável. Descartar
    /// sem repor deixaria o certame servindo, em silêncio, uma configuração editada que nunca foi publicada.
    /// </summary>
    internal static DomainError? RecusaDoDescarte(
        EstadoDaConfiguracao estado, Guid? versaoRestaurada, PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentNullException.ThrowIfNull(precondicao);

        if (GuardaDeEdicao.PendenciaDaSessao(estado.Status, estado.Rascunho, precondicao) is { } pendencia)
        {
            return pendencia;
        }

        return versaoRestaurada switch
        {
            null => new DomainError(
                "ProcessoSeletivo.DescarteSemRestauracao",
                "A sessão editorial não pode ser encerrada sem que a configuração congelada seja reposta — "
                + "encerrá-la agora deixaria o certame servindo a configuração editada como se ela tivesse sido publicada."),
            { } restaurada when restaurada != estado.Rascunho!.VersaoBaseId => new DomainError(
                "ProcessoSeletivo.DescarteComVersaoErrada",
                "A configuração reposta não é a da versão sobre a qual esta retificação foi aberta."),
            _ => null,
        };
    }

    /// <summary>
    /// O que impede repor a configuração congelada, ou nulo. A reposição exige o processo publicado, a
    /// versão como credencial e o grafo válido inteiro, como ele ficará: uma sequência de edições
    /// validaria referências cruzadas contra o estado corrente e recusaria um grafo meio-construído.
    /// </summary>
    internal static DomainError? RecusaDaRestauracao(EstadoDaConfiguracao estado, VersaoConfiguracao versao, GrafoConfiguracao grafo)
    {
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentNullException.ThrowIfNull(versao);
        ArgumentNullException.ThrowIfNull(grafo);

        if (estado.Status != StatusProcesso.Publicado)
        {
            return new DomainError(
                "ProcessoSeletivo.RestauracaoForaDePublicado",
                $"Só é possível restaurar a configuração congelada de um processo publicado — status atual: {estado.Status}.");
        }

        // A cadeia de versões não atravessa certames: repor a configuração de outro processo
        // sobrescreveria este com uma que nunca foi dele, e o envelope seguinte congelaria a troca.
        return RecusaDaVersaoDeOutroProcesso(estado, versao) ?? ValidacaoDoGrafo.Validar(grafo);
    }

    private static DomainError? RecusaDaVersaoDeOutroProcesso(EstadoDaConfiguracao estado, VersaoConfiguracao versao) =>
        versao.ProcessoSeletivoId != estado.ProcessoId
            ? new DomainError(
                "VersaoConfiguracao.VersaoDeOutroProcesso",
                "A versão de configuração informada pertence a outro Processo Seletivo.")
            : null;
}
