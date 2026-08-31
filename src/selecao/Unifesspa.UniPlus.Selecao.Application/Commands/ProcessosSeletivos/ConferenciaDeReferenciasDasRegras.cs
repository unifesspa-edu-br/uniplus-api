namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

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

            string? referenciaOrfa = await PrimeiraReferenciaOrfaAsync(
                regra.Predicado, modalidadeReader, tipoDocumentoReader, tipoEtapaReader, cancellationToken)
                .ConfigureAwait(false);

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

    private static async Task<string?> PrimeiraReferenciaOrfaAsync(
        PredicadoObrigatoriedade predicado,
        IModalidadeReader modalidadeReader,
        ITipoDocumentoReader tipoDocumentoReader,
        ITipoEtapaReader tipoEtapaReader,
        CancellationToken cancellationToken)
    {
        switch (predicado)
        {
            case EtapaObrigatoria etapa:
                return await CasaComOCadastroAsync(
                        etapa.TipoEtapaCodigo,
                        async codigo => (await tipoEtapaReader.ObterAtivoPorCodigoAsync(codigo, cancellationToken).ConfigureAwait(false))?.Codigo)
                    .ConfigureAwait(false)
                    ? null
                    : $"tipo de etapa '{etapa.TipoEtapaCodigo}'";

            case DocumentoObrigatorioParaModalidade documento:
                if (!await CasaComOCadastroAsync(
                        documento.Modalidade,
                        async codigo => (await modalidadeReader.ObterVivaPorCodigoAsync(codigo, cancellationToken).ConfigureAwait(false))?.Codigo)
                    .ConfigureAwait(false))
                {
                    return $"modalidade '{documento.Modalidade}'";
                }

                return await CasaComOCadastroAsync(
                        documento.TipoDocumento,
                        async codigo => (await tipoDocumentoReader.ObterVivoPorCodigoAsync(codigo, cancellationToken).ConfigureAwait(false))?.Codigo)
                    .ConfigureAwait(false)
                    ? null
                    : $"tipo de documento '{documento.TipoDocumento}'";

            case ModalidadesMinimas modalidades:
                foreach (string codigo in modalidades.Codigos ?? [])
                {
                    if (!await CasaComOCadastroAsync(
                            codigo,
                            async c => (await modalidadeReader.ObterVivaPorCodigoAsync(c, cancellationToken).ConfigureAwait(false))?.Codigo)
                        .ConfigureAwait(false))
                    {
                        return $"modalidade '{codigo}'";
                    }
                }

                return null;

            default:
                return null;
        }
    }

    /// <summary>
    /// Casa sse o cadastro devolve um registro vivo <b>e</b> o código dele é idêntico ao
    /// gravado na regra.
    /// </summary>
    /// <remarks>
    /// Encontrar o registro não basta: os leitores aparam e normalizam o valor buscado, então
    /// uma regra gravada como <c>"LB_PPI "</c> acha a modalidade <c>"LB_PPI"</c>. Quem avalia a
    /// conformidade compara por igualdade ordinal contra o código congelado no processo, e para
    /// ela os dois valores continuam diferentes — a cláusula seguiria aprovada por vacuidade,
    /// que é justamente o que este gate existe para impedir. Regras gravadas antes da
    /// normalização na escrita são o caso real disso.
    /// </remarks>
    private static async Task<bool> CasaComOCadastroAsync(
        string? codigoGravado,
        Func<string, Task<string?>> codigoVivoDoCadastro)
    {
        if (string.IsNullOrEmpty(codigoGravado))
        {
            return false;
        }

        string? codigoVivo = await codigoVivoDoCadastro(codigoGravado).ConfigureAwait(false);
        return string.Equals(codigoVivo, codigoGravado, StringComparison.Ordinal);
    }
}
