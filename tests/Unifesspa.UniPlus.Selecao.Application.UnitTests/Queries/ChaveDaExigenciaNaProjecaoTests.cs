namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class ChaveDaExigenciaNaProjecaoTests
{
    [Fact]
    public async Task Projetar_ExigenciaComChave_DevolveChaveGravada()
    {
        (ProcessoSeletivo processo, FaseCronograma fase) = ApoioDaProjecaoDosDocumentosExigidos.ProcessoComUmaFase();
        DocumentoExigido documento = new ApoioDaProjecaoDosDocumentosExigidos.ExigenciaParaProjetar().NaFase(fase.Id);
        processo.DefinirDocumentosExigidosNosFormulariosMinimos([RegraDeComprovacao.CriarRegraDeDocumento(documento, 0).Value!]).IsSuccess.Should().BeTrue();

        ProcessoSeletivoDto resultado = await ApoioDaProjecaoDosDocumentosExigidos.ProjetarAsync(processo);

        resultado.DocumentosExigidos.Should().ContainSingle().Which.Chave.Should().Be(documento.Chave.Valor);
        resultado.RegrasDeComprovacao.Should().ContainSingle().Which.Documento!.Chave.Should().Be(documento.Chave.Valor);
    }
}
