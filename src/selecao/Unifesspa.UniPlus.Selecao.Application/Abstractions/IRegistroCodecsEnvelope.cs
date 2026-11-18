namespace Unifesspa.UniPlus.Selecao.Application.Abstractions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O envelope de uma <see cref="VersaoConfiguracao"/> reconstruído de volta em
/// entidades: a configuração publicada das <b>seis dimensões</b> e os dados que a recanonicalização
/// dele exige.
/// </summary>
/// <remarks>
/// Os dados que acompanham a configuração permitem reproduzir a canonicalização. O bloco
/// <c>periodo</c> e o <c>hashesEdital</c> vêm dos <see cref="DadosEdital"/> e do hash
/// do documento, e a versão <c>N &gt; 1</c> carrega o 18º bloco <c>retificacao</c>.
/// Reidratar só a configuração não permite conferir a fidelidade do envelope.
/// </remarks>
public sealed class EnvelopeReidratado
{
    public EnvelopeReidratado(
        ConfiguracaoPublicada configuracao,
        DadosEdital dados,
        string hashDocumento,
        string fusoHorario,
        RetificacaoDoEdital? retificacao,
        ResultadoConformidade? conformidade,
        DadosDeCatalogoDaConfiguracaoPublicada dadosDeCatalogo)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        ArgumentNullException.ThrowIfNull(dados);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashDocumento);
        ArgumentException.ThrowIfNullOrWhiteSpace(fusoHorario);
        ArgumentNullException.ThrowIfNull(dadosDeCatalogo);

        Configuracao = configuracao;
        Dados = dados;
        HashDocumento = hashDocumento;
        FusoHorario = fusoHorario;
        Retificacao = retificacao;
        Conformidade = conformidade;
        DadosDeCatalogo = CompletarDadosDeCatalogo(dadosDeCatalogo);
    }

    public ConfiguracaoPublicada Configuracao { get; }

    public DadosEdital Dados { get; }

    public string HashDocumento { get; }

    public string FusoHorario { get; }

    public RetificacaoDoEdital? Retificacao { get; }

    public ResultadoConformidade? Conformidade { get; }

    /// <summary>Dados do catálogo preservados na versão publicada.</summary>
    public DadosDeCatalogoDaConfiguracaoPublicada DadosDeCatalogo { get; }

    private static DadosDeCatalogoDaConfiguracaoPublicada CompletarDadosDeCatalogo(DadosDeCatalogoDaConfiguracaoPublicada dados) => dados with
    {
        AgregadosDosGrupos = dados.AgregadosDosGrupos ?? [],
        ApresentacaoDosPressupostos = dados.ApresentacaoDosPressupostos ?? new Dictionary<string, ApresentacaoDoFato>(StringComparer.Ordinal),
        TiposDeAtoCongelados = dados.TiposDeAtoCongelados ?? new Dictionary<string, TipoDeAtoCongelado>(StringComparer.Ordinal),
        FatosDerivadosCongelados = dados.FatosDerivadosCongelados ?? new Dictionary<string, FatoDerivadoCongelado>(StringComparer.Ordinal),
    };
}

/// <summary>
/// O que o sistema sabe fazer com uma versão do envelope —
/// <b>capacidades</b>, não um estado exclusivo.
/// </summary>
/// <remarks>
/// <para>
/// <b>Reidratável ⟺ tem encoder E decoder.</b> Encoder sem decoder não reidrata;
/// decoder sem encoder não <b>prova</b> a fidelidade da reidratação.
/// </para>
/// <para>
/// A versão que <b>emite</b> hoje é uma só, e é outra coisa: quando a <c>1.2</c>
/// chegar, a <c>1.1</c> perde a emissão corrente mas <b>mantém</b> encoder e decoder —
/// senão a leitura de um certame retificado antes daquele bump deixaria de ser
/// verificável.
/// </para>
/// </remarks>
/// <param name="MotivoDaRecusa">
/// Obrigatório em toda versão que <b>não</b> reidrata: o sistema sabe que ela existe e
/// diz <b>por que</b> a recusa. Uma versão conhecida que falhasse sem motivo seria
/// indistinguível de uma desconhecida.
/// </param>
public sealed record CapacidadeCodec(
    string SchemaVersion,
    bool TemEncoder,
    bool TemDecoder,
    string? MotivoDaRecusa)
{
    public bool Reidratavel => TemEncoder && TemDecoder;
}

/// <summary>
/// Registro de <b>codecs</b> do envelope de congelamento, por versão.
/// </summary>
/// <remarks>
/// <para>
/// Um <i>decoder</i> por versão não basta. Provar o round-trip de uma <c>1.1</c> exige
/// <b>recanonicalizá-la com o encoder <c>1.1</c></b> — e o
/// <see cref="ISnapshotPublicacaoCanonicalizer"/> só emite a versão <b>corrente</b>.
/// No dia da <c>1.2</c>, verificar a fidelidade da reidratação de uma <c>1.1</c>
/// ficaria impossível. O encoder de uma versão <b>não é aposentado</b> quando ela
/// deixa de ser a corrente.
/// </para>
/// </remarks>
public interface IRegistroCodecsEnvelope
{
    /// <summary>
    /// Reconstrói o envelope de uma versão congelada — <b>verificando antes que os
    /// bytes provam o que dizem provar</b>: hash, algoritmo e coerência com a própria
    /// linha da <see cref="VersaoConfiguracao"/>. Recebe a versão inteira, e não os
    /// bytes, porque a <c>schema_version</c> <b>não está dentro deles</b>.
    /// </summary>
    Result<EnvelopeReidratado> Reidratar(VersaoConfiguracao versao);

    /// <summary>
    /// Recanonicaliza com o encoder <b>daquela</b> versão — não com o corrente. É o que
    /// torna a prova de fidelidade não-circular.
    /// </summary>
    Result<SnapshotCanonico> Recodificar(string schemaVersion, EntradaCanonicalizacao entrada);

    /// <summary>Toda versão que o sistema conhece, com as suas capacidades declaradas.</summary>
    IReadOnlyList<CapacidadeCodec> Capacidades { get; }

    /// <summary>A versão que o sistema <b>emite</b> hoje — uma só (ADR-0109 D1).</summary>
    string SchemaVersionDeEmissaoCorrente { get; }
}

/// <summary>
/// Perguntas de capacidade que toda leitura de envelope congelado faz antes de tocar nos bytes.
/// </summary>
public static class RegistroCodecsEnvelopeExtensions
{
    /// <summary>
    /// A <paramref name="schemaVersion"/> está entre as capacidades que o registro hoje sabe
    /// <b>LER</b>?
    /// </summary>
    /// <remarks>
    /// Comparação ordinal — <c>SchemaVersion</c> é token, não texto localizável, e <c>"0.0.7"</c>
    /// não é apelido de <c>"0.0.07"</c> nem de variante de caixa. Vive aqui, e não repetida em cada
    /// leitura pública, porque uma cópia que envelhecesse sozinha faria dois endpoints discordarem
    /// sobre o que o sistema sabe ler.
    /// </remarks>
    public static bool SabeLer(this IRegistroCodecsEnvelope registroCodecs, string schemaVersion)
    {
        ArgumentNullException.ThrowIfNull(registroCodecs);

        return registroCodecs.Capacidades.Any(capacidade =>
            string.Equals(capacidade.SchemaVersion, schemaVersion, StringComparison.Ordinal)
            && capacidade.TemDecoder);
    }
}
