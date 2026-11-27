namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Lê os cadastros de Configuração que as vagas citam: a oferta de curso, a modalidade, a referência demográfica, a condição, o
/// recurso e o tipo de deficiência do atendimento e a base legal do FADA. Cada código distinto vai uma vez ao cadastro, em lote, e o
/// cadastro de que o documento não cita código não é consultado. Cada leitura é do cadastro vivo, e quem a congela é a leitura do documento.
/// </summary>
public sealed class LeituraDosCadastrosDasVagasCitados(
    IOfertaCursoReader ofertas,
    IModalidadeReader modalidades,
    IReferenciaReservaDemograficaReader referenciasDemograficas,
    ICondicaoAtendimentoReader condicoes,
    IRecursoAcessibilidadeReader recursos,
    ITipoDeficienciaReader deficiencias,
    IBaseLegalDoFadaReader basesLegaisDoFada)
{
    internal async Task<CadastrosDasVagasEDasExigenciasCitados> LerAsync(CodigosCitadosPorCadastro codigos, CancellationToken cancellationToken) => new()
    {
        OfertasDeCurso = await OfertasAsync(codigos.OfertasDeCurso, cancellationToken).ConfigureAwait(false),
        Modalidades = await ModalidadesAsync(codigos.Modalidades, cancellationToken).ConfigureAwait(false),
        ReferenciasDemograficas = await ReferenciasDemograficasAsync(codigos.ReferenciasDemograficas, cancellationToken).ConfigureAwait(false),
        CondicoesDeAtendimento = await CondicoesAsync(codigos.CondicoesDeAtendimento, cancellationToken).ConfigureAwait(false),
        RecursosDeAcessibilidade = await RecursosAsync(codigos.RecursosDeAcessibilidade, cancellationToken).ConfigureAwait(false),
        TiposDeDeficiencia = await DeficienciasAsync(codigos.TiposDeDeficiencia, cancellationToken).ConfigureAwait(false),
        BasesLegaisDoFada = await BaseLegalDoFadaAsync(codigos.BaseLegalDoFada, cancellationToken).ConfigureAwait(false),
    };

    private async Task<IReadOnlyDictionary<string, OfertaDeCursoCitada>> OfertasAsync(
        IReadOnlyList<string> codigos, CancellationToken cancellationToken) =>
        codigos.Count == 0
            ? new Dictionary<string, OfertaDeCursoCitada>()
            : (await ofertas.ListarVivasPorCodigosAsync(codigos, cancellationToken).ConfigureAwait(false))
                .ToDictionary(static oferta => oferta.Codigo, CopiaDosCadastrosDasVagas.Oferta, StringComparer.Ordinal);

    private async Task<IReadOnlyDictionary<string, ModalidadeNoCadastro>> ModalidadesAsync(
        IReadOnlyList<string> codigos, CancellationToken cancellationToken) =>
        codigos.Count == 0
            ? new Dictionary<string, ModalidadeNoCadastro>()
            : (await modalidades.ListarVivosPorCodigosAsync(codigos, cancellationToken).ConfigureAwait(false))
                .ToDictionary(static modalidade => modalidade.Codigo, CopiaDosCadastrosDasVagas.Modalidade, StringComparer.Ordinal);

    // O código da referência demográfica é o censo de referência dela, como o cadastro a identifica.
    private async Task<IReadOnlyDictionary<string, ReferenciaReservaDemograficaSnapshot>> ReferenciasDemograficasAsync(
        IReadOnlyList<string> codigos, CancellationToken cancellationToken) =>
        codigos.Count == 0
            ? new Dictionary<string, ReferenciaReservaDemograficaSnapshot>()
            : (await referenciasDemograficas.ListarVivasPorCodigosAsync(codigos, cancellationToken).ConfigureAwait(false))
                .Select(static referencia => (referencia.CensoReferencia, Copia: CopiaDosCadastrosDasVagas.ReferenciaDemografica(referencia)))
                .Where(static referencia => referencia.Copia.IsSuccess)
                .ToDictionary(static referencia => referencia.CensoReferencia, static referencia => referencia.Copia.Value!, StringComparer.Ordinal);

    private async Task<IReadOnlyDictionary<string, OpcaoDeAtendimentoCitada>> CondicoesAsync(
        IReadOnlyList<string> codigos, CancellationToken cancellationToken) =>
        codigos.Count == 0
            ? new Dictionary<string, OpcaoDeAtendimentoCitada>()
            : (await condicoes.ListarVivosPorCodigosAsync(codigos, cancellationToken).ConfigureAwait(false))
                .ToDictionary(static condicao => condicao.Codigo, CopiaDosCadastrosDasVagas.Condicao, StringComparer.Ordinal);

    private async Task<IReadOnlyDictionary<string, OpcaoDeAtendimentoCitada>> RecursosAsync(
        IReadOnlyList<string> codigos, CancellationToken cancellationToken) =>
        codigos.Count == 0
            ? new Dictionary<string, OpcaoDeAtendimentoCitada>()
            : (await recursos.ListarVivosPorCodigosAsync(codigos, cancellationToken).ConfigureAwait(false))
                .ToDictionary(static recurso => recurso.Codigo, CopiaDosCadastrosDasVagas.Recurso, StringComparer.Ordinal);

    private async Task<IReadOnlyDictionary<string, OpcaoDeAtendimentoCitada>> DeficienciasAsync(
        IReadOnlyList<string> codigos, CancellationToken cancellationToken) =>
        codigos.Count == 0
            ? new Dictionary<string, OpcaoDeAtendimentoCitada>()
            : (await deficiencias.ListarVivosPorCodigosAsync(codigos, cancellationToken).ConfigureAwait(false))
                .ToDictionary(static deficiencia => deficiencia.Codigo, CopiaDosCadastrosDasVagas.Deficiencia, StringComparer.Ordinal);

    private async Task<IReadOnlyDictionary<string, BaseLegalCongelada>> BaseLegalDoFadaAsync(
        string? codigo, CancellationToken cancellationToken)
    {
        BaseLegalDoFadaView? baseLegal = codigo is null
            ? null
            : await basesLegaisDoFada.ObterPorCodigoAsync(codigo, cancellationToken).ConfigureAwait(false);

        return baseLegal is null
            ? new Dictionary<string, BaseLegalCongelada>()
            : new Dictionary<string, BaseLegalCongelada>(StringComparer.Ordinal) { [codigo!] = CopiaDosCadastrosDasVagas.BaseLegalDoFada(baseLegal) };
    }
}
