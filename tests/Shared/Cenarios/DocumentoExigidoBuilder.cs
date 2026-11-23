namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;

using Microsoft.Extensions.Time.Testing;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Chaves;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

internal sealed class DocumentoExigidoBuilder
{
    private CopiaDoTipoDeDocumento _tipo = new(Guid.CreateVersion7(), "LAUDO", "Laudo médico", "CAT", ProtecaoDoDocumentoDeTeste.Pessoal);
    private MomentoDaExigencia _momento = new(Guid.CreateVersion7(), null, null);
    private AplicabilidadeDaExigencia _aplicabilidade = new(Aplicabilidade.Geral, []);
    private IndeferimentoDoDocumento _indeferimento = new(true, null, []);
    private static readonly ChavesEmUso Chaves = new();
    private ChaveLocal? _chave;
    private RequisitosDoDocumento? _requisitos;
    private IReadOnlyList<string>? _fatos;
    private PrazoDeEmissao? _prazoDeEmissao;
    private ValidadeDoDocumento? _validade;

    public FakeTimeProvider Relogio { get; } = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    public DocumentoExigidoBuilder ComTipo(CopiaDoTipoDeDocumento tipo) { _tipo = tipo; return this; }
    public DocumentoExigidoBuilder NaFase(Guid faseId, FinalidadeFormulario? finalidade = null) { _momento = new(faseId, null, finalidade); return this; }
    public DocumentoExigidoBuilder ComAplicabilidade(AplicabilidadeDaExigencia aplicabilidade) { _aplicabilidade = aplicabilidade; return this; }
    public DocumentoExigidoBuilder ComIndeferimento(IndeferimentoDoDocumento indeferimento) { _indeferimento = indeferimento; return this; }
    public DocumentoExigidoBuilder ComPrazoDeEmissao(PrazoDeEmissao prazo) { _prazoDeEmissao = prazo; return this; }
    public DocumentoExigidoBuilder ComValidade(ValidadeDoDocumento validade) { _validade = validade; return this; }

    public DocumentoExigidoBuilder ComChave(ChaveLocal chave) { _chave = chave; return this; }
    public DocumentoExigidoBuilder ComMomento(MomentoDaExigencia momento) { _momento = momento; return this; }
    public DocumentoExigidoBuilder ComRequisitos(RequisitosDoDocumento requisitos) { _requisitos = requisitos; return this; }
    public DocumentoExigidoBuilder ComFatos(IReadOnlyList<string>? fatos) { _fatos = fatos; return this; }

    public Result<DocumentoExigido> Criar()
    {
        ChaveLocal chave;
        lock (Chaves)
        {
            chave = _chave ?? FormacaoDeChaveLocal.Formar("exigencia", _tipo.Codigo, Chaves);
            Chaves.Adicionar(chave);
        }
        RequisitosDoDocumento requisitos = _requisitos ?? new(FormatosPermitidos.Criar(true, null).Value!, null, null, _prazoDeEmissao, _validade);
        return DocumentoExigido.Criar(chave, new(_tipo, _momento, _aplicabilidade, _indeferimento, requisitos, _fatos));
    }

    public DocumentoExigido Construir() => Criar().Value!;
}
