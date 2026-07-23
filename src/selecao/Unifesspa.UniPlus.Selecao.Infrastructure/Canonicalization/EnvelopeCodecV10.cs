namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// A versão <c>1.0</c> do envelope: <b>conhecida e recusada</b>, com motivo (ADR-0110 D1).
/// </summary>
/// <remarks>
/// <para>
/// Ela podia congelar <c>atendimento</c> e <c>classificacao</c> como
/// <c>{"status":"nao_construido"}</c> — o fallback silencioso que a <b>D8 da ADR-0109</b>
/// matou. Não há o que reidratar: a classificação é o bloco que <b>determina o
/// resultado</b> do certame, e reconstruir um agregado a partir de um stub produziria
/// uma configuração que nunca existiu.
/// </para>
/// <para>
/// Estar aqui é o que a distingue de uma versão <b>desconhecida</b>. As duas são
/// recusadas, mas por motivos diferentes — e um operador diante de um descarte que
/// falhou precisa saber qual dos dois é.
/// </para>
/// </remarks>
public sealed class EnvelopeCodecV10 : IEnvelopeCodec
{
    public string SchemaVersion => "1.0";

    public IPerfilCanonico Perfil => PerfilCanonicoV1.Instancia;

    public string AlgoritmoHash => Perfil.Algoritmo;

    public bool TemEncoder => false;

    public bool TemDecoder => false;

    public string? MotivoDaRecusa =>
        "A versão 1.0 do envelope podia congelar 'atendimento' e 'classificacao' como 'nao_construido' " +
        "(fallback removido pela ADR-0109 D8) — um agregado reconstruído a partir dela teria a dimensão " +
        "que determina o resultado do certame vazia. Não há reidratação parcial.";

    public SnapshotCanonico Codificar(EntradaCanonicalizacao entrada) =>
        throw new NotSupportedException($"A versão {SchemaVersion} do envelope não é emissível: {MotivoDaRecusa}");

    public Result<EnvelopeReidratado> Decodificar(VersaoConfiguracao versao) =>
        Result<EnvelopeReidratado>.Failure(new DomainError(
            ErrosCodecEnvelope.VersaoNaoReidratavel,
            MotivoDaRecusa!));
}
