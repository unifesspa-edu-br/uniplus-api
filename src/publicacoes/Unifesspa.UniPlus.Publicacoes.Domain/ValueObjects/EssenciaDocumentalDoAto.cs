namespace Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;

/// <summary>
/// A essência documental e normativa do ato (ADR-0105): a numeração, o tipo copiado do catálogo, a data
/// que o documento declara, o hash do documento, o assinante e a cadeia de retificação. É o que o gatilho
/// append-only protege (ADR-0063). O instante da divulgação fica fora: é gravado pelo sistema, não
/// declarado pelo documento.
/// </summary>
/// <param name="Numeracao">Órgão, série, ano e número.</param>
/// <param name="Tipo">O tipo do ato por valor, no instante do registro.</param>
/// <param name="DataDoAto">Data que o documento declara. Documental: nunca ordena vigência.</param>
/// <param name="DocumentoHash">Hash SHA-256 do PDF publicado, em hexadecimal minúsculo.</param>
/// <param name="Assinante">Nome de quem assina o ato.</param>
/// <param name="Retificacao">O ato que este retifica, quando é uma retificação.</param>
public readonly record struct EssenciaDocumentalDoAto(
    NumeracaoDoAto Numeracao,
    TipoDoAtoNoRegistro Tipo,
    DateOnly DataDoAto,
    string DocumentoHash,
    string Assinante,
    RelacaoDeRetificacao Retificacao)
{
    private const int TipoNomeMaximo = 200;
    private const int AssinanteMaximo = 200;

    /// <summary>
    /// A essência normalizada e coerente, ou a exceção da primeira recusa de <see cref="Conferir"/>.
    /// A validação de negócio acumulada é da conferência da declaração; aqui ficam as invariantes de
    /// última linha, que lançam.
    /// </summary>
    internal EssenciaDocumentalDoAto Conferida() => Conferir().ValorOuLancar();

    /// <summary>
    /// Todas as recusas da essência, na ordem do corpo: numeração, código do tipo, nome do tipo,
    /// hash do documento, assinante e retificação.
    /// </summary>
    internal ParteConferida<EssenciaDocumentalDoAto> Conferir()
    {
        ParteConferida<NumeracaoDoAto> numeracao = Numeracao.Conferir();
        ParteConferida<TipoDoAtoNoRegistro> tipo = ConferirTipo();
        ParteConferida<(string Hash, string Assinante)> documento = ConferirDocumento(DocumentoHash, Assinante);
        ParteConferida<RelacaoDeRetificacao> retificacao = Retificacao.Conferir();
        FieldError[] recusas = [.. numeracao.Recusas, .. tipo.Recusas, .. documento.Recusas, .. retificacao.Recusas];

        return recusas.Length > 0
            ? ParteConferida<EssenciaDocumentalDoAto>.Recusada(recusas)
            : ParteConferida<EssenciaDocumentalDoAto>.Aceita(ComPartes(numeracao.Valor, tipo.Valor, documento.Valor, retificacao.Valor));
    }

    private EssenciaDocumentalDoAto ComPartes(
        NumeracaoDoAto numeracao, TipoDoAtoNoRegistro tipo, (string Hash, string Assinante) documento, RelacaoDeRetificacao retificacao) =>
        this with
        {
            Numeracao = numeracao,
            Tipo = tipo,
            DocumentoHash = documento.Hash,
            Assinante = documento.Assinante,
            Retificacao = retificacao,
        };

    private ParteConferida<TipoDoAtoNoRegistro> ConferirTipo()
    {
        ParteConferida<string> codigo = ConferirCodigoDoTipo(Tipo.Codigo);
        ParteConferida<string> nome = TextoDoAto.Opcional(Tipo.Nome, TipoNomeMaximo, "tipoNome", "O nome do tipo de ato");
        TipoDoAtoNoRegistro tipo = Tipo;

        return ParteConferida<TipoDoAtoNoRegistro>.Compor(
            [.. codigo.Recusas, .. nome.Recusas],
            () => tipo with { Codigo = codigo.Valor!, Nome = nome.Valor });
    }

    /// <summary>O código do tipo como chegou: a regra é a do cadastro do tipo, sem normalizar.</summary>
    internal static ParteConferida<string> ConferirCodigoDoTipo(string? tipoCodigo) =>
        ParteConferida<string>.Decidida(tipoCodigo, TipoAtoPublicado.CampoDoCodigo.Recusa(tipoCodigo, "tipoCodigo"));

    /// <summary>A data que o documento declara: ausente é recusada como obrigatória.</summary>
    internal static ParteConferida<DateOnly> ConferirDataDoAto(DateOnly? dataDoAto) =>
        ParteConferida<DateOnly>.Decidida(
            dataDoAto.GetValueOrDefault(),
            dataDoAto is null
                ? new FieldError("dataDoAto", new DomainError(CodigosDeValidacao.Obrigatorio, "Informe a data do ato."))
                : null);

    /// <summary>As recusas do hash e do assinante, nessa ordem; o assinante sai sem espaços nas pontas.</summary>
    internal static ParteConferida<(string Hash, string Assinante)> ConferirDocumento(string? documentoHash, string? assinante)
    {
        ParteConferida<string> hash = ConferirHashDoDocumento(documentoHash);
        ParteConferida<string> quemAssina = TextoDoAto.Obrigatorio(assinante, AssinanteMaximo, "assinante", "O assinante");

        return ParteConferida<(string Hash, string Assinante)>.Compor(
            [.. hash.Recusas, .. quemAssina.Recusas],
            () => (hash.Valor!, quemAssina.Valor!));
    }

    private static ParteConferida<string> ConferirHashDoDocumento(string? documentoHash) =>
        ParteConferida<string>.Decidida(
            documentoHash,
            HashSha256.TemFormatoValido(documentoHash)
                ? null
                : new FieldError("documentoHash", new DomainError(
                    AtoNormativoErrorCodes.HashDoDocumentoInvalido,
                    "O hash do documento deve ser um SHA-256 em hexadecimal minúsculo (64 caracteres).")));
}
