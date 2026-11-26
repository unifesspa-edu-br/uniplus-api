namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Queries.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// As opções que o cadastro institucional supre quando o catálogo as deixa nulas: a condição de
/// atendimento, o recurso de acessibilidade, o tipo de deficiência e os municípios do FADA.
/// </summary>
public sealed class OpcoesDoCadastroInstitucionalTests
{
    private readonly ICondicaoAtendimentoRepository _condicoes = Substitute.For<ICondicaoAtendimentoRepository>();
    private readonly IRecursoAcessibilidadeRepository _recursos = Substitute.For<IRecursoAcessibilidadeRepository>();
    private readonly ITipoDeficienciaRepository _tiposDeficiencia = Substitute.For<ITipoDeficienciaRepository>();
    private readonly IBaseLegalDoFadaRepository _basesDoFada = Substitute.For<IBaseLegalDoFadaRepository>();

    [Fact(DisplayName = "A condição de atendimento sem opções recebe o cadastro vivo, por código")]
    public async Task Completar_CondicaoSemOpcoes_RecebeOCadastroPorCodigo()
    {
        _condicoes.ListarVivosParaLeituraAsync(Arg.Any<CancellationToken>()).Returns(
            [Condicao("TEA", "Autismo"), Condicao("BAIXA_VISAO", "Baixa visão")]);
        Dictionary<string, IReadOnlyList<ValorSelecionavel>?> opcoes = new() { ["CONDICAO_ATENDIMENTO"] = null };

        await CompletarAsync(opcoes, []);

        opcoes["CONDICAO_ATENDIMENTO"].Should().Equal(new ValorSelecionavel("BAIXA_VISAO", "Baixa visão", 0), new ValorSelecionavel("TEA", "Autismo", 1));
    }

    [Fact(DisplayName = "O recurso de acessibilidade sem opções recebe o cadastro vivo, por código")]
    public async Task Completar_RecursoSemOpcoes_RecebeOCadastroPorCodigo()
    {
        _recursos.ListarVivosParaLeituraAsync(Arg.Any<CancellationToken>()).Returns(
            [Recurso("SALA_TERREA", "Sala térrea"), Recurso("LEDOR", "Ledor")]);
        Dictionary<string, IReadOnlyList<ValorSelecionavel>?> opcoes = new() { ["RECURSO_ACESSIBILIDADE"] = null };

        await CompletarAsync(opcoes, []);

        opcoes["RECURSO_ACESSIBILIDADE"].Should().Equal(new ValorSelecionavel("LEDOR", "Ledor", 0), new ValorSelecionavel("SALA_TERREA", "Sala térrea", 1));
    }

    [Fact(DisplayName = "Todo campo da fonte de tipos de deficiência recebe o mesmo cadastro vivo, por código")]
    public async Task Completar_CamposDeTipoDeDeficiencia_RecebemOCadastroPorCodigo()
    {
        _tiposDeficiencia.ListarVivosParaLeituraAsync(Arg.Any<CancellationToken>()).Returns(
            [Tipo("VISUAL", "Visual"), Tipo("AUDITIVA", "Auditiva")]);
        Dictionary<string, IReadOnlyList<ValorSelecionavel>?> opcoes = new() { ["TIPO_A"] = null, ["TIPO_B"] = null };

        await CompletarAsync(opcoes, [Fato("TIPO_A", FonteValoresFato.TiposDeDeficiencia), Fato("TIPO_B", FonteValoresFato.TiposDeDeficiencia)]);

        ValorSelecionavel[] esperado = [new("AUDITIVA", "Auditiva", 0), new("VISUAL", "Visual", 1)];
        opcoes["TIPO_A"].Should().Equal(esperado);
        opcoes["TIPO_B"].Should().Equal(esperado);
    }

    [Fact(DisplayName = "Os municípios do FADA saem sem repetição, em ordem alfabética sem acento, com a UF no rótulo")]
    public async Task Completar_CamposDoFada_RecebemOsMunicipiosDeTodasAsBases()
    {
        _basesDoFada.ListarVivasParaLeituraAsync(Arg.Any<CancellationToken>()).Returns(
            [Base("Portaria A", ("1504208", "Marabá", "PA"), ("1500404", "Água Azul do Norte", "PA")), Base("Portaria B", ("1504208", "Marabá", "PA"))]);
        Dictionary<string, IReadOnlyList<ValorSelecionavel>?> opcoes = new() { ["FADA_A"] = null, ["FADA_B"] = null };

        await CompletarAsync(opcoes, [Fato("FADA_A", FonteValoresFato.MunicipiosDoFada), Fato("FADA_B", FonteValoresFato.MunicipiosDoFada)]);

        ValorSelecionavel[] esperado = [new("1500404", "Água Azul do Norte/PA", 0), new("1504208", "Marabá/PA", 1)];
        opcoes["FADA_A"].Should().Equal(esperado);
        opcoes["FADA_B"].Should().Equal(esperado);
    }

    [Fact(DisplayName = "A opção que o catálogo já definiu não é substituída nem consulta o cadastro")]
    public async Task Completar_OpcoesJaDefinidas_NaoConsultaOCadastro()
    {
        ValorSelecionavel[] doCatalogo = [new("X", "Do catálogo", 0)];
        Dictionary<string, IReadOnlyList<ValorSelecionavel>?> opcoes = new()
        {
            ["CONDICAO_ATENDIMENTO"] = doCatalogo,
            ["RECURSO_ACESSIBILIDADE"] = doCatalogo,
            ["TIPO"] = doCatalogo,
            ["FADA"] = doCatalogo,
        };

        await CompletarAsync(opcoes, [Fato("TIPO", FonteValoresFato.TiposDeDeficiencia), Fato("FADA", FonteValoresFato.MunicipiosDoFada)]);

        opcoes.Values.Should().OnlyContain(valores => ReferenceEquals(valores, doCatalogo));
        await NenhumCadastroConsultadoAsync();
    }

    [Fact(DisplayName = "Sem campo que o cadastro supra, nenhum cadastro é consultado")]
    public async Task Completar_SemCampoDoCadastro_NaoConsultaOCadastro()
    {
        Dictionary<string, IReadOnlyList<ValorSelecionavel>?> opcoes = new() { ["OUTRO"] = null };

        await CompletarAsync(opcoes, [Fato("OUTRO", FonteValoresFato.OpcoesDoProcesso), Fato("TIPO_FORA_DO_MODELO", FonteValoresFato.TiposDeDeficiencia)]);

        opcoes["OUTRO"].Should().BeNull();
        opcoes.Should().ContainSingle();
        await NenhumCadastroConsultadoAsync();
    }

    private Task CompletarAsync(Dictionary<string, IReadOnlyList<ValorSelecionavel>?> opcoes, IReadOnlyList<FatoCandidato> fatos) =>
        new OpcoesDoCadastroInstitucional(_condicoes, _recursos, _tiposDeficiencia, _basesDoFada).CompletarAsync(opcoes, fatos, CancellationToken.None);

    private async Task NenhumCadastroConsultadoAsync()
    {
        await _condicoes.DidNotReceiveWithAnyArgs().ListarVivosParaLeituraAsync(default);
        await _recursos.DidNotReceiveWithAnyArgs().ListarVivosParaLeituraAsync(default);
        await _tiposDeficiencia.DidNotReceiveWithAnyArgs().ListarVivosParaLeituraAsync(default);
        await _basesDoFada.DidNotReceiveWithAnyArgs().ListarVivasParaLeituraAsync(default);
    }

    private static CondicaoAtendimentoEspecializado Condicao(string codigo, string nome) => CondicaoAtendimentoEspecializado.Criar(codigo, nome, null).Value!;

    private static RecursoAcessibilidade Recurso(string codigo, string nome) => RecursoAcessibilidade.Criar(codigo, nome, "Recurso de acessibilidade").Value!;

    private static TipoDeficiencia Tipo(string codigo, string nome) => TipoDeficiencia.Criar(codigo, nome, "Tipo de deficiência").Value!;

    private static BaseLegalDoFada Base(string identificacao, params (string CodigoIbge, string Nome, string Uf)[] municipios) =>
        BaseLegalDoFada.Criar("PORTARIA", identificacao, "Institui inclusão regional", municipios.Select(static m => ((string?)m.CodigoIbge, (string?)m.Nome, (string?)m.Uf))).Value!;

    private static FatoCandidato Fato(string codigo, FonteValoresFato fonte) =>
        CriacaoDoFato.CriarDoAdministrador(
            new DescritivoDoFato(codigo, codigo, "Descrição do fato."),
            new DefinicaoDoFato(TipoDoDado.Opcao, QuantidadeDeValores.ValorUnico, fonte, null, "INSCRICAO", EscopoFato.Candidato, ClassificacaoDeProtecao.Pessoal, "Verificação dos requisitos do processo seletivo.", HipoteseLegalTratamento.CumprimentoObrigacaoLegal)).Value!;
}
