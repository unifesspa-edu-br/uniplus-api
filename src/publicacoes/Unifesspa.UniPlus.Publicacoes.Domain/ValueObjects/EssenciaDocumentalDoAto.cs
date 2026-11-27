namespace Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;

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
    private const int OrgaoMaxLength = 200;
    private const int SerieMaxLength = 100;
    private const int NumeroMaxLength = 60;
    private const int TipoNomeMaxLength = 200;
    private const int AssinanteMaxLength = 200;
    private const int MotivoRetificacaoMaxLength = 1000;

    /// <summary>
    /// A essência normalizada e coerente, ou a exceção da primeira invariante violada, na ordem: órgão,
    /// série, código do tipo, assinante, número, motivo da retificação, nome do tipo, ano, hash do
    /// documento, ato retificado vazio e simetria do par de retificação. A validação de negócio é do
    /// handler e do validator; aqui ficam as invariantes de última linha, que lançam.
    /// </summary>
    internal EssenciaDocumentalDoAto Conferida()
    {
        EssenciaDocumentalDoAto normalizada = ComTextosExigidosNormalizados().ComTextosOpcionaisNormalizados();
        ExigirAnoPositivo(normalizada.Numeracao.Ano);
        ExigirHashDoDocumento(normalizada.DocumentoHash);

        return normalizada with { Retificacao = normalizada.Retificacao.ParConferido() };
    }

    /// <summary>
    /// Passos 1 a 4: órgão, série, código do tipo e assinante, sem espaços nas pontas e até o
    /// limite; vazio lança.
    /// </summary>
    private EssenciaDocumentalDoAto ComTextosExigidosNormalizados() =>
        this with
        {
            Numeracao = Numeracao with
            {
                Orgao = Exigido(Numeracao.Orgao, OrgaoMaxLength, "orgao"),
                Serie = Exigido(Numeracao.Serie, SerieMaxLength, "serie"),
            },
            Tipo = Tipo with { Codigo = CodigoConferido(Tipo.Codigo) },
            Assinante = Exigido(Assinante, AssinanteMaxLength, "assinante"),
        };

    /// <summary>
    /// Passos 5 a 7: número, motivo da retificação e nome do tipo; vazio vira nulo, e o que passa
    /// do limite lança. O motivo vem antes do nome do tipo porque é o passo anterior.
    /// </summary>
    private EssenciaDocumentalDoAto ComTextosOpcionaisNormalizados() =>
        this with
        {
            Numeracao = Numeracao with { Numero = Opcional(Numeracao.Numero, NumeroMaxLength, "numero") },
            Retificacao = Retificacao with { Motivo = Opcional(Retificacao.Motivo, MotivoRetificacaoMaxLength, "motivoRetificacao") },
            Tipo = Tipo with { Nome = Opcional(Tipo.Nome, TipoNomeMaxLength, "tipoNome") },
        };

    private static string CodigoConferido(string tipoCodigo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoCodigo);
        if (tipoCodigo.Length > TipoAtoPublicado.TamanhoMaximoDoCodigo || !FormatoDoCodigo.TemFormatoValido(tipoCodigo))
        {
            throw new ArgumentException("Código do tipo de ato fora do formato.", nameof(tipoCodigo));
        }

        return tipoCodigo;
    }

    private static string Exigido(string valor, int maxLength, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(valor, paramName);
        return AteOLimite(valor.Trim(), maxLength, paramName);
    }

    private static string? Opcional(string? valor, int maxLength, string paramName) =>
        string.IsNullOrWhiteSpace(valor) ? null : AteOLimite(valor.Trim(), maxLength, paramName);

    private static string AteOLimite(string texto, int maxLength, string paramName) =>
        texto.Length <= maxLength
            ? texto
            : throw new ArgumentException($"'{paramName}' deve ter no máximo {maxLength} caracteres.", paramName);

    private static void ExigirAnoPositivo(int ano) =>
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ano);

    private static void ExigirHashDoDocumento(string documentoHash)
    {
        if (!HashSha256.TemFormatoValido(documentoHash))
        {
            throw new ArgumentException(
                "Hash do documento deve ser um SHA-256 em hexadecimal minúsculo (64 caracteres).",
                nameof(documentoHash));
        }
    }
}
