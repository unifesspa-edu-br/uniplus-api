namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Abstractions;

using Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;
using Domain.MotivosDeDecisao;
using Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Domain.ValueObjects;

using Services;

/// <summary>
/// O que o painel de conformidade lê dos cadastros vivos além do catálogo, uma vez e como valor: o calendário vigente, as regras
/// citadas e os corretores delas, os tipos de ato únicos com os atos criadores do processo, os motivos escolhidos com o número
/// da versão que nasceria agora e a precedência entre fases conferida contra as fases da versão publicada. É a mesma leitura
/// que a publicação e a retificação fazem, sem bloqueio nem versão, no instante da consulta: o painel é previsão e a operação
/// confere de novo.
/// </summary>
public sealed class LeituraDosCadastrosDoPainel(
    IProcessoSeletivoRepository processoSeletivoRepository,
    LeituraDoContextoDeContagemDePrazos contextos,
    LeituraDasRegrasCitadasNaConfiguracao leituraDasRegras,
    ResolucaoDosTiposDeAtoUnicos tiposDeAto,
    LeituraDasPrecedenciasDosCadastros precedencias,
    IMotivoDoProcessoRepository motivos)
{
    internal async Task<LeiturasDoPainel> LerAsync(ProcessoSeletivo processo, VersaoPublicadaNoPainel publicada, CancellationToken cancellationToken)
    {
        ContextoDeContagemDePrazos contexto = await contextos.LerAsync(cancellationToken).ConfigureAwait(false);
        RegrasCitadasNaConfiguracao regras = await leituraDasRegras.LerAsync(processo, cancellationToken).ConfigureAwait(false);
        TiposDeAtoUnicosPorObjeto tipos = await LerOsTiposDeAtoUnicosAsync(processo, contexto, publicada, cancellationToken).ConfigureAwait(false);
        MotivosEscolhidos escolhidos = await LerOsMotivosEscolhidosAsync(processo.Id, publicada, cancellationToken).ConfigureAwait(false);
        PrecedenciasDosCadastros lidas = await precedencias
            .LerAsync(publicada.Fases, processo.CronogramaFases, cancellationToken)
            .ConfigureAwait(false);

        return new LeiturasDoPainel(contexto, regras, tipos, escolhidos, lidas);
    }

    private async Task<TiposDeAtoUnicosPorObjeto> LerOsTiposDeAtoUnicosAsync(
        ProcessoSeletivo processo, ContextoDeContagemDePrazos contexto, VersaoPublicadaNoPainel publicada, CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> atosDaLinhagem = await processoSeletivoRepository
            .ObterAtosCriadoresAsync(processo.Id, cancellationToken)
            .ConfigureAwait(false);

        return await tiposDeAto
            .ParaChecklistAsync(processo, atosDaLinhagem, contexto.FusoInstitucional, publicada.TiposCongelados, cancellationToken)
            .ConfigureAwait(false);
    }

    // Os motivos entram na versão que nasce, a mesma que o congelamento confere.
    private async Task<MotivosEscolhidos> LerOsMotivosEscolhidosAsync(Guid processoId, VersaoPublicadaNoPainel publicada, CancellationToken cancellationToken) =>
        new(await motivos.ListarEscolhaAtualComOrigemAsync(processoId, cancellationToken).ConfigureAwait(false), publicada.NumeroDaVersaoQueNasce);
}

/// <summary>Os cadastros que o painel lê além do catálogo e da versão vigente.</summary>
internal sealed record LeiturasDoPainel(
    ContextoDeContagemDePrazos Contexto,
    RegrasCitadasNaConfiguracao Regras,
    TiposDeAtoUnicosPorObjeto TiposDeAtoUnicos,
    MotivosEscolhidos MotivosEscolhidos,
    PrecedenciasDosCadastros Precedencias);
