namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Monta o contexto de proteção das exigências que o painel de conformidade, a publicação e a
/// retificação avaliam do mesmo jeito: a classificação dos fatos derivados e a proteção efetiva de
/// cada exigência. O fato derivado que a versão vigente já congelou mantém o que ela congelou, e só o
/// derivado que a versão não tem sai do catálogo de hoje (ADR-0154). A efetiva é a que o tipo de documento tem no cadastro agora, e não a copiada
/// quando a exigência foi gravada, porque o tipo pode ter sido reclassificado no intervalo; na
/// retificação, a exigência que a versão vigente já tem (mesmo id, mesmo tipo) mantém a proteção
/// congelada nela, e reclassificar o tipo não rebaixa o que já foi publicado.
/// </summary>
internal static class ProtecaoVigenteDasExigenciasDoProcesso
{
    /// <summary>
    /// Os tipos de documento vivos do cadastro, lidos de uma vez e por identificador: a conferência da aprovação e o
    /// congelamento da proteção usam a mesma leitura, e o cadastro não é consultado tipo a tipo. O processo sem
    /// exigência não consulta o cadastro.
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, TipoDocumentoView>> LerTiposVivosAsync(
        ProcessoSeletivo processo, ITipoDocumentoReader tipoDocumentoReader, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(tipoDocumentoReader);

        if (processo.DocumentosExigidos.Count == 0)
        {
            return new Dictionary<Guid, TipoDocumentoView>();
        }

        IReadOnlyList<TipoDocumentoView> vivos = await tipoDocumentoReader.ListarVivosAsync(cancellationToken).ConfigureAwait(false);
        return vivos.ToDictionary(static t => t.Id);
    }

    public static ProtecaoVigenteDoProcesso Montar(
        ProcessoSeletivo processo,
        IReadOnlyDictionary<Guid, TipoDocumentoView> tiposVivos,
        IReadOnlyList<FatoCandidatoView> catalogoDeFatos,
        CongeladoNaVersaoVigente? daVersaoVigente)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(tiposVivos);
        ArgumentNullException.ThrowIfNull(catalogoDeFatos);

        IReadOnlyDictionary<string, FatoDerivadoCongelado> derivados =
            CongelamentoDosFatosDerivados.Resolver(catalogoDeFatos, daVersaoVigente?.FatosDerivados);

        return new ProtecaoVigenteDoProcesso(
            new ProtecaoVigenteDasExigencias(
                ClassificacaoDosDerivados(derivados),
                ProtecaoDeCadaExigencia(processo, tiposVivos, daVersaoVigente),
                DerivadosSemProtecaoAprovada(catalogoDeFatos, derivados)),
            derivados);
    }

    private static Dictionary<Guid, ProtecaoDoDocumento> ProtecaoDeCadaExigencia(
        ProcessoSeletivo processo, IReadOnlyDictionary<Guid, TipoDocumentoView> tiposVivos, CongeladoNaVersaoVigente? daVersaoVigente)
    {
        Dictionary<Guid, ProtecaoDoDocumento> porExigencia = [];
        foreach (DocumentoExigido exigencia in processo.DocumentosExigidos)
        {
            if (ProtecaoDaExigencia(exigencia, tiposVivos, daVersaoVigente) is { } protecao)
            {
                porExigencia[exigencia.Id] = protecao;
            }
        }

        return porExigencia;
    }

    private static ProtecaoDoDocumento? ProtecaoDaExigencia(
        DocumentoExigido exigencia, IReadOnlyDictionary<Guid, TipoDocumentoView> tiposVivos, CongeladoNaVersaoVigente? daVersaoVigente)
    {
        if (daVersaoVigente is not null
            && daVersaoVigente.Exigencias.TryGetValue(exigencia.Id, out ExigenciaCongelada? congelada)
            && congelada.TipoDocumentoOrigemId == exigencia.TipoDocumentoOrigemId)
        {
            return congelada.Protecao;
        }

        return tiposVivos.TryGetValue(exigencia.TipoDocumentoOrigemId, out TipoDocumentoView? tipo)
            ? new ProtecaoDoDocumento(tipo.ClassificacaoProtecao, tipo.FinalidadeTratamento, tipo.HipoteseLegal)
            : null;
    }

    private static Dictionary<string, ClassificacaoDeProtecao> ClassificacaoDosDerivados(IReadOnlyDictionary<string, FatoDerivadoCongelado> derivados)
    {
        Dictionary<string, ClassificacaoDeProtecao> classificacao = [];
        foreach (FatoDerivadoCongelado derivado in derivados.Values)
        {
            if (ClassificacoesDeProtecao.TryAnalisar(derivado.ClassificacaoProtecao, out ClassificacaoDeProtecao nivel))
            {
                classificacao[derivado.Codigo] = nivel;
            }
        }

        return classificacao;
    }

    private static HashSet<string> DerivadosSemProtecaoAprovada(
        IReadOnlyList<FatoCandidatoView> catalogoDeFatos, IReadOnlyDictionary<string, FatoDerivadoCongelado> derivados) =>
        new(
            catalogoDeFatos
                .Where(f => !f.ProtecaoAprovada && string.Equals(f.Origem, DominioDoCatalogo.OrigemDerivada, StringComparison.Ordinal))
                .Select(static f => f.Codigo)
                .Where(codigo => !derivados.ContainsKey(codigo)),
            StringComparer.Ordinal);

    /// <summary>
    /// O que a versão vigente congelou — as exigências, por id, com o tipo e a proteção, e os fatos
    /// derivados —; nulo quando o processo não tem versão ou a versão não se reidrata (a retificação
    /// recusa essa por conta própria).
    /// </summary>
    public static CongeladoNaVersaoVigente? DaVersao(
        IRegistroCodecsEnvelope registroCodecs, VersaoConfiguracao? versaoVigente)
    {
        ArgumentNullException.ThrowIfNull(registroCodecs);

        return versaoVigente is null ? null : DoEnvelope(registroCodecs.Reidratar(versaoVigente));
    }

    /// <summary>O que o envelope já reidratado da versão vigente congelou; nulo quando o codec não o leu.</summary>
    public static CongeladoNaVersaoVigente? DoEnvelope(Result<EnvelopeReidratado> vigente)
    {
        ArgumentNullException.ThrowIfNull(vigente);

        return vigente.IsFailure
            ? null
            : new CongeladoNaVersaoVigente(
                vigente.Value!.Grafo.DocumentosExigidos.ToDictionary(
                    static e => e.Id,
                    static e => new ExigenciaCongelada(
                        e.TipoDocumentoOrigemId,
                        new ProtecaoDoDocumento(e.ClassificacaoProtecao, e.FinalidadeTratamento, e.HipoteseLegal))),
                vigente.Value!.FatosDerivadosCongelados);
    }
}

/// <summary>O tipo de documento e a proteção que uma exigência da versão vigente congelou.</summary>
internal sealed record ExigenciaCongelada(Guid TipoDocumentoOrigemId, ProtecaoDoDocumento Protecao);

/// <summary>As exigências e os fatos derivados que a versão vigente congelou.</summary>
internal sealed record CongeladoNaVersaoVigente(
    IReadOnlyDictionary<Guid, ExigenciaCongelada> Exigencias,
    IReadOnlyDictionary<string, FatoDerivadoCongelado> FatosDerivados);

/// <summary>
/// A proteção do processo no momento de avaliar ou congelar: a das exigências, com que se confere o fato
/// comprovado, e os fatos derivados que a versão congela.
/// </summary>
internal sealed record ProtecaoVigenteDoProcesso(
    ProtecaoVigenteDasExigencias Exigencias,
    IReadOnlyDictionary<string, FatoDerivadoCongelado> FatosDerivados);
