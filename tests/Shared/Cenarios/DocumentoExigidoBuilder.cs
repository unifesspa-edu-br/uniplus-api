namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;

using Microsoft.Extensions.Time.Testing;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

internal sealed class DocumentoExigidoBuilder
{
    private CopiaDoTipoDeDocumento _tipo = new(Guid.CreateVersion7(), "LAUDO", "Laudo médico", "CAT", ProtecaoDoDocumentoDeTeste.Pessoal);
    private MomentoDaExigencia _momento = new(Guid.CreateVersion7(), null, null);
    private AplicabilidadeDaExigencia _aplicabilidade = new(Aplicabilidade.Geral, []);
    private IndeferimentoDoDocumento _indeferimento = new(true, null, []);
    private PrazoDeEmissao? _prazoDeEmissao;
    private ValidadeDoDocumento? _validade;

    public FakeTimeProvider Relogio { get; } = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    public DocumentoExigidoBuilder ComTipo(CopiaDoTipoDeDocumento tipo) { _tipo = tipo; return this; }
    public DocumentoExigidoBuilder NaFase(Guid faseId, FinalidadeFormulario? finalidade = null) { _momento = new(faseId, null, finalidade); return this; }
    public DocumentoExigidoBuilder ComAplicabilidade(AplicabilidadeDaExigencia aplicabilidade) { _aplicabilidade = aplicabilidade; return this; }
    public DocumentoExigidoBuilder ComIndeferimento(IndeferimentoDoDocumento indeferimento) { _indeferimento = indeferimento; return this; }
    public DocumentoExigidoBuilder ComPrazoDeEmissao(PrazoDeEmissao prazo) { _prazoDeEmissao = prazo; return this; }
    public DocumentoExigidoBuilder ComValidade(ValidadeDoDocumento validade) { _validade = validade; return this; }

    public DocumentoExigido Construir() => DocumentoExigido.Criar(
        _tipo, _momento, _aplicabilidade, _indeferimento,
        new RequisitosDoDocumento(FormatosPermitidos.Criar(true, null).Value!, null, null, _prazoDeEmissao, _validade), null).Value!;
}
