namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;

/// <summary>
/// Reúne, para os gates de publicação e para o checklist, o que Publicações sabe sobre os tipos de
/// ato únicos por objeto que o processo cita (ADR-0056 — leitura cross-módulo) e os tipos dos atos
/// que já criaram versão, e recusa o processo que declara o mesmo tipo único mais de uma vez.
/// </summary>
/// <remarks>
/// <para>
/// A leitura é I/O e por isso fica aqui, e não no agregado (ADR-0042). Os códigos vêm do próprio
/// agregado (<see cref="ProcessoSeletivo.DeclaracoesDeAtos"/>). A classificação de cada um vem de
/// dois lugares, conforme ele já tenha sido congelado ou não: na publicação de abertura, e para o
/// tipo declarado pela primeira vez numa retificação, pergunta-se ao catálogo vigente, numa só
/// leitura; o tipo que a versão vigente já congelou mantém essa classificação, e o catálogo não é
/// relido para ele, porque mudar o tipo no catálogo depois da publicação não alcança o processo já
/// publicado (UNI-REQ-0163). Tipo declarado sem versão vigente no catálogo não entra: quem o recusa
/// é o congelamento da classificação (<see cref="CongelamentoDosTiposDeAto"/>), não esta.
/// </para>
/// <para>
/// <b>O ato da operação não é relido.</b> O tipo dele já foi conferido no dia da publicação do
/// ato (<see cref="ConferenciaDoTipoDeAto"/>) e é com essa leitura que o registro decide; reler
/// o catálogo em outra data abriria um cenário em que o gate enxerga uma unicidade e o registro,
/// outra.
/// </para>
/// <para>
/// <b>As vagas ocupadas vêm do registro local dos atos.</b> O registro do ato da versão é gravado na
/// mesma transação da versão e da mensagem durável (ADR-0108), enquanto Publicações só conhece o ato
/// depois de consumi-la. Perguntar a Publicações deixaria uma janela, entre o commit e o consumo, em
/// que o tipo do ato anterior não aparece como ocupado. Os gates e o checklist leem do mesmo lugar,
/// para que concordem.
/// </para>
/// </remarks>
public sealed class ResolucaoDosTiposDeAtoUnicos(
    ITipoAtoPublicadoReader tipoAtoReader,
    IAtoDoProcessoRepository atoDoProcessoRepository,
    TimeProvider relogio)
{
    /// <summary>
    /// Resolve os tipos únicos da operação e recusa a pendência, tudo num ponto só: o congelamento da versão chama
    /// isto logo depois do cronograma, antes de canonicalizar, na ordem da raiz. O catálogo é consultado no
    /// <paramref name="diaDaPublicacao"/>, o dia civil do instante em que a operação publica.
    /// </summary>
    /// <param name="jaCongelados">
    /// A classificação que a versão vigente já congelou: o tipo que consta nela mantém a classificação
    /// congelada, e só o tipo declarado pela primeira vez é lido do catálogo. Vazio na publicação de
    /// abertura, que não tem versão anterior.
    /// </param>
    /// <remarks>
    /// Os <paramref name="atosDaLinhagem"/> são os que a conferência da vaga já obteve do
    /// repositório. O que ela perguntou a Publicações foi outra coisa (se há ato de outra
    /// linhagem), então a pergunta daqui — o tipo de cada ato da linhagem — é uma leitura
    /// própria, e fundi-las só acoplaria dois contratos.
    /// </remarks>
    internal async Task<Result<TiposDeAtoUnicosPorObjeto>> ConferirAsync(
        ProcessoSeletivo processo,
        TipoAtoPublicadoView tipoDoAtoDaOperacao,
        IReadOnlyList<Guid> atosDaLinhagem,
        DateOnly diaDaPublicacao,
        IReadOnlyDictionary<string, TipoDeAtoCongelado> jaCongelados,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> ocupados = await TiposOcupadosAsync(atosDaLinhagem, cancellationToken).ConfigureAwait(false);
        LeituraDoCatalogo leitura = await LerDoCatalogoAsync(
            processo, ocupados, tipoDoAtoDaOperacao.Codigo, diaDaPublicacao, jaCongelados, cancellationToken)
            .ConfigureAwait(false);
        AcrescentarOTipoDoAto(leitura, tipoDoAtoDaOperacao, jaCongelados);

        TiposDeAtoUnicosPorObjeto tipos = TiposDeAtoUnicosPorObjeto.ParaOperacao(
            leitura.Unicos, ocupados, tipoDoAtoDaOperacao.Codigo, leitura.Classificacoes,
            tipoDoAtoDaOperacao.UnicoPorObjeto, tipoDoAtoDaOperacao.Nome);

        return TiposDeAtoDoCronograma.PendenciaDoTipoDeAtoUnico(EstadoDaConfiguracao.De(processo), tipos) is { } pendencia
            ? Result<TiposDeAtoUnicosPorObjeto>.Failure(pendencia)
            : Result<TiposDeAtoUnicosPorObjeto>.Success(tipos);
    }

    /// <summary>
    /// Para o checklist, que avalia o processo antes de haver ato da operação: conta as vagas que
    /// os atos da linhagem já ocupam, como os gates que congelam versão. Sem fuso reconhecido, o dia é o do UTC.
    /// </summary>
    internal async Task<TiposDeAtoUnicosPorObjeto> ParaChecklistAsync(
        ProcessoSeletivo processo,
        IReadOnlyList<Guid> atosDaLinhagem,
        TimeZoneInfo? fusoInstitucional,
        IReadOnlyDictionary<string, TipoDeAtoCongelado> jaCongelados,
        CancellationToken cancellationToken)
    {
        DateOnly hoje = Hoje(relogio, fusoInstitucional);
        IReadOnlyList<string> ocupados = await TiposOcupadosAsync(atosDaLinhagem, cancellationToken).ConfigureAwait(false);
        LeituraDoCatalogo leitura = await LerDoCatalogoAsync(
            processo, ocupados, null, hoje, jaCongelados, cancellationToken)
            .ConfigureAwait(false);

        return TiposDeAtoUnicosPorObjeto.ParaChecklist(leitura.Unicos, ocupados, leitura.Classificacoes);
    }

    internal static ClassificacaoDoTipoDeAto ClassificacaoDe(TipoAtoPublicadoView tipo) =>
        new(tipo.EhResultado, tipo.CongelaConfiguracao);

    /// <summary>O dia civil de hoje no fuso institucional, a data em que o catálogo é consultado.</summary>
    internal static DateOnly Hoje(TimeProvider relogio, TimeZoneInfo? fusoInstitucional)
    {
        ArgumentNullException.ThrowIfNull(relogio);

        return (fusoInstitucional ?? TimeZoneInfo.Utc).DiaCivilDe(relogio.GetUtcNow());
    }

    // O tipo do ato da operação serve só à vaga e ao registro do próprio ato. Se o mesmo código é de um
    // item do cronograma, a classificação do item é a congelada na versão, e a do ato não a sobrescreve.
    // Sem esse item na versão, o tipo da operação já foi lido do catálogo e entra na classificação.
    private static void AcrescentarOTipoDoAto(
        LeituraDoCatalogo leitura, TipoAtoPublicadoView tipoDaOperacao, IReadOnlyDictionary<string, TipoDeAtoCongelado> jaCongelados)
    {
        if (jaCongelados.ContainsKey(tipoDaOperacao.Codigo))
        {
            return;
        }

        if (tipoDaOperacao.UnicoPorObjeto)
        {
            leitura.Unicos[tipoDaOperacao.Codigo] = tipoDaOperacao.Nome;
        }

        leitura.Classificacoes[tipoDaOperacao.Codigo] = ClassificacaoDe(tipoDaOperacao);
    }

    private async Task<IReadOnlyList<string>> TiposOcupadosAsync(IReadOnlyList<Guid> atosDaLinhagem, CancellationToken cancellationToken) =>
        atosDaLinhagem.Count == 0
            ? []
            : await atoDoProcessoRepository
                .ListarTiposDosAtosAsync(atosDaLinhagem, cancellationToken)
                .ConfigureAwait(false);

    private async Task<LeituraDoCatalogo> LerDoCatalogoAsync(
        ProcessoSeletivo processo,
        IEnumerable<string> codigosOcupados,
        string? codigoJaConferido,
        DateOnly dataVigencia,
        IReadOnlyDictionary<string, TipoDeAtoCongelado> jaCongelados,
        CancellationToken cancellationToken)
    {
        string[] codigos = CodigosALer(processo, codigosOcupados, codigoJaConferido, jaCongelados);
        IReadOnlyList<TipoAtoPublicadoView> vigentes = codigos.Length == 0
            ? []
            : await tipoAtoReader.ListarVigentesAsync(codigos, dataVigencia, cancellationToken).ConfigureAwait(false);

        return ClassificarOsVigentes(vigentes, jaCongelados);
    }

    private static string[] CodigosALer(
        ProcessoSeletivo processo,
        IEnumerable<string> codigosOcupados,
        string? codigoJaConferido,
        IReadOnlyDictionary<string, TipoDeAtoCongelado> jaCongelados) =>
    [
        .. TiposDeAtoDoCronograma.DeclaracoesDeAtos(EstadoDaConfiguracao.De(processo))
            .Select(static declaracao => declaracao.AtoCodigo)
            .Concat(codigosOcupados)
            .Where(codigo => !string.Equals(codigo, codigoJaConferido, StringComparison.Ordinal) && !jaCongelados.ContainsKey(codigo))
            .Distinct(StringComparer.Ordinal),
    ];

    private static LeituraDoCatalogo ClassificarOsVigentes(
        IReadOnlyList<TipoAtoPublicadoView> vigentes, IReadOnlyDictionary<string, TipoDeAtoCongelado> jaCongelados)
    {
        Dictionary<string, string> unicos = vigentes
            .Where(static tipo => tipo.UnicoPorObjeto)
            .GroupBy(static tipo => tipo.Codigo, StringComparer.Ordinal)
            .ToDictionary(static grupo => grupo.Key, static grupo => grupo.First().Nome, StringComparer.Ordinal);
        Dictionary<string, ClassificacaoDoTipoDeAto> classificacoes = vigentes
            .GroupBy(static tipo => tipo.Codigo, StringComparer.Ordinal)
            .ToDictionary(static grupo => grupo.Key, static grupo => ClassificacaoDe(grupo.First()), StringComparer.Ordinal);
        LeituraDoCatalogo leitura = new(unicos, classificacoes);
        PrevalecerOsJaCongelados(leitura, jaCongelados);

        return leitura;
    }

    // O nome do tipo congelado não viaja no envelope: a recusa o cita pelo código.
    private static void PrevalecerOsJaCongelados(LeituraDoCatalogo leitura, IReadOnlyDictionary<string, TipoDeAtoCongelado> jaCongelados)
    {
        foreach (TipoDeAtoCongelado congelado in jaCongelados.Values)
        {
            leitura.Classificacoes[congelado.Codigo] = congelado.Classificacao;
            if (congelado.UnicoPorObjeto)
            {
                leitura.Unicos[congelado.Codigo] = congelado.Codigo;
            }
        }
    }

    private sealed record LeituraDoCatalogo(
        Dictionary<string, string> Unicos,
        Dictionary<string, ClassificacaoDoTipoDeAto> Classificacoes);
}
