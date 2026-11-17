namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Queries.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// As opções que o cadastro institucional supre quando o catálogo as deixa nulas: a condição de
/// atendimento, o recurso de acessibilidade, o tipo de deficiência e os municípios do bônus.
/// </summary>
public sealed class OpcoesDoCadastroInstitucionalTests
{
    private readonly ICondicaoAtendimentoRepository _condicoes = Substitute.For<ICondicaoAtendimentoRepository>();
    private readonly IRecursoAcessibilidadeRepository _recursos = Substitute.For<IRecursoAcessibilidadeRepository>();
    private readonly ITipoDeficienciaRepository _tiposDeficiencia = Substitute.For<ITipoDeficienciaRepository>();
    private readonly IBaseLegalBonusRegionalRepository _basesDoBonus = Substitute.For<IBaseLegalBonusRegionalRepository>();

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

        await CompletarAsync(opcoes, [Fato("TIPO_A", FonteValoresFato.TiposDeficiencia), Fato("TIPO_B", FonteValoresFato.TiposDeficiencia)]);

        ValorSelecionavel[] esperado = [new("AUDITIVA", "Auditiva", 0), new("VISUAL", "Visual", 1)];
        opcoes["TIPO_A"].Should().Equal(esperado);
        opcoes["TIPO_B"].Should().Equal(esperado);
    }

    [Fact(DisplayName = "Os municípios do bônus saem sem repetição, em ordem alfabética sem acento, com a UF no rótulo")]
    public async Task Completar_CamposDoBonus_RecebemOsMunicipiosDeTodasAsBases()
    {
        _basesDoBonus.ListarVivasParaLeituraAsync(Arg.Any<CancellationToken>()).Returns(
            [Base("Portaria A", ("1504208", "Marabá", "PA"), ("1500404", "Água Azul do Norte", "PA")), Base("Portaria B", ("1504208", "Marabá", "PA"))]);
        Dictionary<string, IReadOnlyList<ValorSelecionavel>?> opcoes = new() { ["BONUS_A"] = null, ["BONUS_B"] = null };

        await CompletarAsync(opcoes, [Fato("BONUS_A", FonteValoresFato.MunicipiosBonus), Fato("BONUS_B", FonteValoresFato.MunicipiosBonus)]);

        ValorSelecionavel[] esperado = [new("1500404", "Água Azul do Norte/PA", 0), new("1504208", "Marabá/PA", 1)];
        opcoes["BONUS_A"].Should().Equal(esperado);
        opcoes["BONUS_B"].Should().Equal(esperado);
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
            ["BONUS"] = doCatalogo,
        };

        await CompletarAsync(opcoes, [Fato("TIPO", FonteValoresFato.TiposDeficiencia), Fato("BONUS", FonteValoresFato.MunicipiosBonus)]);

        opcoes.Values.Should().OnlyContain(valores => ReferenceEquals(valores, doCatalogo));
        await NenhumCadastroConsultadoAsync();
    }

    [Fact(DisplayName = "Sem campo que o cadastro supra, nenhum cadastro é consultado")]
    public async Task Completar_SemCampoDoCadastro_NaoConsultaOCadastro()
    {
        Dictionary<string, IReadOnlyList<ValorSelecionavel>?> opcoes = new() { ["OUTRO"] = null };

        await CompletarAsync(opcoes, [Fato("OUTRO", FonteValoresFato.Processo), Fato("TIPO_FORA_DO_MODELO", FonteValoresFato.TiposDeficiencia)]);

        opcoes["OUTRO"].Should().BeNull();
        opcoes.Should().ContainSingle();
        await NenhumCadastroConsultadoAsync();
    }

    private Task CompletarAsync(Dictionary<string, IReadOnlyList<ValorSelecionavel>?> opcoes, IReadOnlyList<FatoCandidato> fatos) =>
        new OpcoesDoCadastroInstitucional(_condicoes, _recursos, _tiposDeficiencia, _basesDoBonus).CompletarAsync(opcoes, fatos, CancellationToken.None);

    private async Task NenhumCadastroConsultadoAsync()
    {
        await _condicoes.DidNotReceiveWithAnyArgs().ListarVivosParaLeituraAsync(default);
        await _recursos.DidNotReceiveWithAnyArgs().ListarVivosParaLeituraAsync(default);
        await _tiposDeficiencia.DidNotReceiveWithAnyArgs().ListarVivosParaLeituraAsync(default);
        await _basesDoBonus.DidNotReceiveWithAnyArgs().ListarVivasParaLeituraAsync(default);
    }

    private static CondicaoAtendimentoEspecializado Condicao(string codigo, string nome) => CondicaoAtendimentoEspecializado.Criar(codigo, nome, null).Value!;

    private static RecursoAcessibilidade Recurso(string codigo, string nome) => RecursoAcessibilidade.Criar(codigo, nome, "Recurso de acessibilidade").Value!;

    private static TipoDeficiencia Tipo(string codigo, string nome) => TipoDeficiencia.Criar(codigo, nome, "Tipo de deficiência").Value!;

    private static BaseLegalBonusRegional Base(string identificacao, params (string CodigoIbge, string Nome, string Uf)[] municipios) =>
        BaseLegalBonusRegional.Criar("PORTARIA", identificacao, "Institui inclusão regional", municipios.Select(static m => ((string?)m.CodigoIbge, (string?)m.Nome, (string?)m.Uf))).Value!;

    private static FatoCandidato Fato(string codigo, FonteValoresFato fonte) =>
        CriacaoDoFato.CriarDoAdministrador(
            codigo, codigo, "Descrição do fato.", DominioFato.Categorico, CardinalidadeFato.Escalar, fonte, null, "INSCRICAO", EscopoFato.Candidato,
            ClassificacaoDeProtecao.Pessoal, "Verificação dos requisitos do processo seletivo.", HipoteseLegalTratamento.CumprimentoObrigacaoLegal).Value!;
}
