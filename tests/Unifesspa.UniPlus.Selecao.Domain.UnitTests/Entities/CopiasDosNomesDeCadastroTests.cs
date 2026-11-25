namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class CopiasDosNomesDeCadastroTests
{
    [Fact]
    public void Criar_FaseComNomeDoCadastro_CopiaNome()
    {
        FaseCronograma fase = new ModeloDeFase("AVALIACAO", 1) { Nome = "Avaliação fictícia" }.Criar();

        fase.TipoFaseNome.Should().Be("Avaliação fictícia");
    }

    [Fact]
    public void AtualizarDe_FaseComNomeAlterado_RecopiaNome()
    {
        ModeloDeFase modelo = new("AVALIACAO", 1) { Nome = "Nome anterior" };
        FaseCronograma fase = modelo.Criar();
        FaseCronograma alterada = (modelo with { Nome = "Nome atualizado" }).Criar();

        fase.AtualizarDe(alterada, []);

        fase.TipoFaseNome.Should().Be("Nome atualizado");
    }

    [Fact]
    public void Criar_FormularioSemModelo_NaoGuardaNome()
    {
        Result<FormularioProcesso> resultado = FormularioProcesso.Criar(FinalidadeFormulario.Inscricao, null, null, FormularioDeTeste.Paginas(FinalidadeFormulario.Inscricao), modeloOrigemNome: "Nome ignorado");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.ModeloOrigemNome.Should().BeNull();
    }
}
