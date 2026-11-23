namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Os requisitos de um documento do modelo na forma de entrada dos testes: sem dizer nada, nenhuma escolha foi
/// feita. Cada método devolve um construtor novo, então um ponto de partida compartilhado não vaza para os demais.
/// </summary>
internal sealed record RequisitosDoDocumentoDoModeloBuilder
{
    private PrazoDeEmissaoDoModeloInput? Prazo { get; init; }

    private ValidadeDoDocumentoDoModeloInput? Validade { get; init; }

    /// <summary>O modelo ainda não escolheu o prazo de emissão nem a validade.</summary>
    public static RequisitosDoDocumentoDoModeloBuilder AindaNaoEscolhidos() => new();

    /// <summary>A referência que conta a partir do início da fase do cronograma, pelo código dela.</summary>
    public static DataDeReferenciaDoDocumentoDoModeloInput NoInicioDaFase(string faseCodigo) => new("INICIO_DA_FASE", null, faseCodigo);

    /// <summary>A referência que conta a partir do fim da fase do cronograma, pelo código dela.</summary>
    public static DataDeReferenciaDoDocumentoDoModeloInput NoFimDaFase(string faseCodigo) => new("FIM_DA_FASE", null, faseCodigo);

    /// <summary>A referência que conta a partir do fim das inscrições, sem fase nem data.</summary>
    public static DataDeReferenciaDoDocumentoDoModeloInput NoFimDasInscricoes() => new("FIM_DAS_INSCRICOES", null, null);

    /// <summary>Prazo com valor, unidade, referência e o efeito do documento emitido fora do prazo.</summary>
    public RequisitosDoDocumentoDoModeloBuilder ComPrazoDe(
        int valor, string unidade, DataDeReferenciaDoDocumentoDoModeloInput referencia, string efeito = "RECUSAR_DOCUMENTO") =>
        this with { Prazo = new PrazoDeEmissaoDoModeloInput("COM_PRAZO", valor, unidade, referencia, efeito) };

    /// <summary>O prazo de emissão com o código de escolha dado, sem nenhum detalhe.</summary>
    public RequisitosDoDocumentoDoModeloBuilder ComEscolhaDePrazo(string? escolha) =>
        this with { Prazo = new PrazoDeEmissaoDoModeloInput(escolha, null, null, null, null) };

    /// <summary>O modelo escolhe que o documento não tem prazo de emissão.</summary>
    public RequisitosDoDocumentoDoModeloBuilder SemPrazo() => ComEscolhaDePrazo("SEM_PRAZO");

    /// <summary>O prazo de emissão como o pedido o traz, com cada código e a referência à escolha.</summary>
    public RequisitosDoDocumentoDoModeloBuilder ComPrazo(PrazoDeEmissaoDoModeloInput prazo) => this with { Prazo = prazo };

    /// <summary>Exige documento válido na data da referência, com o efeito do documento vencido.</summary>
    public RequisitosDoDocumentoDoModeloBuilder ExigindoValidadeNa(
        DataDeReferenciaDoDocumentoDoModeloInput referencia, string efeito = "ACEITAR_COM_AVISO") =>
        this with { Validade = new ValidadeDoDocumentoDoModeloInput("EXIGE_DOCUMENTO_VALIDO", referencia, efeito) };

    /// <summary>A validade como o pedido a traz, com cada código e a referência à escolha.</summary>
    public RequisitosDoDocumentoDoModeloBuilder ComValidade(ValidadeDoDocumentoDoModeloInput validade) => this with { Validade = validade };

    public RequisitosDoDocumentoDoModeloInput Construir() => new(Prazo, Validade);
}
