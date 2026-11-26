namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
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
    public void Criar_FormularioSemModelo_NaoTemOrigem()
    {
        Result<FormularioProcesso> resultado = FormularioProcesso.Criar(FormularioDeTeste.DeclaradoNoPapel(PapelDoFormularioDeTeste.Inscricao, null), new CronogramaDoProcesso([]), null, FormularioDeTeste.Paginas(PapelDoFormularioDeTeste.Inscricao));

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.ModeloDeOrigem.Should().BeNull();
    }

    [Fact]
    public void Criar_FormularioComModelo_CopiaONomeSemEspacosNasPontas()
    {
        ModeloDeOrigem origem = new(Guid.CreateVersion7(), "MODELO_FICTICIO", "  Modelo fictício  ");

        Result<FormularioProcesso> resultado = FormularioProcesso.Criar(FormularioDeTeste.DeclaradoNoPapel(PapelDoFormularioDeTeste.Inscricao, null), new CronogramaDoProcesso([]), null, FormularioDeTeste.Paginas(PapelDoFormularioDeTeste.Inscricao), origem);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.ModeloDeOrigem!.Nome.Should().Be("Modelo fictício");
    }
}
