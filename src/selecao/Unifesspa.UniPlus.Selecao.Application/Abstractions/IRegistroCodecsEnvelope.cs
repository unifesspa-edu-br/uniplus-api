namespace Unifesspa.UniPlus.Selecao.Application.Abstractions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O envelope de uma <see cref="VersaoConfiguracao"/> reconstruído de volta em
/// entidades: o grafo das <b>seis dimensões</b> mais tudo o que a recanonicalização
/// dele exige (ADR-0110 D1).
/// </summary>
/// <remarks>
/// Os três acompanhantes do grafo não são luxo — são o que torna o round-trip
/// <b>provável</b>. O envelope não é função só da configuração: o bloco
/// <c>periodo</c> e o <c>hashesEdital</c> vêm dos <see cref="DadosEdital"/> e do hash
/// do documento, e a versão <c>N &gt; 1</c> carrega o 18º bloco <c>retificacao</c>.
/// Reidratar só o grafo deixaria a prova de fidelidade impossível de escrever — e uma
/// reidratação que não se prova é exatamente a que destrói configuração em silêncio.
/// </remarks>
public sealed class EnvelopeReidratado
{
    public EnvelopeReidratado(
        GrafoConfiguracao grafo,
        DadosEdital dados,
        string hashDocumento,
        string fusoHorario,
        RetificacaoInfo? retificacao,
        ResultadoConformidade? conformidade,
        IReadOnlyDictionary<string, MetadadoFatoCongelado>? metadadosFatosCongelados = null,
        IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>? valoresSelecionaveisCongelados = null,
        CalendarioDiasUteisCongelado? calendarioDiasUteis = null)
    {
        ArgumentNullException.ThrowIfNull(grafo);
        ArgumentNullException.ThrowIfNull(dados);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashDocumento);
        ArgumentException.ThrowIfNullOrWhiteSpace(fusoHorario);

        Grafo = grafo;
        Dados = dados;
        HashDocumento = hashDocumento;
        FusoHorario = fusoHorario;
        Retificacao = retificacao;
        Conformidade = conformidade;
        MetadadosFatosCongelados = metadadosFatosCongelados;
        ValoresSelecionaveisCongelados = valoresSelecionaveisCongelados;
        CalendarioDiasUteis = calendarioDiasUteis;
    }

    /// <summary>
    /// Calendário de dias úteis <b>congelado nesta versão</b>, e não o vigente hoje. Como o fuso,
    /// acompanha o grafo sem entrar nele: não é estado do agregado, e sim contexto da
    /// canonicalização.
    /// <para>
    /// A razão de voltar por aqui é mais forte que a do fuso. O dataset que originou este bloco
    /// pode ter deixado de ser vigente e sido <b>removido</b> do cadastro; recanonicalizar contra
    /// o vigente não produziria apenas bytes diferentes — não haveria como recuperar a lista que
    /// esta versão congelou, e a prova de fidelidade ficaria impossível de repetir.
    /// </para>
    /// </summary>
    public CalendarioDiasUteisCongelado? CalendarioDiasUteis { get; }

    public GrafoConfiguracao Grafo { get; }

    public DadosEdital Dados { get; }

    public string HashDocumento { get; }

    /// <summary>
    /// Fuso institucional <b>congelado nesta versão</b>, e não o vigente hoje. Acompanha o grafo em
    /// vez de entrar nele porque não é estado do agregado — não há propriedade de fuso para repor na
    /// raiz. Ele volta à recanonicalização porque <c>dataReferenciaFatos</c> é recalculada ao provar
    /// a fidelidade: convertida sob o fuso de hoje, a mesma versão produziria bytes diferentes
    /// depois que o padrão institucional mudasse, e a prova byte a byte deixaria de valer.
    /// </summary>
    public string FusoHorario { get; }

    /// <summary><see langword="null"/> na versão 1 — o envelope de abertura não tem o 18º bloco.</summary>
    public RetificacaoInfo? Retificacao { get; }

    /// <summary>
    /// Evidência forense da conformidade legal congelada (Story #853, §3.4/CA-18) — fora
    /// de <see cref="GrafoConfiguracao"/> porque não é reposta no agregado vivo (o
    /// processo não tem propriedade "Conformidade" para restaurar); serve à paridade
    /// contra o cadastro vivo, não a
    /// <see cref="Entities.ProcessoSeletivo.RestaurarConfiguracaoCongelada"/>.
    /// </summary>
    public ResultadoConformidade? Conformidade { get; }

    /// <summary>
    /// Metadado de fato do candidato congelado (Story #919, RN08), por <c>Codigo</c> —
    /// fora de <see cref="GrafoConfiguracao"/> pela MESMA razão que <see cref="Conformidade"/>:
    /// não é reposto no agregado vivo (<see cref="Domain.Entities.CondicaoGatilho"/>
    /// continua só com o trio bruto <c>{fato, operador, valor}</c>), sobrevive só para
    /// alimentar a recanonicalização em <see cref="Application.Services.RestauradorDeConfiguracao"/> —
    /// a prova estrutural de que a restauração nunca reconsulta o catálogo vivo.
    /// </summary>
    public IReadOnlyDictionary<string, MetadadoFatoCongelado>? MetadadosFatosCongelados { get; }

    /// <summary>
    /// Os valores selecionáveis de cada fato coletado congelado, por <c>FatoCodigo</c> — fora de
    /// <see cref="GrafoConfiguracao"/> pela MESMA razão que <see cref="MetadadosFatosCongelados"/>:
    /// os valores são dado de CATÁLOGO (ou da oferta do processo), não configuração persistida no
    /// agregado vivo — <see cref="Domain.Entities.FatoColetado"/> não ganha propriedade por isso,
    /// e este dicionário sobrevive só para alimentar a recanonicalização em
    /// <see cref="Application.Services.RestauradorDeConfiguracao"/>, a prova estrutural de que a
    /// restauração nunca reconsulta o catálogo vivo.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>? ValoresSelecionaveisCongelados { get; }
}

/// <summary>
/// O que o sistema sabe fazer com uma versão do envelope (ADR-0110 D1) —
/// <b>capacidades</b>, não um estado exclusivo.
/// </summary>
/// <remarks>
/// <para>
/// <b>Reidratável ⟺ tem encoder E decoder.</b> Encoder sem decoder não reidrata;
/// decoder sem encoder não <b>prova</b> o round-trip — e uma reidratação sem prova é
/// o que a Feature inteira existe para evitar.
/// </para>
/// <para>
/// A versão que <b>emite</b> hoje é uma só, e é outra coisa: quando a <c>1.2</c>
/// chegar, a <c>1.1</c> perde a emissão corrente mas <b>mantém</b> encoder e decoder —
/// senão o descarte de um certame retificado antes daquele bump deixaria de ser
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
/// Registro de <b>codecs</b> do envelope de congelamento, por versão (ADR-0110 D1).
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
