namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries.ApoioDaProjecaoDosDocumentosExigidos;

public sealed class ProjecaoDaConsequenciaTests
{
    [Fact]
    public async Task ProjetarDocumento_ComConsequencia_EmiteCodigoERotulo()
    {
        DocumentoExigidoDto documento = await ProjetarAUnicaExigenciaAsync(new ExigenciaParaProjetar { Consequencia = "ELIMINA_DO_PROCESSO" });

        documento.Indeferimento.ConsequenciaDoDescumprimento!.Codigo.Should().Be("ELIMINA_DO_PROCESSO");
        documento.Indeferimento.ConsequenciaDoDescumprimento.Rotulo.Should().Be("Eliminação do processo");
    }

    [Fact]
    public async Task ProjetarDocumento_SemConsequencia_EmiteNulo()
    {
        DocumentoExigidoDto documento = await ProjetarAUnicaExigenciaAsync(new ExigenciaParaProjetar());

        documento.Indeferimento.ConsequenciaDoDescumprimento.Should().BeNull();
    }

    [Fact]
    public void ProjetarGrupo_ComConsequencia_EmiteCodigoERotulo()
    {
        (ProcessoSeletivo processo, FaseCronograma fase) = ProcessoComUmaFase();
        RegraDeComprovacao primeiro = RegraDeComprovacao.CriarRegraDeDocumento(new ExigenciaParaProjetar().NaFase(fase.Id), 0).Value!;
        RegraDeComprovacao segundo = RegraDeComprovacao.CriarRegraDeDocumento(new ExigenciaParaProjetar().NaFase(fase.Id), 1).Value!;
        RegraDeComprovacao grupo = RegraDeComprovacao.CriarGrupo(new(FormaDeComprovacao.QuantidadeMinimaDeItens, 1, "ELIMINA_DO_PROCESSO"), 0, [], [primeiro, segundo]).Value!;
        processo.DefinirDocumentosExigidosNosFormulariosMinimos([grupo]).IsSuccess.Should().BeTrue();

        RegraDeComprovacaoDto projetado = ProjecaoDaColeta.ProjetarRegrasDeComprovacaoSemGrupo(processo).Should().ContainSingle().Which;
        projetado.Indeferimento.ConsequenciaDoDescumprimento!.Codigo.Should().Be("ELIMINA_DO_PROCESSO");
        projetado.Indeferimento.ConsequenciaDoDescumprimento.Rotulo.Should().Be("Eliminação do processo");
        projetado.Itens[0].Indeferimento.ConsequenciaDoDescumprimento.Should().BeNull();
    }
}
