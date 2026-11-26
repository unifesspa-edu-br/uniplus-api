namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O formulário apresentado do rascunho é pedido pelo código do formulário e diz o código, a fase e a obrigatoriedade
/// que o processo declara; o código que o processo não tem, ou que não segue o formato, não encontra formulário.
/// </summary>
public sealed class ObterFormularioApresentadoDoRascunhoTests
{
    private const string CodigoDaHabilitacao = "HABILITACAO";

    [Fact(DisplayName = "O formulário da inscrição do rascunho diz o código, a fase que coleta inscrição e que é obrigatório")]
    public async Task Handle_FormularioDaInscricao_DizCodigoFaseEObrigatoriedade()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS 2026 — Formulário do rascunho", out Guid faseDaInscricao);

        Result<FormularioApresentadoDoProcessoDto> resultado = await ObterAsync(processo, FormularioDeTeste.CodigoDaInscricao);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Codigo.Should().Be(FormularioDeTeste.CodigoDaInscricao);
        resultado.Value.FaseId.Should().Be(faseDaInscricao);
        resultado.Value.Obrigatorio.Should().BeTrue();
    }

    [Fact(DisplayName = "O formulário do rascunho que o processo declara com outro código é encontrado só por esse código")]
    public async Task Handle_FormularioDeOutroCodigo_EncontradoSoPeloCodigo()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS 2026 — Habilitação do rascunho");
        processo.DefinirItens(
            [FatoColetadoDeTeste.Coletando("CONDICAO_DA_HABILITACAO", 0).DoTipo(TipoDeCampo.SimOuNao).Construir()], papel: PapelDoFormularioDeTeste.Habilitacao).IsSuccess.Should().BeTrue();

        Result<FormularioApresentadoDoProcessoDto> daHabilitacao = await ObterAsync(processo, CodigoDaHabilitacao, CondicaoDaHabilitacaoNoCatalogo());

        daHabilitacao.IsSuccess.Should().BeTrue(daHabilitacao.Error?.Message);
        daHabilitacao.Value!.Codigo.Should().Be(CodigoDaHabilitacao);
        daHabilitacao.Value.Formulario.Coleta.Itens.Select(static i => i.FatoCodigo).Should().Equal("CONDICAO_DA_HABILITACAO");
    }

    [Theory(DisplayName = "O código que o processo não declara ou que não segue o formato não encontra formulário")]
    [InlineData("COMPLEMENTO_DA_INSCRICAO")]
    [InlineData("inscricao")]
    [InlineData("1INSCRICAO")]
    public async Task Handle_CodigoSemFormulario_NaoEncontrado(string codigo)
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS 2026 — Sem o formulário");

        Result<FormularioApresentadoDoProcessoDto> resultado = await ObterAsync(processo, codigo);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    private static FatoCandidatoView CondicaoDaHabilitacaoNoCatalogo() => CatalogoDoConjuntoBasico.Fatos.Single(static f => f.Codigo == "DESEJA_NOME_SOCIAL") with
    {
        Codigo = "CONDICAO_DA_HABILITACAO",
        Binding = "CAMPO_FORMULARIO:CONDICAO_DA_HABILITACAO",
    };

    private static Task<Result<FormularioApresentadoDoProcessoDto>> ObterAsync(
        ProcessoSeletivo processo, string formularioCodigo, params FatoCandidatoView[] fatosDoCatalogo)
    {
        IProcessoSeletivoRepository repositorio = Substitute.For<IProcessoSeletivoRepository>();
        repositorio.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        IFatoCandidatoReader catalogo = Substitute.For<IFatoCandidatoReader>();
        catalogo.ListarAsync(Arg.Any<CancellationToken>()).Returns(CatalogoDoConjuntoBasico.Com(fatosDoCatalogo));

        return ObterFormularioApresentadoDoRascunhoQueryHandler.Handle(
            new ObterFormularioApresentadoDoRascunhoQuery(processo.Id, formularioCodigo), repositorio, catalogo, new ResolvedorFusoDeTeste(), CancellationToken.None);
    }
}
