namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;

public sealed record ValidadeDoDocumento
{
    private ValidadeDoDocumento() { }

    private ValidadeDoDocumento(EscolhaDaValidade escolha, DataDeReferenciaDoDocumento? referencia, EfeitoNaAnaliseDoDocumento? efeito)
    {
        Escolha = escolha;
        Referencia = referencia;
        EfeitoDoVencimento = efeito;
    }

    public EscolhaDaValidade Escolha { get; }
    public DataDeReferenciaDoDocumento? Referencia { get; }
    public EfeitoNaAnaliseDoDocumento? EfeitoDoVencimento { get; }

    public static Result<ValidadeDoDocumento> Criar(EscolhaDaValidade escolha, DataDeReferenciaDoDocumento? referencia,
        EfeitoNaAnaliseDoDocumento? efeitoDoVencimento)
    {
        if (!Enum.IsDefined(escolha))
            return Falha("escolha", ValidadeDoDocumentoErrorCodes.EscolhaInvalida, "A escolha da validade não é reconhecida.");
        if (escolha == EscolhaDaValidade.NaoExigeDocumentoValido)
            return SemExigencia(referencia, efeitoDoVencimento);
        if (referencia is null)
            return Falha("referencia", DataDeReferenciaDoDocumentoErrorCodes.Ausente, "Informe a data de referência da validade.");
        if (efeitoDoVencimento is null || !Enum.IsDefined(efeitoDoVencimento.Value))
            return Falha("efeitoDoVencimento", EfeitoNaAnaliseDoDocumentoErrorCodes.Invalido, "Informe um efeito válido para o documento vencido.");
        return Result<ValidadeDoDocumento>.Success(new(escolha, referencia, efeitoDoVencimento));
    }

    public bool AtendeAValidade(DateOnly vencimento, DateOnly dataDeReferencia) =>
        Escolha == EscolhaDaValidade.NaoExigeDocumentoValido || vencimento >= dataDeReferencia;

    private static Result<ValidadeDoDocumento> SemExigencia(DataDeReferenciaDoDocumento? referencia, EfeitoNaAnaliseDoDocumento? efeito) =>
        referencia is not null || efeito is not null
            ? Falha("", ValidadeDoDocumentoErrorCodes.DetalhesSemExigencia, "Não informe detalhes quando a validade não for exigida.")
            : Result<ValidadeDoDocumento>.Success(new(EscolhaDaValidade.NaoExigeDocumentoValido, null, null));

    private static Result<ValidadeDoDocumento> Falha(string campo, string codigo, string mensagem) =>
        Result<ValidadeDoDocumento>.ValidationFailure([new FieldError(campo, new DomainError(codigo, mensagem))]);
}
