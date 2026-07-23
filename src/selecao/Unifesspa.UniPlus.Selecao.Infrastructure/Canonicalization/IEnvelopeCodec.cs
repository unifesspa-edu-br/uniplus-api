namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// O que o sistema sabe fazer com <b>uma</b> versão do envelope (ADR-0110 D1).
/// </summary>
/// <remarks>
/// É um <b>codec</b>, e não um decoder: provar que a reidratação de uma <c>1.1</c> foi
/// fiel exige recanonicalizá-la com o encoder <b>da <c>1.1</c></b>. Aposentar o encoder
/// de uma versão no dia em que ela deixa de ser a corrente tornaria o descarte de todo
/// certame congelado antes daquele bump <b>não verificável</b> — e uma reidratação sem
/// prova é a que destrói configuração em silêncio.
/// </remarks>
public interface IEnvelopeCodec
{
    string SchemaVersion { get; }

    /// <summary>
    /// Regras de bytes sob as quais esta versão foi congelada. É por aqui que o gate de forma
    /// pergunta “estes bytes são canônicos?” — a pergunta só tem resposta <b>relativa a um
    /// perfil</b>, e a de uma versão nunca é a de outra.
    /// </summary>
    IPerfilCanonico Perfil { get; }

    /// <summary>
    /// Algoritmo de hash que esta versão emite — parte da evidência, não detalhe. Deriva do
    /// <see cref="Perfil"/>: o rótulo persistido e o código que produz os bytes têm de ser a
    /// mesma coisa dita duas vezes, nunca dois literais que alguém mantém em sincronia.
    /// </summary>
    string AlgoritmoHash { get; }

    bool TemEncoder { get; }

    bool TemDecoder { get; }

    /// <summary>Preenchido em toda versão que não reidrata — a recusa é <b>nomeada</b>.</summary>
    string? MotivoDaRecusa { get; }

    SnapshotCanonico Codificar(EntradaCanonicalizacao entrada);

    /// <summary>
    /// Recebe a <see cref="VersaoConfiguracao"/> inteira, e não os bytes: a
    /// <c>schema_version</c> <b>não está dentro deles</b> — é metadado ao lado —, e a
    /// coerência entre o envelope e a linha que o guarda (hash do ato, cadeia de
    /// retificação) só é verificável com os dois em mãos.
    /// </summary>
    Result<EnvelopeReidratado> Decodificar(VersaoConfiguracao versao);
}
