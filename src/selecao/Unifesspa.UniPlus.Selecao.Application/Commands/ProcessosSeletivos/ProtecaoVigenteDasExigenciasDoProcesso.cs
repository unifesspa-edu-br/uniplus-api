namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

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
    public static async Task<ProtecaoVigenteDoProcesso> MontarAsync(
        ProcessoSeletivo processo,
        ITipoDocumentoReader tipoDocumentoReader,
        IReadOnlyList<FatoCandidatoView> catalogoDeFatos,
        CongeladoNaVersaoVigente? daVersaoVigente,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(tipoDocumentoReader);
        ArgumentNullException.ThrowIfNull(catalogoDeFatos);

        Dictionary<Guid, ProtecaoDoDocumento?> doCadastro = [];
        foreach (Guid tipoId in processo.DocumentosExigidos.Select(static d => d.TipoDocumentoOrigemId).Distinct())
        {
            TipoDocumentoView? tipo = await tipoDocumentoReader.ObterPorIdAsync(tipoId, cancellationToken).ConfigureAwait(false);
            doCadastro[tipoId] = tipo is null
                ? null
                : new ProtecaoDoDocumento(tipo.ClassificacaoProtecao, tipo.FinalidadeTratamento, tipo.HipoteseLegal);
        }

        Dictionary<Guid, ProtecaoDoDocumento> porExigencia = [];
        foreach (DocumentoExigido exigencia in processo.DocumentosExigidos)
        {
            if (daVersaoVigente is not null
                && daVersaoVigente.Exigencias.TryGetValue(exigencia.Id, out ExigenciaCongelada? congelada)
                && congelada.TipoDocumentoOrigemId == exigencia.TipoDocumentoOrigemId)
            {
                porExigencia[exigencia.Id] = congelada.Protecao;
            }
            else if (doCadastro[exigencia.TipoDocumentoOrigemId] is { } vigente)
            {
                // O tipo que o cadastro não devolve mantém a cópia da exigência.
                porExigencia[exigencia.Id] = vigente;
            }
        }

        IReadOnlyDictionary<string, FatoDerivadoCongelado> derivados =
            CongelamentoDosFatosDerivados.Resolver(catalogoDeFatos, daVersaoVigente?.FatosDerivados);
        Dictionary<string, ClassificacaoDeProtecao> classificacaoDosDerivados = [];
        foreach (FatoDerivadoCongelado derivado in derivados.Values)
        {
            if (ClassificacoesDeProtecao.TryAnalisar(derivado.ClassificacaoProtecao, out ClassificacaoDeProtecao nivel))
            {
                classificacaoDosDerivados[derivado.Codigo] = nivel;
            }
        }

        return new ProtecaoVigenteDoProcesso(new ProtecaoVigenteDasExigencias(classificacaoDosDerivados, porExigencia), derivados);
    }

    /// <summary>
    /// O que a versão vigente congelou — as exigências, por id, com o tipo e a proteção, e os fatos
    /// derivados —; nulo quando o processo não tem versão ou a versão não se reidrata (a retificação
    /// recusa essa por conta própria).
    /// </summary>
    public static CongeladoNaVersaoVigente? DaVersao(
        IRegistroCodecsEnvelope registroCodecs, VersaoConfiguracao? versaoVigente)
    {
        ArgumentNullException.ThrowIfNull(registroCodecs);
        if (versaoVigente is null)
        {
            return null;
        }

        Result<EnvelopeReidratado> vigente = registroCodecs.Reidratar(versaoVigente);
        if (vigente.IsFailure)
        {
            return null;
        }

        return new CongeladoNaVersaoVigente(
            vigente.Value!.Grafo.DocumentosExigidos.ToDictionary(
                static e => e.Id,
                static e => new ExigenciaCongelada(
                    e.TipoDocumentoOrigemId,
                    new ProtecaoDoDocumento(e.ClassificacaoProtecao, e.FinalidadeTratamento, e.HipoteseLegal))),
            vigente.Value!.FatosDerivadosCongelados);
    }

    public static async Task<CongeladoNaVersaoVigente?> LerDaVersaoVigenteAsync(
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        Guid processoSeletivoId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);

        VersaoConfiguracao? versaoAtual = await processoSeletivoRepository
            .ObterVersaoAtualAsync(processoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        return DaVersao(registroCodecs, versaoAtual);
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
