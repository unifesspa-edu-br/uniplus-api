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
        if (valor is null or <= 0)
            return Falha("valor", PrazoDeEmissaoErrorCodes.ValorInvalido, "Informe um prazo de emissão maior que zero.");
        if (unidade is null || !Enum.IsDefined(unidade.Value))
            return Falha("unidade", PrazoDeEmissaoErrorCodes.UnidadeInvalida, "Informe uma unidade válida para o prazo de emissão.");
        if (referencia is null)
            return Falha("referencia", DataDeReferenciaDoDocumentoErrorCodes.Ausente, "Informe a data de referência do prazo de emissão.");
        if (efeitoForaDoPrazo is null || !Enum.IsDefined(efeitoForaDoPrazo.Value))
            return Falha("efeitoForaDoPrazo", EfeitoNaAnaliseDoDocumentoErrorCodes.Invalido, "Informe um efeito válido para o documento fora do prazo.");
        return Result<PrazoDeEmissao>.Success(new(escolha, valor, unidade, referencia, efeitoForaDoPrazo));
    }

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
        Result<PrazoDeEmissao>.ValidationFailure([new FieldError(campo, new DomainError(codigo, mensagem))]);
}
