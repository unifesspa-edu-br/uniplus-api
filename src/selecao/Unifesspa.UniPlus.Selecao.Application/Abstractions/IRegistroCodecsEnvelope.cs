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
/// Lê e reconstrói a versão corrente do envelope, conferindo sua integridade.
/// </summary>
public interface IRegistroCodecsEnvelope
{
    /// <summary>Reconstrói a configuração após conferir a evidência congelada.</summary>
    Result<EnvelopeReidratado> Reidratar(VersaoConfiguracao versao);

    /// <summary>Informa se a versão do envelope pode ser lida.</summary>
    bool LeAVersao(string schemaVersion);
}
