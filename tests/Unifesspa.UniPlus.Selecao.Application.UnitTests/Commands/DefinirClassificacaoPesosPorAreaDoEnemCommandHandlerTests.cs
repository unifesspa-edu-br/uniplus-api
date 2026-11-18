namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDaClassificacao;

/// <summary>
/// A resolução de pesos por área do ENEM da classificação com cálculo local: é conferida na forma e no
/// cadastro, congelada por cópia e recusada, sem consultar o cadastro, quando não cabe ao tipo de classificação.
/// </summary>
public sealed class DefinirClassificacaoPesosPorAreaDoEnemCommandHandlerTests
{
    private static void DeveTerCongeladoOsPesosDaAreaDeSaude(ConfiguracaoClassificacao classificacao)
    {
        GrupoPesoAreaEnemCongelado saude = classificacao.QuadroPesoAreaEnem.Single(g => g.GrupoAreaEnem.Codigo == "SAUDE_E_BIOLOGICAS");
        saude.BaseLegal.Should().Be("Resolução nº 805/2024/Consepe – Anexo I (Saúde e Biológicas)");
        saude.Areas.Select(a => (a.Codigo, a.Rotulo, a.Peso, a.Corte)).Should().BeEquivalentTo(
        [
            ("REDACAO", "Redação", 2.00m, (decimal?)400m),
            ("CIENCIAS_DA_NATUREZA", "Ciências da Natureza e suas Tecnologias", 1.50m, (decimal?)null),
            ("CIENCIAS_HUMANAS", "Ciências Humanas e suas Tecnologias", 2.50m, (decimal?)null),
            ("LINGUAGENS", "Linguagens e suas Tecnologias", 2.50m, (decimal?)null),
            ("MATEMATICA", "Matemática e suas Tecnologias", 1.50m, (decimal?)null),
        ]);
    }

    private static CenarioDaClassificacao CenarioDeEnemLocal(ProcessoSeletivo processo) =>
        Com(processo).ComRegrasBasicas();

    [Fact(DisplayName = "Resolução completa é congelada por cópia: grupo, base legal e peso e corte de cada área")]
    public async Task Handle_ResolucaoCompleta_CongelaOQuadro()
    {
        ProcessoSeletivo processo = NovoProcessoEnem();
        CenarioDaClassificacao cenario = CenarioDeEnemLocal(processo).ComResolucaoDosPesos();

        Result result = await cenario.DefinirAsync(ComandoEnemLocal(processo.Id, ResolucaoDePesos));

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        ConfiguracaoClassificacao classificacao = processo.Classificacao!;
        classificacao.ResolucaoPesoAreaEnem.Should().Be(ResolucaoDePesos);
        classificacao.QuadroPesoAreaEnem.Select(g => (g.GrupoAreaEnem.Codigo, g.GrupoAreaEnem.Rotulo))
            .Should().BeEquivalentTo(GruposDoAnexoI);
        DeveTerCongeladoOsPesosDaAreaDeSaude(classificacao);
        await cenario.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Resolução sem algum grupo vivo é recusada no campo da resolução, nomeando o grupo ausente")]
    public async Task Handle_ResolucaoIncompleta_RecusaNomeandoOGrupoAusente()
    {
        ProcessoSeletivo processo = NovoProcessoEnem();
        ResolucaoPesoAreaEnemView semSaude = ResolucaoCom(GruposDoAnexoI.Where(g => g.Codigo != "SAUDE_E_BIOLOGICAS"), ("SAUDE_E_BIOLOGICAS", "Saúde e Biológicas"));
        CenarioDaClassificacao cenario = CenarioDeEnemLocal(processo).ComResolucaoDosPesos(semSaude);

        Result result = await cenario.DefinirAsync(ComandoEnemLocal(processo.Id, ResolucaoDePesos));

        result.IsFailure.Should().BeTrue();
        FieldError erro = result.Errors.Should().ContainSingle().Subject;
        erro.Field.Should().Be("resolucaoPesoAreaEnem");
        erro.Error.Code.Should().Be("ConfiguracaoClassificacao.ResolucaoPesoAreaEnemIncompleta");
        erro.Error.Message.Should().Contain("Saúde e Biológicas");
        processo.Classificacao.Should().BeNull();
        await cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Resolução sem nenhuma linha viva é recusada no campo da resolução")]
    public async Task Handle_ResolucaoInexistente_Recusa()
    {
        ProcessoSeletivo processo = NovoProcessoEnem();
        CenarioDaClassificacao cenario = CenarioDeEnemLocal(processo).ComResolucaoCadastrada(null);

        Result result = await cenario.DefinirAsync(ComandoEnemLocal(processo.Id, "Res. inexistente"));

        result.IsFailure.Should().BeTrue();
        FieldError erro = result.Errors.Should().ContainSingle().Subject;
        erro.Field.Should().Be("resolucaoPesoAreaEnem");
        erro.Error.Code.Should().Be("ConfiguracaoClassificacao.ResolucaoPesoAreaEnemNaoEncontrada");
        processo.Classificacao.Should().BeNull();
    }

    [Fact(DisplayName = "Classificação baseada em ENEM com cálculo local sem resolução é recusada sem consultar o cadastro")]
    public async Task Handle_EnemLocalSemResolucao_RecusaSemConsultarOCadastro()
    {
        ProcessoSeletivo processo = NovoProcessoEnem();
        CenarioDaClassificacao cenario = CenarioDeEnemLocal(processo);

        Result result = await cenario.DefinirAsync(ComandoEnemLocal(processo.Id, resolucao: "   "));

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle(e =>
            e.Field == "resolucaoPesoAreaEnem" && e.Error.Code == "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemObrigatoria");
        await cenario.NaoConsultouOCadastroDePesosAsync();
    }

    [Theory(DisplayName = "Resolução com caractere invisível ou acima da coluna é recusada como inválida sem consultar o cadastro")]
    [InlineData("805\u0000")]
    [InlineData("Res.\n805/2024")]
    [InlineData("Res. \u202E4202/508")]
    [InlineData("Resolução com quarenta e um caracteres ..")]
    public async Task Handle_ResolucaoMalformada_RecusaSemConsultarOCadastro(string resolucao)
    {
        ProcessoSeletivo processo = NovoProcessoEnem();
        CenarioDaClassificacao cenario = CenarioDeEnemLocal(processo);

        Result result = await cenario.DefinirAsync(ComandoEnemLocal(processo.Id, resolucao));

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle(e =>
            e.Field == ConfiguracaoClassificacao.CampoResolucaoPesoAreaEnem
            && e.Error.Code == "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemInvalida");
        await cenario.NaoConsultouOCadastroDePesosAsync();
    }

    [Fact(DisplayName = "Resolução com não-caractere é recusada como inválida sem consultar o cadastro, sem exceção")]
    public async Task Handle_ResolucaoComNaoCaractere_RecusaSemConsultarOCadastro()
    {
        ProcessoSeletivo processo = NovoProcessoEnem();
        CenarioDaClassificacao cenario = CenarioDeEnemLocal(processo);

        Result result = await cenario.DefinirAsync(ComandoEnemLocal(processo.Id, "Res. 805" + (char)0xFFFE));

        result.Errors.Should().ContainSingle(e => e.Error.Code == "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemInvalida");
        await cenario.NaoConsultouOCadastroDePesosAsync();
    }

    [Fact(DisplayName = "ADR-0125: a forma inválida da resolução sai junto com as demais violações, uma vez só")]
    public async Task Handle_ResolucaoMalformadaECasasInvalidas_AcumulaSemDuplicar()
    {
        ProcessoSeletivo processo = NovoProcessoEnem();
        CenarioDaClassificacao cenario = CenarioDeEnemLocal(processo);
        DefinirClassificacaoCommand command = ComandoEnemLocal(processo.Id, "805\u0000") with { CasasArredondamento = 0 };

        Result result = await cenario.DefinirAsync(command);

        result.Errors.Select(e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ((string?)ConfiguracaoClassificacao.CampoResolucaoPesoAreaEnem, "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemInvalida"),
            ((string?)"casasArredondamento", "ConfiguracaoClassificacao.CasasArredondamentoObrigatorio"),
        ]);
        await cenario.NaoConsultouOCadastroDePesosAsync();
    }

    [Fact(DisplayName = "Resolução acima da coluna em classificação importada é recusada só como indevida")]
    public async Task Handle_ResolucaoLongaEmImportada_RecusaSoComoIndevida()
    {
        ProcessoSeletivo processo = NovoProcessoEnem();
        CenarioDaClassificacao cenario = Com(processo)
            .ComRegra(RegraCalculoCodigo.ClassificacaoImportada, TipoRegra.RegraCalculo)
            .ComRegra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, TipoRegra.RegraOrdemAlocacao);

        Result result = await cenario.DefinirAsync(
            ComandoEnemLocal(processo.Id, "Resolução com quarenta e um caracteres ..", baseadoEmEnem: true, RegraCalculoCodigo.ClassificacaoImportada));

        result.Errors.Select(e => e.Error.Code).Should().Equal(
            ["ConfiguracaoClassificacao.ResolucaoPesoAreaEnemIndevida"],
            "a classificação importada não admite resolução nenhuma, e é isso que o operador precisa saber");
        await cenario.NaoConsultouOCadastroDePesosAsync();
    }

    [Fact(DisplayName = "A resolução gravada é a forma que o cadastro devolveu, não o texto do comando")]
    public async Task Handle_GravaAResolucaoQueOCadastroDevolveu()
    {
        ProcessoSeletivo processo = NovoProcessoEnem();
        CenarioDaClassificacao cenario = CenarioDeEnemLocal(processo)
            .ComResolucaoCadastrada(ResolucaoCom(GruposDoAnexoI) with { Resolucao = "Resolução canônica" });

        Result result = await cenario.DefinirAsync(ComandoEnemLocal(processo.Id, "texto do comando"));

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Classificacao!.ResolucaoPesoAreaEnem.Should().Be("Resolução canônica");
    }

    [Fact(DisplayName = "ADR-0125: a recusa do cadastro sai junto com as demais violações da classificação")]
    public async Task Handle_ResolucaoInexistenteECasasInvalidas_AcumulaAsDuas()
    {
        ProcessoSeletivo processo = NovoProcessoEnem();
        CenarioDaClassificacao cenario = CenarioDeEnemLocal(processo).ComResolucaoCadastrada(null);
        DefinirClassificacaoCommand command = ComandoEnemLocal(processo.Id, "Res. inexistente") with { CasasArredondamento = 0 };

        Result result = await cenario.DefinirAsync(command);

        result.IsFailure.Should().BeTrue();
        result.Errors.Select(e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ((string?)"resolucaoPesoAreaEnem", "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemNaoEncontrada"),
            ((string?)"casasArredondamento", "ConfiguracaoClassificacao.CasasArredondamentoObrigatorio"),
        ], "o quadro vazio é consequência da recusa do cadastro, e não sai como violação à parte");
        processo.Classificacao.Should().BeNull();
    }

    [Fact(DisplayName = "A falta em dia de prova repetida sai no próprio item, junto com a recusa da resolução")]
    public async Task Handle_FaltaEmDiaDeProvaEnemRepetidaComResolucaoInexistente_AcumulaAsDuas()
    {
        ProcessoSeletivo processo = NovoProcessoEnem();
        CenarioDaClassificacao cenario = CenarioDeEnemLocal(processo).ComRegra(RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem, TipoRegra.RegraEliminacao).ComResolucaoCadastrada(null);
        RegraEliminacaoInput falta = new(RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem, "v1", null, null, null, null);
        DefinirClassificacaoCommand command = ComandoEnemLocal(processo.Id, "Res. inexistente") with { RegrasEliminacao = [falta, falta] };

        Result result = await cenario.DefinirAsync(command);

        result.IsFailure.Should().BeTrue();
        result.Errors.Select(e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ((string?)"resolucaoPesoAreaEnem", "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemNaoEncontrada"),
            ((string?)"regrasEliminacao[1]", "ConfiguracaoClassificacao.FaltaEmDiaDeProvaEnemRepetida"),
        ], "a repetição não é consequência da resolução recusada e chega ao operador no item que a repete");
        processo.Classificacao.Should().BeNull();
    }

    [Theory(DisplayName = "Resolução informada fora da classificação baseada em ENEM com cálculo local é recusada sem consultar o cadastro")]
    [InlineData(false, RegraCalculoCodigo.FormulaMediaPonderada)]
    [InlineData(true, RegraCalculoCodigo.ClassificacaoImportada)]
    [InlineData(false, RegraCalculoCodigo.ClassificacaoImportada)]
    public async Task Handle_ResolucaoForaDeEnemLocal_RecusaComoIndevida(bool baseadoEmEnem, string regraCalculo)
    {
        ProcessoSeletivo processo = NovoProcessoEnem();
        CenarioDaClassificacao cenario = CenarioDeEnemLocal(processo).ComRegra(RegraCalculoCodigo.ClassificacaoImportada, TipoRegra.RegraCalculo);

        Result result = await cenario.DefinirAsync(ComandoEnemLocal(processo.Id, ResolucaoDePesos, baseadoEmEnem, regraCalculo));

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle(e =>
            e.Field == "resolucaoPesoAreaEnem" && e.Error.Code == "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemIndevida");
        await cenario.NaoConsultouOCadastroDePesosAsync();
    }
}
