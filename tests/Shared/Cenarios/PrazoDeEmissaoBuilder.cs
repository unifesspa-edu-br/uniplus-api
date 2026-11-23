namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>O prazo de emissão; sem dizer nada, noventa dias contados do fim das inscrições, com a recusa do documento.</summary>
internal sealed class PrazoDeEmissaoBuilder
{
    private EscolhaDoPrazoDeEmissao _escolha = EscolhaDoPrazoDeEmissao.ComPrazo;
    private int? _valor = 90;
    private UnidadeDoPrazoDeEmissao? _unidade = UnidadeDoPrazoDeEmissao.Dias;
    private DataDeReferenciaDoDocumento? _referencia = new DataDeReferenciaBuilder().Construir();
    private EfeitoNaAnaliseDoDocumento? _efeito = EfeitoNaAnaliseDoDocumento.RecusarDocumento;

    public PrazoDeEmissaoBuilder SemPrazo()
    {
        _escolha = EscolhaDoPrazoDeEmissao.SemPrazo;
        _valor = null;
        _unidade = null;
        _referencia = null;
        _efeito = null;
        return this;
    }

    public PrazoDeEmissaoBuilder De(int valor, UnidadeDoPrazoDeEmissao unidade) { _valor = valor; _unidade = unidade; return this; }
    public PrazoDeEmissaoBuilder Referenciado(DataDeReferenciaDoDocumento referencia) { _referencia = referencia; return this; }
    public PrazoDeEmissaoBuilder ComEfeito(EfeitoNaAnaliseDoDocumento efeito) { _efeito = efeito; return this; }

    public PrazoDeEmissao Construir()
    {
        Result<PrazoDeEmissao> prazo = PrazoDeEmissao.Criar(_escolha, _valor, _unidade, _referencia, _efeito);
        return prazo.IsSuccess ? prazo.Value! : throw new InvalidOperationException(prazo.Error!.Message);
    }
}
