namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ProcessosSeletivos.FasesEEtapas;
using Domain.ValueObjects;

using Kernel.Results;

using Queries.ProdutosPrevistos;

using ConferenciaDoDominio = Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao.ConferenciaDoCicloRecursalAberto;

/// <summary>
/// Lê os atos publicados do processo e a versão publicada vigente e entrega ao domínio o que ele precisa para
/// recusar a retificação que desfaz um ciclo recursal aberto (UNI-REQ-0157).
/// </summary>
/// <remarks>
/// A regra vive em <see cref="ConferenciaDoDominio"/>. A publicação do item vem do registro local dos atos, que
/// enxerga também o ato ainda pendente de confirmação, e chega ao domínio como o conjunto das identidades cuja
/// primeira publicação existe.
/// </remarks>
internal static class ConferenciaDoCicloRecursalAberto
{
    public static async Task<Result> ConferirAsync(
        IAtoDoProcessoRepository atoDoProcessoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        ProcessoSeletivo processo,
        VersaoConfiguracao versaoPublicada,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(atoDoProcessoRepository);
        ArgumentNullException.ThrowIfNull(registroCodecs);
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(versaoPublicada);

        IReadOnlyList<AtoDoProcesso> atos = await atoDoProcessoRepository
            .ListarDoProcessoAsync(processo.Id, cancellationToken)
            .ConfigureAwait(false);

        // Sem item do edital publicado não há ciclo aberto, e a versão publicada nem precisa ser lida.
        return atos.All(static a => a.ItemDoEdital is null)
            ? Result.Success()
            : ConferirContraAVersaoPublicada(registroCodecs, processo, versaoPublicada, atos);
    }

    private static Result ConferirContraAVersaoPublicada(
        IRegistroCodecsEnvelope registroCodecs,
        ProcessoSeletivo processo,
        VersaoConfiguracao versaoPublicada,
        IReadOnlyList<AtoDoProcesso> atos)
    {
        Result<EnvelopeReidratado> publicada = registroCodecs.Reidratar(versaoPublicada);
        if (publicada.IsFailure)
        {
            return Result.Failure(publicada.Error!);
        }

        GrafoConfiguracao grafo = publicada.Value!.Grafo;
        IReadOnlyList<FieldError> recusas = ConferenciaDoDominio.Conferir(
            grafo.CronogramaFases, grafo.Etapas, processo.CronogramaFases, processo.Etapas, ItensPublicados(atos, grafo));

        return recusas.Count == 0 ? Result.Success() : Result.ValidationFailure(PonteiroNoDocumento.Converter(recusas, OrigemNoDocumento.Identidade));
    }

    private static HashSet<IdentidadeDoItemDoEdital> ItensPublicados(IReadOnlyList<AtoDoProcesso> atos, GrafoConfiguracao publicado)
    {
        CadeiasDosProdutosPrevistos cadeias = CadeiasDosProdutosPrevistos.De(atos);

        return
        [
            .. IdentidadeDoItemDoEdital.Dos(publicado.CronogramaFases, publicado.Etapas).Values
                .Where(item => cadeias.PrimeiraPublicacaoDe(item) is not null),
        ];
    }
}
