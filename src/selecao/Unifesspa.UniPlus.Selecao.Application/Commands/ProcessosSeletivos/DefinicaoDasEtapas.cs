namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.MotivosDeDecisao;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Define as etapas do processo em duas passadas (ADR-0125). A primeira confere o payload inteiro —
/// forma, caráter, avaliação — sem mutar nada; só se ela vier limpa a segunda reconcilia as etapas, declara
/// os filhos de cada uma, entrega a coleção ao agregado e leva junto os motivos pendentes das etapas que saem
/// (<see cref="MotivosDosLugaresQueSaem"/>). A segunda passada muta instâncias rastreadas, e por isso quem chama
/// descarta as alterações quando ela recusa: sem o descarte o <c>SaveChanges</c> persistiria a mutação parcial mesmo
/// com a coleção recusada.
/// </summary>
public sealed class DefinicaoDasEtapas(
    ITipoEtapaReader tipoEtapaReader,
    ITipoBancaReader tipoBancaReader,
    IRegraCatalogoReader regraCatalogoReader,
    ITipoAtoPublicadoReader tipoAtoPublicadoReader,
    IProcessoSeletivoRepository processoSeletivoRepository,
    IRegistroCodecsEnvelope registroCodecs,
    MotivosDosLugaresQueSaem motivosDosLugaresQueSaem)
{
    internal async Task<Result> DefinirAsync(
        ProcessoSeletivo processo, DefinirEtapasCommand command, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        Result payload = EscritaDasEtapasDoProcesso.Conferir(command.Etapas);
        if (payload.IsFailure)
        {
            return payload;
        }

        LugaresDaDecisao lugaresAntes = LugaresDaDecisao.Do(processo);
        Result substituida = await SubstituirAsync(NovaEdicao(processo, command, agora), cancellationToken).ConfigureAwait(false);
        return substituida.IsFailure
            ? substituida
            : await motivosDosLugaresQueSaem.RetirarAsync(processo, lugaresAntes, cancellationToken).ConfigureAwait(false);
    }

    private EdicaoDasEtapas NovaEdicao(ProcessoSeletivo processo, DefinirEtapasCommand command, DateTimeOffset agora) =>
        new(
            processo,
            command,
            new CadastrosDasEtapas(tipoEtapaReader, tipoBancaReader, regraCatalogoReader),
            new AtosConsultadosNaEdicao(tipoAtoPublicadoReader, processoSeletivoRepository, registroCodecs, command.ProcessoSeletivoId, agora));

    private static async Task<Result> SubstituirAsync(EdicaoDasEtapas edicao, CancellationToken cancellationToken)
    {
        Result<List<EtapaConferida>> conferidas = await ConferirContraOsCadastrosAsync(edicao, cancellationToken).ConfigureAwait(false);
        return conferidas.IsFailure
            ? Result.ValidationFailure(conferidas.Errors)
            : await AplicarAsync(edicao, conferidas.Value!, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<List<EtapaConferida>>> ConferirContraOsCadastrosAsync(
        EdicaoDasEtapas edicao, CancellationToken cancellationToken)
    {
        Dictionary<Guid, EtapaProcesso> existentes = edicao.Processo.Etapas.ToDictionary(e => e.Id);
        List<EtapaConferida> declaradas = [.. edicao.Command.Etapas.Select(etapa => EtapaConferida.De(etapa, existentes))];
        Result<List<EtapaConferida>> comCarater = await new ConferenciaDoCaraterDasEtapas(edicao.Cadastros.TiposDeEtapa)
            .ConferirAsync(declaradas, cancellationToken).ConfigureAwait(false);
        return comCarater.IsFailure
            ? comCarater
            : await new ConferenciaDaAvaliacaoDasEtapas(edicao.Cadastros.Regras)
                .ConferirAsync(comCarater.Value!, cancellationToken).ConfigureAwait(false);
    }

    // O fechamento do ciclo acumula uma violação por matéria aberta, cada uma com o campo que a localiza; propagar o
    // lote preserva esses campos.
    private static async Task<Result> AplicarAsync(
        EdicaoDasEtapas edicao, IReadOnlyList<EtapaConferida> conferidas, CancellationToken cancellationToken)
    {
        Result<List<EtapaProcesso>> etapas = MontagemDasEtapas.Montar(conferidas);
        if (etapas.IsFailure)
        {
            return Result.ValidationFailure(etapas.Errors);
        }

        Result declaradas = await DeclararFilhosAsync(edicao, etapas.Value!, cancellationToken).ConfigureAwait(false);
        if (declaradas.IsFailure)
        {
            return declaradas;
        }

        Result definidas = edicao.Processo.DefinirEtapas(etapas.Value!, edicao.Command.Precondicao);
        return definidas.IsFailure ? Result.ValidationFailure(definidas.Errors) : definidas;
    }

    private static async Task<Result> DeclararFilhosAsync(
        EdicaoDasEtapas edicao, List<EtapaProcesso> etapas, CancellationToken cancellationToken)
    {
        DeclaracaoDosFilhosDasEtapas declaracao = DeclaracaoDosFilhos(edicao, etapas);
        List<FieldError> recusas = [];
        for (int indice = 0; indice < etapas.Count; indice++)
        {
            IReadOnlyList<FieldError> doFilho = await declaracao
                .DeclararAsync(etapas[indice], edicao.Command.Etapas[indice], cancellationToken).ConfigureAwait(false);
            recusas.AddRange(EscritaDasEtapasDoProcesso.ComIndice(doFilho, indice));
        }

        return recusas.Count > 0 ? Result.ValidationFailure(recusas) : Result.Success();
    }

    private static DeclaracaoDosFilhosDasEtapas DeclaracaoDosFilhos(EdicaoDasEtapas edicao, List<EtapaProcesso> etapas) =>
        new(
            new DeclaracaoDosProdutosDaEtapa(edicao.Atos),
            new DeclaracaoDasBancasDaEtapa(edicao.Cadastros.TiposDeBanca),
            new DeclaracaoDosRecursosDaEtapa(edicao.Atos, edicao.Cadastros.Regras, etapas.Exists(static e => e.DeclaraNotaDoEnem)));

    private sealed record EdicaoDasEtapas(
        ProcessoSeletivo Processo, DefinirEtapasCommand Command, CadastrosDasEtapas Cadastros, AtosConsultadosNaEdicao Atos);
}
