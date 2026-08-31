namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Collections.Frozen;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Confere se as regras vigentes podem, de fato, ser avaliadas: a forma do predicado e
/// as referências por código que ele carrega — modalidade, tipo de documento e tipo de
/// etapa — contra os cadastros vivos.
/// </summary>
/// <remarks>
/// <para>Sem esta conferência, uma regra que referencia um código inexistente é
/// <b>aprovada por vacuidade</b>: <c>AvaliadorConformidadeLegal</c> é serviço de
/// domínio puro e não consulta cadastro, então "modalidade não ofertada neste
/// edital" — situação legítima — é indistinguível de "modalidade que não existe".
/// A cláusula legal passa, e o edital publica sob uma obrigação que nunca poderia
/// ser satisfeita.</para>
/// <para>A validação na escrita impede que regras assim nasçam; esta conferência
/// alcança as que já existem e as que ficaram órfãs porque o cadastro mudou depois.
/// Reprovar é o comportamento correto: uma regra que não pode ser avaliada não é
/// uma regra cumprida.</para>
/// <para>A forma entra pelo mesmo motivo e sem migração que a garanta: uma exigência de
/// modalidades mínimas gravada sem nenhum código atravessa a avaliação com zero
/// requisitos e sai aprovada — vacuidade idêntica à do código órfão, por outro
/// caminho.</para>
/// </remarks>
internal static class ConferenciaDeReferenciasDasRegras
{
    /// <summary>
    /// Motivo pelo qual cada regra não pode ser avaliada, indexado pelo id — ausente
    /// quando a regra é avaliável. Leitura e transição consomem este mesmo levantamento,
    /// para que a consulta pública nunca dê por conforme o que a publicação vai recusar.
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, string>> LevantarRegrasInavaliaveisAsync(
        IReadOnlyList<ObrigatoriedadeLegal> regras,
        IModalidadeReader modalidadeReader,
        ITipoDocumentoReader tipoDocumentoReader,
        ITipoEtapaReader tipoEtapaReader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(regras);
        ArgumentNullException.ThrowIfNull(modalidadeReader);
        ArgumentNullException.ThrowIfNull(tipoDocumentoReader);
        ArgumentNullException.ThrowIfNull(tipoEtapaReader);

        if (regras.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        CatalogosVivos catalogos = await CarregarCatalogosAsync(
            modalidadeReader, tipoDocumentoReader, tipoEtapaReader, cancellationToken).ConfigureAwait(false);

        Dictionary<Guid, string> inavaliaveis = [];

        foreach (ObrigatoriedadeLegal regra in regras)
        {
            // Forma primeiro, como na escrita: nenhuma migração garante que as regras
            // gravadas antes desta validação tenham conteúdo avaliável.
            Result forma = ObrigatoriedadeLegal.ValidarFormaDoPredicado(regra.Predicado);
            if (forma.IsFailure)
            {
                inavaliaveis[regra.Id] = forma.Error!.Message;
                continue;
            }

            string? referenciaOrfa = PrimeiraReferenciaOrfa(regra.Predicado, catalogos);
            if (referenciaOrfa is not null)
            {
                inavaliaveis[regra.Id] = $"{referenciaOrfa} não existe no cadastro";
            }
        }

        return inavaliaveis;
    }

    /// <summary>Motivo com que a regra inavaliável aparece reprovada na leitura pública.</summary>
    public static string MotivoDaInavaliabilidade(string motivo) =>
        $"regra não pode ser avaliada: {motivo}";

    public static async Task<Result> ConferirAsync(
        IReadOnlyList<ObrigatoriedadeLegal> regras,
        IModalidadeReader modalidadeReader,
        ITipoDocumentoReader tipoDocumentoReader,
        ITipoEtapaReader tipoEtapaReader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(regras);

        IReadOnlyDictionary<Guid, string> inavaliaveis = await LevantarRegrasInavaliaveisAsync(
            regras, modalidadeReader, tipoDocumentoReader, tipoEtapaReader, cancellationToken)
            .ConfigureAwait(false);

        if (inavaliaveis.Count == 0)
        {
            return Result.Success();
        }

        string descritas = string.Join(", ", regras
            .Where(r => inavaliaveis.ContainsKey(r.Id))
            .Select(r => $"{r.RegraCodigo} ({inavaliaveis[r.Id]})"));

        return Result.Failure(new DomainError(
            "ProcessoSeletivo.RegraLegalInavaliavel",
            $"Há regra legal vigente que não pode ser avaliada: {descritas}. "
            + "Corrija a regra ou o cadastro antes de publicar."));
    }

    /// <summary>
    /// Os três catálogos que os predicados referenciam, carregados de uma vez e indexados
    /// por código, com comparação ordinal.
    /// </summary>
    /// <remarks>
    /// <para>Um lookup por código faria uma ida ao banco por item de cada predicado, de cada
    /// regra vigente — nem a lista de modalidades mínimas nem a quantidade de regras têm
    /// teto, e a publicação e o checklist do editor pagariam esse custo em série. Os três
    /// catálogos são pequenos e já têm listagem própria: três consultas resolvem.</para>
    /// <para>O conjunto compara por igualdade ordinal, que é exatamente a pergunta a
    /// responder — a avaliação de conformidade compara assim contra o código congelado no
    /// processo. Encontrar o registro por busca normalizada não bastaria: uma regra gravada
    /// como <c>"LB_PPI "</c> acha a modalidade viva e mesmo assim nunca casaria.</para>
    /// </remarks>
    private sealed record CatalogosVivos(
        FrozenSet<string> Modalidades,
        FrozenSet<string> TiposDocumento,
        FrozenSet<string> TiposEtapa);

    private static async Task<CatalogosVivos> CarregarCatalogosAsync(
        IModalidadeReader modalidadeReader,
        ITipoDocumentoReader tipoDocumentoReader,
        ITipoEtapaReader tipoEtapaReader,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ModalidadeView> modalidades = await modalidadeReader
            .ListarVivosAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<TipoDocumentoView> tiposDocumento = await tipoDocumentoReader
            .ListarVivosAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<TipoEtapaView> tiposEtapa = await tipoEtapaReader
            .ListarAtivosAsync(cancellationToken).ConfigureAwait(false);

        return new CatalogosVivos(
            modalidades.Select(static m => m.Codigo).ToFrozenSet(StringComparer.Ordinal),
            tiposDocumento.Select(static t => t.Codigo).ToFrozenSet(StringComparer.Ordinal),
            tiposEtapa.Select(static t => t.Codigo).ToFrozenSet(StringComparer.Ordinal));
    }

    private static string? PrimeiraReferenciaOrfa(PredicadoObrigatoriedade predicado, CatalogosVivos catalogos)
    {
        switch (predicado)
        {
            case EtapaObrigatoria etapa:
                return catalogos.TiposEtapa.Contains(etapa.TipoEtapaCodigo ?? string.Empty)
                    ? null
                    : $"tipo de etapa '{etapa.TipoEtapaCodigo}'";

            case DocumentoObrigatorioParaModalidade documento:
                if (!catalogos.Modalidades.Contains(documento.Modalidade ?? string.Empty))
                {
                    return $"modalidade '{documento.Modalidade}'";
                }

                return catalogos.TiposDocumento.Contains(documento.TipoDocumento ?? string.Empty)
                    ? null
                    : $"tipo de documento '{documento.TipoDocumento}'";

            case ModalidadesMinimas modalidades:
                foreach (string codigo in modalidades.Codigos ?? [])
                {
                    if (!catalogos.Modalidades.Contains(codigo ?? string.Empty))
                    {
                        return $"modalidade '{codigo}'";
                    }
                }

                return null;

            default:
                return null;
        }
    }
}
