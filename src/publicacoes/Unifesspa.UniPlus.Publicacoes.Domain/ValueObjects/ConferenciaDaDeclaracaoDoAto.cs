namespace Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;

/// <summary>
/// A declaração do ato conferida parte por parte. Só compõe: a regra de cada parte é de quem a é dona
/// (numeração, tipo do ato, essência, versão do objeto, retificação, vínculos e anexos).
/// </summary>
/// <param name="Numeracao">Órgão, série, ano e número.</param>
/// <param name="CodigoDoTipo">O código do tipo, como chegou.</param>
/// <param name="DataDoAto">A data que o documento declara.</param>
/// <param name="Documento">O hash do documento e o assinante.</param>
/// <param name="VersaoDoObjeto">O par da versão do objeto, completo ou ausente.</param>
/// <param name="Retificacao">O ato retificado e o motivo.</param>
/// <param name="Vinculos">Uma parte por vínculo, na posição do pedido.</param>
/// <param name="RecusasDosAnexos">As recusas da lista de anexos e de cada anexo.</param>
public sealed record ConferenciaDaDeclaracaoDoAto(
    ParteConferida<NumeracaoDoAto> Numeracao,
    ParteConferida<string> CodigoDoTipo,
    ParteConferida<DateOnly> DataDoAto,
    ParteConferida<(string Hash, string Assinante)> Documento,
    ParteConferida<ReferenciaVersaoDoObjeto> VersaoDoObjeto,
    ParteConferida<RelacaoDeRetificacao> Retificacao,
    IReadOnlyList<ParteConferida<EntidadeVinculada>> Vinculos,
    IReadOnlyList<FieldError> RecusasDosAnexos)
{
    /// <summary>
    /// As recusas que não dependem do banco, na ordem do corpo do pedido: numeração, código do tipo,
    /// data do ato, documento, versão do objeto, retificação, vínculos por posição e anexos. A leitura do banco
    /// intercala as suas depois, por campo.
    /// </summary>
    public IReadOnlyList<FieldError> Recusas =>
    [
        .. Numeracao.Recusas,
        .. CodigoDoTipo.Recusas,
        .. DataDoAto.Recusas,
        .. Documento.Recusas,
        .. VersaoDoObjeto.Recusas,
        .. Retificacao.Recusas,
        .. Vinculos.SelectMany(vinculo => vinculo.Recusas),
        .. RecusasDosAnexos,
    ];

    public static ConferenciaDaDeclaracaoDoAto De(DeclaracaoDoAtoNormativo declaracao)
    {
        ArgumentNullException.ThrowIfNull(declaracao);

        return new(
            declaracao.Numeracao.Conferir(),
            EssenciaDocumentalDoAto.ConferirCodigoDoTipo(declaracao.TipoCodigo),
            EssenciaDocumentalDoAto.ConferirDataDoAto(declaracao.DataDoAto),
            EssenciaDocumentalDoAto.ConferirDocumento(declaracao.DocumentoHash, declaracao.Assinante),
            ReferenciaVersaoDoObjeto.DoPar(declaracao.VersaoDoObjetoId, declaracao.VersaoDoObjetoHash),
            declaracao.Retificacao.Conferir(),
            AtoNormativo.ConferirVinculos(declaracao.Vinculos),
            AnexoDoAto.Recusas(declaracao.Anexos));
    }
}
