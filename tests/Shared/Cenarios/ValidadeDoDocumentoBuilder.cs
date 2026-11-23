namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>A validade; sem dizer nada, exige documento válido no fim das inscrições, com a recusa do documento.</summary>
internal sealed class ValidadeDoDocumentoBuilder
{
    private EscolhaDaValidade _escolha = EscolhaDaValidade.ExigeDocumentoValido;
    private DataDeReferenciaDoDocumento? _referencia = new DataDeReferenciaBuilder().Construir();
    private EfeitoNaAnaliseDoDocumento? _efeito = EfeitoNaAnaliseDoDocumento.RecusarDocumento;

    public ValidadeDoDocumentoBuilder NaoExige()
    {
        _escolha = EscolhaDaValidade.NaoExigeDocumentoValido;
        _referencia = null;
        _efeito = null;
        return this;
    }

    public ValidadeDoDocumentoBuilder Referenciada(DataDeReferenciaDoDocumento referencia) { _referencia = referencia; return this; }
    public ValidadeDoDocumentoBuilder ComEfeito(EfeitoNaAnaliseDoDocumento efeito) { _efeito = efeito; return this; }

    public ValidadeDoDocumento Construir()
    {
        Result<ValidadeDoDocumento> validade = ValidadeDoDocumento.Criar(_escolha, _referencia, _efeito);
        return validade.IsSuccess ? validade.Value! : throw new InvalidOperationException(validade.Error!.Message);
    }
}
