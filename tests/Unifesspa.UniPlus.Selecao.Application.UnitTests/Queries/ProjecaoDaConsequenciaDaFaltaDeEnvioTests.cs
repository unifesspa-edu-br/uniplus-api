namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries.ApoioDaProjecaoDosDocumentosExigidos;

public sealed class ProjecaoDaConsequenciaDaFaltaDeEnvioTests
{
    [Fact]
    public void ProjetarFormularios_ComConsequencia_EmiteCodigoERotulo()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirFormulario(FinalidadeFormulario.Habilitacao, null, null, FormularioDeTeste.Paginas(), null, ConsequenciaDoDescumprimento.RetiraAModalidade)
            .IsSuccess.Should().BeTrue();

        FormularioDto formulario = ProjecaoDaColeta.ProjetarFormularios(processo).Single(static f => f.Codigo == "HABILITACAO");

        formulario.ConsequenciaDaFaltaDeEnvio!.Codigo.Should().Be("RETIRA_A_MODALIDADE");
        formulario.ConsequenciaDaFaltaDeEnvio.Rotulo.Should().Be("Retirada da modalidade");
    }

    [Fact]
    public void ProjetarFormularios_SemConsequencia_EmiteNulo()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirFormulario(FinalidadeFormulario.Habilitacao, null, null, FormularioDeTeste.Paginas()).IsSuccess.Should().BeTrue();

        ProjecaoDaColeta.ProjetarFormularios(processo).Single(static f => f.Codigo == "HABILITACAO")
            .ConsequenciaDaFaltaDeEnvio.Should().BeNull();
    }
}
