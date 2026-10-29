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
/// retificação avaliam do mesmo jeito: a classificação dos fatos do catálogo e a proteção efetiva de
/// cada exigência. A efetiva é a que o tipo de documento tem no cadastro agora, e não a copiada
/// quando a exigência foi gravada, porque o tipo pode ter sido reclassificado no intervalo; na
/// retificação, a exigência que a versão vigente já tem (mesmo id, mesmo tipo) mantém a proteção
/// congelada nela, e reclassificar o tipo não rebaixa o que já foi publicado.
/// </summary>
internal static class ProtecaoVigenteDasExigenciasDoProcesso
{
    public static async Task<ProtecaoVigenteDasExigencias> MontarAsync(
        ProcessoSeletivo processo,
        ITipoDocumentoReader tipoDocumentoReader,
        IReadOnlyList<FatoCandidatoView> catalogoDeFatos,
        IReadOnlyDictionary<Guid, ExigenciaCongelada>? daVersaoVigente,
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
                && daVersaoVigente.TryGetValue(exigencia.Id, out ExigenciaCongelada? congelada)
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

        Dictionary<string, ClassificacaoDeProtecao> doCatalogo = [];
        foreach (FatoCandidatoView fato in catalogoDeFatos)
        {
            if (ClassificacoesDeProtecao.TryAnalisar(fato.ClassificacaoProtecao, out ClassificacaoDeProtecao nivel))
            {
                doCatalogo[fato.Codigo] = nivel;
            }
        }

        return new ProtecaoVigenteDasExigencias(doCatalogo, porExigencia);
    }

    /// <summary>
    /// As exigências da versão vigente, por id, com o tipo e a proteção congelados; nulo quando o
    /// processo não tem versão ou a versão não se reidrata (a retificação recusa essa por conta própria).
    /// </summary>
    public static IReadOnlyDictionary<Guid, ExigenciaCongelada>? DaVersao(
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

        return vigente.Value!.Grafo.DocumentosExigidos.ToDictionary(
            static e => e.Id,
            static e => new ExigenciaCongelada(
                e.TipoDocumentoOrigemId,
                new ProtecaoDoDocumento(e.ClassificacaoProtecao, e.FinalidadeTratamento, e.HipoteseLegal)));
    }

    public static async Task<IReadOnlyDictionary<Guid, ExigenciaCongelada>?> LerDaVersaoVigenteAsync(
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
