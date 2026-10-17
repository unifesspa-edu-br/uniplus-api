namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Reúne, para os gates de publicação, o que Publicações sabe sobre os tipos de ato únicos por
/// objeto que o processo cita (ADR-0056 — leitura cross-módulo), e recusa o processo que declara
/// o mesmo tipo único mais de uma vez.
/// </summary>
/// <remarks>
/// <para>
/// A leitura é I/O e por isso fica aqui, e não no agregado (ADR-0042). Pergunta-se ao catálogo
/// vigente, e não a uma lista de códigos: <c>unico_por_objeto</c> é editável, e o que o catálogo
/// disser no momento de publicar é o que o registro do ato aplicará. Os códigos vêm do próprio
/// agregado (<see cref="ProcessoSeletivo.DeclaracoesDeAtos"/>) e vão numa só leitura. Tipo sem
/// versão vigente não entra: quem o recusa é a conferência do tipo declarado, não esta.
/// </para>
/// <para>
/// <b>O ato da operação não é relido.</b> O tipo dele já foi conferido na data de publicação do
/// ato (<see cref="ConferenciaDoTipoDeAto"/>) e é com essa leitura que o registro decide; reler
/// o catálogo em outra data abriria um cenário em que o gate enxerga uma unicidade e o registro,
/// outra. As vagas que os atos já registrados do processo ocupam vêm de Publicações pelo
/// contrato em processo, sem HTTP.
/// </para>
/// </remarks>
internal static class ResolucaoDosTiposDeAtoUnicos
{
    /// <summary>
    /// Resolve os tipos únicos da operação e recusa a pendência, tudo num ponto só: os handlers
    /// que congelam versão chamam isto logo depois do cronograma, antes de canonicalizar, na
    /// ordem da raiz.
    /// </summary>
    /// <remarks>
    /// Os <paramref name="atosDaLinhagem"/> são os que a conferência da vaga já obteve do
    /// repositório. O que ela perguntou a Publicações foi outra coisa (se há ato de outra
    /// linhagem), então a pergunta daqui — o tipo de cada ato da linhagem — é uma leitura
    /// própria, e fundi-las só acoplaria dois contratos.
    /// </remarks>
    public static async Task<Result<TiposDeAtoUnicosPorObjeto>> ConferirAsync(
        ITipoAtoPublicadoReader tipoAtoReader,
        IVagaDeLinhagemReader vagaDeLinhagemReader,
        ProcessoSeletivo processo,
        TipoAtoPublicadoView tipoDoAtoDaOperacao,
        IReadOnlyList<Guid> atosDaLinhagem,
        TimeProvider relogio,
        TimeZoneInfo fusoInstitucional,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(vagaDeLinhagemReader);
        ArgumentNullException.ThrowIfNull(tipoDoAtoDaOperacao);
        ArgumentNullException.ThrowIfNull(atosDaLinhagem);

        IReadOnlyList<string> ocupados = atosDaLinhagem.Count == 0
            ? []
            : await vagaDeLinhagemReader
                .ListarTiposDosAtosAsync(atosDaLinhagem, cancellationToken)
                .ConfigureAwait(false);

        Dictionary<string, string> unicos = await LerDoCatalogoAsync(
            tipoAtoReader, processo, ocupados, tipoDoAtoDaOperacao.Codigo, Hoje(relogio, fusoInstitucional), cancellationToken)
            .ConfigureAwait(false);
        if (tipoDoAtoDaOperacao.UnicoPorObjeto)
        {
            unicos[tipoDoAtoDaOperacao.Codigo] = tipoDoAtoDaOperacao.Nome;
        }

        TiposDeAtoUnicosPorObjeto tipos = TiposDeAtoUnicosPorObjeto.ParaOperacao(unicos, ocupados, tipoDoAtoDaOperacao.Codigo);

        return processo.PendenciaDoTipoDeAtoUnico(tipos) is { } pendencia
            ? Result<TiposDeAtoUnicosPorObjeto>.Failure(pendencia)
            : Result<TiposDeAtoUnicosPorObjeto>.Success(tipos);
    }

    /// <summary>Para o checklist, que avalia o processo antes de haver ato.</summary>
    public static async Task<TiposDeAtoUnicosPorObjeto> ParaChecklistAsync(
        ITipoAtoPublicadoReader reader,
        ProcessoSeletivo processo,
        DateOnly dataVigencia,
        CancellationToken cancellationToken) =>
        TiposDeAtoUnicosPorObjeto.ParaChecklist(
            await LerDoCatalogoAsync(reader, processo, [], null, dataVigencia, cancellationToken).ConfigureAwait(false));

    /// <summary>O dia civil de hoje no fuso institucional, a data em que o catálogo é consultado.</summary>
    internal static DateOnly Hoje(TimeProvider relogio, TimeZoneInfo? fusoInstitucional)
    {
        ArgumentNullException.ThrowIfNull(relogio);

        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(relogio.GetUtcNow(), fusoInstitucional ?? TimeZoneInfo.Utc).DateTime);
    }

    private static async Task<Dictionary<string, string>> LerDoCatalogoAsync(
        ITipoAtoPublicadoReader reader,
        ProcessoSeletivo processo,
        IEnumerable<string> codigosOcupados,
        string? codigoJaConferido,
        DateOnly dataVigencia,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(processo);

        string[] codigos =
        [
            .. processo.DeclaracoesDeAtos()
                .Select(static declaracao => declaracao.AtoCodigo)
                .Concat(codigosOcupados)
                .Where(codigo => !string.Equals(codigo, codigoJaConferido, StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal),
        ];

        if (codigos.Length == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        IReadOnlyList<TipoAtoPublicadoView> vigentes = await reader
            .ListarVigentesAsync(codigos, dataVigencia, cancellationToken)
            .ConfigureAwait(false);

        return vigentes
            .Where(static tipo => tipo.UnicoPorObjeto)
            .GroupBy(static tipo => tipo.Codigo, StringComparer.Ordinal)
            .ToDictionary(static grupo => grupo.Key, static grupo => grupo.First().Nome, StringComparer.Ordinal);
    }
}
