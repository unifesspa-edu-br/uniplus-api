namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;

/// <summary>
/// O que o documento da configuração declara, copiado do JSON sem conferir regra: a identificação, as fases do cronograma, o
/// cadastro de reserva por chamada, a manifestação de interesse, a prova presencial, a importação do resultado, as etapas de avaliação, a classificação, as vagas e as exigências de documentos, com a posição de cada parte no documento enviado e os códigos de
/// catálogo como foram escritos. Os blocos seguintes entram aqui à medida que a leitura os alcança.
/// </summary>
public sealed record DeclaracoesDoDocumento(IdentificacaoNoDocumento Identificacao, IReadOnlyList<FaseNoDocumento> Fases)
{
    /// <summary>As chamadas do cadastro de reserva na ordem do documento; o bloco ausente é a lista vazia.</summary>
    public IReadOnlyList<CadastroDeReservaInformado?> CadastroDeReserva { get; init; } = [];

    /// <summary>O código da manifestação de interesse como foi escrito; ausente é nulo, e quer dizer que o edital não a prevê.</summary>
    public string? ManifestacaoDeInteresse { get; init; }

    /// <summary>A prova presencial como o documento a traz; o bloco ausente é nulo, e quer dizer que o processo ainda não a declarou.</summary>
    public ProvaPresencialNoDocumento? ProvaPresencial { get; init; }

    /// <summary>A importação do resultado como o documento a traz; o bloco ausente é nulo, e quer dizer que o processo ainda não a declarou.</summary>
    public ImportacaoNoDocumento? Importacao { get; init; }

    /// <summary>As etapas de avaliação na ordem do documento; o bloco ausente é a lista vazia.</summary>
    public IReadOnlyList<EtapaNoDocumento> Etapas { get; init; } = [];

    /// <summary>A classificação como o documento a traz; o bloco ausente é nulo, e quer dizer que o processo ainda não a declarou.</summary>
    public ClassificacaoNoDocumento? Classificacao { get; init; }

    /// <summary>As vagas (opções de curso, distribuição, cascata, FADA e atendimento) como o documento as traz; nulo quando a declaração não as leu.</summary>
    public VagasNoDocumento? Vagas { get; init; }

    /// <summary>As exigências de documentos e as regras de comprovação como o documento as traz; o bloco ausente é nulo.</summary>
    public ExigenciasNoDocumento? Exigencias { get; init; }
}

/// <summary>
/// A identificação do processo como o documento a traz. O nome e o identificador legível vão como foram escritos, cada código de
/// catálogo também, e ausente é nulo.
/// </summary>
public sealed record IdentificacaoNoDocumento(
    string? Nome,
    string? TipoProcessoCodigo,
    string? UnidadeAdministradoraCodigo,
    OrigemCandidatos? OrigemCandidatos,
    string? LocalidadeCodigoIbge,
    string? IdentificadorLegivel,
    ReferenciaDeRegraNoDocumento? AlgoritmoContagemPrazo);

/// <summary>A regra do catálogo que o documento cita pelo código e pela versão; um dos dois pode faltar no rascunho.</summary>
public sealed record ReferenciaDeRegraNoDocumento(string? Codigo, string? Versao);
