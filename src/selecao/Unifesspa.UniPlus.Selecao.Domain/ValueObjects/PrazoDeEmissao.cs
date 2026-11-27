namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;

public sealed record PrazoDeEmissao
{
    private PrazoDeEmissao() { }

    private PrazoDeEmissao(EscolhaDoPrazoDeEmissao escolha, int? valor, UnidadeDoPrazoDeEmissao? unidade,
        DataDeReferenciaDoDocumento? referencia, EfeitoNaAnaliseDoDocumento? efeitoForaDoPrazo)
    {
        Escolha = escolha;
        Valor = valor;
        Unidade = unidade;
        Referencia = referencia;
        EfeitoForaDoPrazo = efeitoForaDoPrazo;
    }

    public EscolhaDoPrazoDeEmissao Escolha { get; }
    public int? Valor { get; }
    public UnidadeDoPrazoDeEmissao? Unidade { get; }
    public DataDeReferenciaDoDocumento? Referencia { get; }
    public EfeitoNaAnaliseDoDocumento? EfeitoForaDoPrazo { get; }

    public static Result<PrazoDeEmissao> Criar(EscolhaDoPrazoDeEmissao escolha, int? valor, UnidadeDoPrazoDeEmissao? unidade,
        DataDeReferenciaDoDocumento? referencia, EfeitoNaAnaliseDoDocumento? efeitoForaDoPrazo)
    {
        if (!Enum.IsDefined(escolha))
            return Falha("escolha", PrazoDeEmissaoErrorCodes.EscolhaInvalida, "A escolha do prazo de emissão não é reconhecida.");
        if (escolha == EscolhaDoPrazoDeEmissao.SemPrazo)
            return SemPrazo(valor, unidade, referencia, efeitoForaDoPrazo);
        List<FieldError> erros = [.. ValidarValorEUnidade(valor, unidade), .. ValidarReferencia(referencia), .. ValidarEfeito(efeitoForaDoPrazo)];

        return erros.Count > 0
            ? Result<PrazoDeEmissao>.ValidationFailure([erros[0]])
            : Result<PrazoDeEmissao>.Success(new(escolha, valor, unidade, referencia, efeitoForaDoPrazo));
    }

    /// <summary>Confere o valor, a unidade e o efeito de um prazo com prazo, que não dependem da data de referência.</summary>
    public static List<FieldError> ValidarDetalhesIndependentesDaReferencia(
        int? valor, UnidadeDoPrazoDeEmissao? unidade, EfeitoNaAnaliseDoDocumento? efeitoForaDoPrazo) =>
        [.. ValidarValorEUnidade(valor, unidade), .. ValidarEfeito(efeitoForaDoPrazo)];

    private static List<FieldError> ValidarValorEUnidade(int? valor, UnidadeDoPrazoDeEmissao? unidade)
    {
        List<FieldError> erros = [];
        if (valor is null or <= 0)
            erros.Add(Erro("valor", PrazoDeEmissaoErrorCodes.ValorInvalido, "Informe um prazo de emissão maior que zero."));
        if (unidade is null || !Enum.IsDefined(unidade.Value))
            erros.Add(Erro("unidade", PrazoDeEmissaoErrorCodes.UnidadeInvalida, "Informe uma unidade válida para o prazo de emissão."));
        else if (valor > LimiteDoValor(unidade.Value))
            erros.Add(Erro("valor", PrazoDeEmissaoErrorCodes.ValorInvalido, $"O prazo de emissão não pode passar de {LimiteDoValor(unidade.Value)} na unidade escolhida."));
        return erros;
    }

    private static List<FieldError> ValidarReferencia(DataDeReferenciaDoDocumento? referencia) =>
        referencia is null
            ? [Erro("referencia", DataDeReferenciaDoDocumentoErrorCodes.Ausente, "Informe a data de referência do prazo de emissão.")]
            : [];

    private static List<FieldError> ValidarEfeito(EfeitoNaAnaliseDoDocumento? efeitoForaDoPrazo) =>
        efeitoForaDoPrazo is null || !Enum.IsDefined(efeitoForaDoPrazo.Value)
            ? [Erro("efeitoForaDoPrazo", EfeitoNaAnaliseDoDocumentoErrorCodes.Invalido, "Informe um efeito válido para o documento fora do prazo.")]
            : [];

    // Cem anos em cada unidade: o maior prazo que ainda cabe numa data de emissão válida.
    private static int LimiteDoValor(UnidadeDoPrazoDeEmissao unidade) => unidade switch
    {
        UnidadeDoPrazoDeEmissao.Dias => 36500,
        UnidadeDoPrazoDeEmissao.Meses => 1200,
        _ => 100,
    };

    public DateOnly? DataMinimaDeEmissao(DateOnly dataDeReferencia) => Unidade switch
    {
        UnidadeDoPrazoDeEmissao.Dias => dataDeReferencia.AddDays(-Valor!.Value),
        UnidadeDoPrazoDeEmissao.Meses => dataDeReferencia.AddMonths(-Valor!.Value),
        UnidadeDoPrazoDeEmissao.Anos => dataDeReferencia.AddYears(-Valor!.Value),
        _ => null,
    };

    public bool AtendeAoPrazo(DateOnly dataDeEmissao, DateOnly dataDeReferencia) =>
        DataMinimaDeEmissao(dataDeReferencia) is not { } minima || dataDeEmissao >= minima;

    private static Result<PrazoDeEmissao> SemPrazo(int? valor, UnidadeDoPrazoDeEmissao? unidade,
        DataDeReferenciaDoDocumento? referencia, EfeitoNaAnaliseDoDocumento? efeito) =>
        valor is not null || unidade is not null || referencia is not null || efeito is not null
            ? Falha("", PrazoDeEmissaoErrorCodes.DetalhesSemPrazo, "Não informe detalhes quando não houver prazo de emissão.")
            : Result<PrazoDeEmissao>.Success(new(EscolhaDoPrazoDeEmissao.SemPrazo, null, null, null, null));

    private static Result<PrazoDeEmissao> Falha(string campo, string codigo, string mensagem) =>
        Result<PrazoDeEmissao>.ValidationFailure([Erro(campo, codigo, mensagem)]);

    private static FieldError Erro(string campo, string codigo, string mensagem) => new(campo, new DomainError(codigo, mensagem));
}
