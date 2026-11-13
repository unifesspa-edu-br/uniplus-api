namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Define as etapas de um processo já travado para edição, em duas passadas (ADR-0125). A primeira confere o
/// payload inteiro (forma, caráter, avaliação) sem mutar nada; só se ela vier limpa a segunda reconcilia as
/// etapas e as entrega ao agregado (ver <see cref="SubstituicaoDasEtapas"/>). O instante vem de quem chama, lido
/// uma vez por operação (ADR-0068). A leitura memoizada dos cadastros nasce a cada definição, e por isso o
/// serviço não tem estado entre elas.
/// </summary>
public sealed class DefinicaoDasEtapas(
    ITipoEtapaReader tipoEtapaReader,
    ITipoBancaReader tipoBancaReader,
    IRegraCatalogoReader regraCatalogoReader,
    SubstituicaoDasEtapas substituicao)
{
    internal async Task<Result> DefinirAsync(
        ProcessoSeletivo processo, DefinirEtapasCommand command, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        Result payload = EscritaDasEtapasDoProcesso.Conferir(command.Etapas);
        if (payload.IsFailure)
        {
            return payload;
        }

        CadastrosDasEtapas cadastros = new(tipoEtapaReader, tipoBancaReader, regraCatalogoReader);
        Result<List<EtapaConferida>> conferidas = await ConferirContraOsCadastrosAsync(processo, command, cadastros, cancellationToken).ConfigureAwait(false);
        return conferidas.IsFailure
            ? Result.ValidationFailure(conferidas.Errors)
            : await substituicao.SubstituirAsync(processo, command, conferidas.Value!, cadastros, agora, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<List<EtapaConferida>>> ConferirContraOsCadastrosAsync(
        ProcessoSeletivo processo, DefinirEtapasCommand command, CadastrosDasEtapas cadastros, CancellationToken cancellationToken)
    {
        Dictionary<Guid, EtapaProcesso> existentes = processo.Etapas.ToDictionary(e => e.Id);
        List<EtapaConferida> declaradas = [.. command.Etapas.Select(etapa => EtapaConferida.De(etapa, existentes))];
        Result<List<EtapaConferida>> comCarater = await new ConferenciaDoCaraterDasEtapas(cadastros.TiposDeEtapa)
            .ConferirAsync(declaradas, cancellationToken).ConfigureAwait(false);
        return comCarater.IsFailure
            ? comCarater
            : await new ConferenciaDaAvaliacaoDasEtapas(cadastros.Regras)
                .ConferirAsync(comCarater.Value!, cancellationToken).ConfigureAwait(false);
    }
}
