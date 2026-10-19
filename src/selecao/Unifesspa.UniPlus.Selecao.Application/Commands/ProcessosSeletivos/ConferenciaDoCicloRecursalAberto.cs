namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Enums;
using Domain.Errors;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

using Queries.ProdutosPrevistos;

using Services;

/// <summary>
/// Recusa a retificação cuja configuração nova desfaz um ciclo recursal que o preliminar já publicado
/// abriu e que o definitivo ainda não fechou (UNI-REQ-0157).
/// </summary>
/// <remarks>
/// <para>
/// A versão nova é conferida por dentro: ela precisa ser coerente sozinha. Esta conferência a compara
/// com o que já saiu. A matéria tem o ciclo aberto quando o preliminar foi publicado e nenhum dos
/// definitivos que o fecham na versão publicada foi; enquanto está aberto, o preliminar e esses
/// definitivos precisam continuar no cronograma novo, o definitivo ainda fechando o ciclo do
/// preliminar.
/// </para>
/// <para>
/// O pareamento entre preliminar e definitivo vem de <see cref="PreliminaresDaMateria"/>, que o mesmo
/// cronograma consulta para liberar o definitivo, e a publicação do item vem do registro local dos atos,
/// que enxerga também o ato ainda pendente de confirmação. O item se identifica, na versão publicada e no cronograma
/// novo, pela identidade estável que o ato guarda (<see cref="IdentidadeDoItemDoEdital"/>): lugar, tipo de
/// ato e papel. O <c>Id</c> do produto muda sem mudar o ciclo quando o descarte de uma sessão recria os
/// produtos, e por isso não serve.
/// Mover o definitivo para outro lugar muda o par, e a troca da fase concluinte muda quem fecha o preliminar.
/// </para>
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
        if (atos.All(static a => a.ItemDoEdital is null))
        {
            return Result.Success();
        }

        Result<EnvelopeReidratado> publicada = registroCodecs.Reidratar(versaoPublicada);
        if (publicada.IsFailure)
        {
            return Result.Failure(publicada.Error!);
        }

        return Conferir(
            publicada.Value!.Grafo.CronogramaFases,
            publicada.Value.Grafo.Etapas,
            processo.CronogramaFases,
            processo.Etapas,
            CadeiasDosProdutosPrevistos.De(atos));
    }

    internal static Result Conferir(
        IReadOnlyCollection<FaseCronograma> fasesPublicadas,
        IReadOnlyCollection<EtapaProcesso> etapasPublicadas,
        IReadOnlyCollection<FaseCronograma> fasesNovas,
        IReadOnlyCollection<EtapaProcesso> etapasNovas,
        CadeiasDosProdutosPrevistos publicacoes)
    {
        List<string> materiasDesfeitas = [];
        Dictionary<Guid, IdentidadeDoItemDoEdital> publicados = IdentidadeDoItemDoEdital.Dos(fasesPublicadas, etapasPublicadas);
        Dictionary<Guid, IdentidadeDoItemDoEdital> novos = IdentidadeDoItemDoEdital.Dos(fasesNovas, etapasNovas);

        foreach ((Guid preliminarId, IdentidadeDoItemDoEdital item) in publicados.Where(static i => i.Value.Papel == PapelProdutoFase.Preliminar))
        {
            if (publicacoes.PrimeiraPublicacaoDe(item) is not { } preliminar)
            {
                continue;
            }

            IReadOnlyList<Guid> definitivos = PreliminaresDaMateria.DefinitivosQueFecham(fasesPublicadas, etapasPublicadas, preliminarId);
            if (definitivos.Count == 0 || definitivos.Any(d => publicacoes.PrimeiraPublicacaoDe(publicados[d]) is not null))
            {
                continue;
            }

            if (!MantemOCiclo(item, definitivos.Select(d => publicados[d]), novos, fasesNovas, etapasNovas)
                && !materiasDesfeitas.Contains(preliminar.TipoNome))
            {
                materiasDesfeitas.Add(preliminar.TipoNome);
            }
        }

        return materiasDesfeitas.Count == 0
            ? Result.Success()
            : Result.Failure(new DomainError(
                ProcessoSeletivoErrorCodes.RetificacaoDesfazCicloRecursalAberto,
                $"A retificação desfaz o ciclo de recurso de {Materias(materiasDesfeitas)}: o resultado preliminar já foi publicado, o definitivo ainda não, "
                + "e a configuração nova retira ou muda de ciclo um dos dois. Publique o definitivo antes de retificar, ou mantenha o preliminar e o definitivo da matéria no cronograma."));
    }

    private static string Materias(List<string> nomes) =>
        nomes.Count == 1
            ? $"a matéria '{nomes[0]}'"
            : $"as matérias {string.Join(", ", nomes.Select(static n => $"'{n}'"))}";

    private static bool MantemOCiclo(
        IdentidadeDoItemDoEdital preliminar,
        IEnumerable<IdentidadeDoItemDoEdital> definitivos,
        Dictionary<Guid, IdentidadeDoItemDoEdital> novos,
        IReadOnlyCollection<FaseCronograma> fasesNovas,
        IReadOnlyCollection<EtapaProcesso> etapasNovas)
    {
        Guid? preliminarNovo = novos.Where(i => i.Value == preliminar).Select(static i => (Guid?)i.Key).FirstOrDefault();
        if (preliminarNovo is null)
        {
            return false;
        }

        HashSet<IdentidadeDoItemDoEdital> quemFecha =
        [
            .. PreliminaresDaMateria.DefinitivosQueFecham(fasesNovas, etapasNovas, preliminarNovo.Value).Select(d => novos[d]),
        ];

        return definitivos.All(quemFecha.Contains);
    }
}
