namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using System.Text.Json;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// As exigências de documentos e as regras de comprovação como o documento as traz, copiadas sem conferir regra, cada item com a
/// posição que tinha na lista enviada. O bloco ausente é nulo, e quer dizer que o processo não declarou comprovação documental.
/// </summary>
public sealed record ExigenciasNoDocumento(
    IReadOnlyList<ExigenciaNoDocumento> Exigencias,
    IReadOnlyList<RegraDeComprovacaoNoDocumento> RegrasDeComprovacao);

/// <summary>
/// Uma exigência de documento: a chave que o autor escolheu, o tipo de documento pelo código e a fase, a etapa e o formulário em que
/// é exigida, também como foram escritos. O que o cadastro do tipo de documento dá (nome, categoria, proteção, finalidade e
/// hipótese legal) e o modelo que a exportação descreve são só de leitura e não entram.
/// </summary>
public sealed record ExigenciaNoDocumento(
    int PosicaoNoDocumento,
    string Chave,
    string? TipoDocumentoCodigo,
    string? FaseChave,
    string? EtapaChave,
    string? FormularioCodigo)
{
    public Aplicabilidade? Aplicabilidade { get; init; }

    /// <summary>Se a entrega é obrigatória; ausente é nulo, porque o domínio é que o recusa.</summary>
    public bool? Obrigatorio { get; init; }

    public bool AbrePrazoParaReenvio { get; init; }

    public ConsequenciaDoDescumprimento? ConsequenciaDoDescumprimento { get; init; }

    /// <summary>As alternativas de condições que tornam a exigência aplicável; as condições de cada alternativa valem juntas.</summary>
    public IReadOnlyList<IReadOnlyList<CondicaoSobreDadoNoDocumento>> Condicoes { get; init; } = [];

    public IReadOnlyList<BaseLegalNoDocumento> BasesLegais { get; init; } = [];

    public FormatosPermitidosNoDocumento? FormatosPermitidos { get; init; }

    public int? TamanhoMaximoBytes { get; init; }

    /// <summary>O identificador do modelo de documento enviado ao mesmo rascunho; ausente é nulo.</summary>
    public Guid? ModeloId { get; init; }

    public PrazoDeEmissaoNoDocumento? PrazoDeEmissao { get; init; }

    public ValidadeNoDocumento? Validade { get; init; }

    /// <summary>Os dados do candidato que o documento comprova, pelo código, como o autor os escreveu.</summary>
    public IReadOnlyList<string> DadosComprovados { get; init; } = [];
}

/// <summary>Uma condição sobre dado do candidato: o código do dado, o operador e o valor, como foram escritos; a leitura não confere nenhum dos três.</summary>
public sealed record CondicaoSobreDadoNoDocumento(string? CodigoDoDado, string? Operador, JsonElement? Valor);
