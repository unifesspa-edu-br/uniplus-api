namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Cobertura de <see cref="ObterProcessoSeletivoQueryHandler.Handle"/> para a leitura tipada de
/// fatos coletados e regras de derivação (Story #987): a ordenação determinística, o contrato
/// "ausência é null, nunca []" e o round-trip do valor tipado.
/// </summary>
public sealed class ObterProcessoSeletivoQueryHandlerColetaDeFatosTests
{
    private static CondicaoRegraDerivacao CondicaoRegra(int clausula, string fato, Operador operador, object valor) =>
        CondicaoRegraDerivacao.Criar(clausula, fato, operador, JsonSerializer.SerializeToElement(valor)).Value!;

    private static ProcessoSeletivo NovoProcesso() =>
        ProcessoSeletivo.Criar("PS Query", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    private static async Task<ProcessoSeletivoDto> ProjetarAsync(ProcessoSeletivo processo)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        ProcessoSeletivoDto? dto = await ObterProcessoSeletivoQueryHandler.Handle(
            new ObterProcessoSeletivoQuery(processo.Id), repository, CancellationToken.None);
        dto.Should().NotBeNull();
        return dto!;
    }

    [Fact(DisplayName = "Fatos coletados são ordenados por ordem e projetam a apresentação e as regras no código canônico da escrita")]
    public async Task Fatos_Ordenados_ProjetamApresentacaoEObrigatoriedade()
    {
        IReadOnlyList<FatoColetadoDto> fatos = await ProjetarFatosAsync(ProcessoComDoisFatosForaDeOrdem());

        fatos.Select(f => f.FatoCodigo).Should().ContainInOrder("BAIXA_RENDA", "COR_RACA");
        fatos[0].Apresentacao.Rotulo.Should().Be("Baixa renda");
        fatos[0].Apresentacao.TipoRenderizacao.Should().Be("BOOLEANO", "o wire de leitura usa o mesmo código canônico do wire de escrita");
        fatos[0].Regras.Obrigatoriedade.Tipo.Should().Be("SEMPRE");
    }

    [Fact(DisplayName = "Fato sem exibição projeta null, nunca lista vazia")]
    public async Task Fatos_SemExibicao_ProjetaNull()
    {
        IReadOnlyList<FatoColetadoDto> fatos = await ProjetarFatosAsync(ProcessoComDoisFatosForaDeOrdem());

        fatos[0].Regras.Exibicao.Should().BeNull();
    }

    [Fact(DisplayName = "A exibição do fato faz round-trip: o valor tipado volta como booleano JSON")]
    public async Task Fatos_ComExibicao_ProjetaACondicao()
    {
        IReadOnlyList<FatoColetadoDto> fatos = await ProjetarFatosAsync(ProcessoComDoisFatosForaDeOrdem());

        IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>> exibicao = fatos[1].Regras.Exibicao!;
        CondicaoPrecondicaoDto condicao = exibicao.Should().ContainSingle().Which.Should().ContainSingle().Subject;
        condicao.Fato.Should().Be("BAIXA_RENDA");
        condicao.Operador.Should().Be("IGUAL");
        condicao.Valor.GetBoolean().Should().BeTrue();
    }

    private static async Task<IReadOnlyList<FatoColetadoDto>> ProjetarFatosAsync(ProcessoSeletivo processo)
    {
        ProcessoSeletivoDto dto = await ProjetarAsync(processo);
        return dto.Formularios.Should().ContainSingle().Which.FatosColetados;
    }

    /// <summary>BAIXA_RENDA na ordem 0, sem exibição; COR_RACA na ordem 1, com exibição citando o anterior. Entram fora de ordem: a projeção é quem ordena.</summary>
    private static ProcessoSeletivo ProcessoComDoisFatosForaDeOrdem()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FatoColetado baixaRenda = FatoColetadoDeTeste.Coletando("BAIXA_RENDA", 0).ComRotulo("Baixa renda").ComObrigatoriedade(Obrigatoriedade.Sempre).Construir();
        FatoColetado corRaca = FatoColetadoDeTeste.Coletando("COR_RACA", 1).ComRotulo("Cor ou raça").DoTipo(TipoRenderizacao.SelecaoUnica)
            .ExibidoQuando(ExibicaoDeTeste.Quando("BAIXA_RENDA", Operador.Igual, true)).Construir();
        processo.DefinirItens([corRaca, baixaRenda], finalidade: FinalidadeFormulario.Habilitacao).IsSuccess.Should().BeTrue();
        return processo;
    }

    [Fact(DisplayName = "Regras de derivação: config por codigoFato, regras por ordem; âncora projeta quando null")]
    public async Task Regras_OrdenadasEAncoraProjetaNull()
    {
        ConfiguracaoDerivacaoDto projetada = await ProjetarDerivacaoAsync();

        projetada.CodigoFato.Should().Be("REGIME_DE_VAGA");
        projetada.Regras.Select(r => r.Ordem).Should().ContainInOrder(0, 1);
        projetada.Regras[0].Contribui.Should().Be("AC");
        projetada.Regras[0].Quando.Should().BeNull("a regra âncora incondicional projeta quando null, nunca lista vazia");
    }

    [Fact(DisplayName = "A condição da regra de derivação projeta o fato, o operador e o valor tipado")]
    public async Task Regras_Condicional_ProjetaACondicao()
    {
        RegraDerivacaoDto regraCondicional = (await ProjetarDerivacaoAsync()).Regras[1];

        regraCondicional.Contribui.Should().Be("LB_PPI");
        CondicaoDerivacaoDto condicao = regraCondicional.Quando!.Should().ContainSingle().Which.Should().ContainSingle().Subject;
        condicao.Fato.Should().Be("COR_RACA");
        condicao.Operador.Should().Be("IGUAL");
        condicao.Valor.GetString().Should().Be("PRETA");
    }

    /// <summary>As regras entram fora de ordem: a projeção ordena por <c>Ordem</c>.</summary>
    private static async Task<ConfiguracaoDerivacaoDto> ProjetarDerivacaoAsync()
    {
        ProcessoSeletivo processo = NovoProcesso();
        RegraDerivacaoConfigurada ancora = RegraDerivacaoConfigurada.Criar(0, "AC", null).Value!;
        RegraDerivacaoConfigurada condicional = RegraDerivacaoConfigurada.Criar(1, "LB_PPI", [CondicaoRegra(0, "COR_RACA", Operador.Igual, "PRETA")]).Value!;
        processo.DefinirRegrasDerivacao([ConfiguracaoDerivacaoFato.Criar("REGIME_DE_VAGA", [condicional, ancora]).Value!]).IsSuccess.Should().BeTrue();

        return (await ProjetarAsync(processo)).RegrasDerivacao.Should().ContainSingle().Which;
    }

    [Fact(DisplayName = "Processo sem coleta nem derivação projeta listas vazias (não null)")]
    public async Task SemColeta_ListasVazias()
    {
        ProcessoSeletivo processo = NovoProcesso();

        ProcessoSeletivoDto dto = await ProjetarAsync(processo);

        dto.Formularios.Should().BeEmpty();
        dto.RegrasDerivacao.Should().BeEmpty();
    }
}
