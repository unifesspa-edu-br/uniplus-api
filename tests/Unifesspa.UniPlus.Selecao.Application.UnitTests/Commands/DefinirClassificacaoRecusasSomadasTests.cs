namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDaClassificacao;

/// <summary>
/// A definição da classificação soma as recusas dos campos e as do catálogo de regras na mesma resposta (ADR-0125), sem
/// recusar por consequência o que depende da regra que o catálogo não confirmou, e sem gravar nada quando algo é recusado.
/// </summary>
public sealed class DefinirClassificacaoRecusasSomadasTests
{
    private const string RegraInexistente = "REGRA-QUE-O-CATALOGO-NAO-TEM";

    private static string Calculo => RegraDeCalculoDaNota.MediaPonderada.Codigo();

    private static string Ordem => RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo();

    private static DefinirClassificacaoCommand ComandoSemArredondamento(Guid processoId) =>
        ComandoDeMediaPonderada(processoId) with { RegraArredondamentoCodigo = null, RegraArredondamentoVersao = null, CasasArredondamento = null };

    private static (string? Campo, string Codigo)[] Recusas(Result result) =>
        [.. result.Errors.Select(static erro => (erro.Field, erro.Error.Code))];

    private static (CenarioDaClassificacao Cenario, ProcessoSeletivo Processo) ComCatalogoBasico()
    {
        ProcessoSeletivo processo = NovoProcesso("PSIQ 2026", TiposDeProcessoDeTeste.IndigenaEQuilombola);
        return (Com(processo).ComRegrasBasicas(), processo);
    }

    private static async Task NadaFoiGravadoAsync(CenarioDaClassificacao cenario, ProcessoSeletivo processo)
    {
        processo.Classificacao.Should().BeNull();
        await cenario.UnitOfWork.DidNotReceiveWithAnyArgs().SalvarAlteracoesAsync(default);
    }

    [Fact(DisplayName = "Regra de cálculo inexistente com escala zero recusa a escala e depois a regra, cada uma no seu campo")]
    public async Task RegraDeCalculoInexistenteEEscalaZero_SomaAsDuasRecusas()
    {
        (CenarioDaClassificacao cenario, ProcessoSeletivo processo) = ComCatalogoBasico();
        DefinirClassificacaoCommand command = ComandoDeMediaPonderada(processo.Id) with { RegraCalculoCodigo = RegraInexistente, EscalaNotaFinal = 0m };

        Result result = await cenario.DefinirAsync(command);

        Recusas(result).Should().Equal(
            ("escalaNotaFinal", "ConfiguracaoClassificacao.EscalaNotaFinalInvalida"),
            ("regraCalculoCodigo", "ConfiguracaoClassificacao.RegraNaoEncontrada"));
        await NadaFoiGravadoAsync(cenario, processo);
    }

    [Fact(DisplayName = "Cálculo local sem casas decimais e ordem de alocação inexistente recusa as casas e depois a regra")]
    public async Task CasasInvalidasEOrdemInexistente_SomaAsDuasRecusas()
    {
        (CenarioDaClassificacao cenario, ProcessoSeletivo processo) = ComCatalogoBasico();
        DefinirClassificacaoCommand command = ComandoDeMediaPonderada(processo.Id) with { CasasArredondamento = null, RegraOrdemAlocacaoCodigo = RegraInexistente };

        Result result = await cenario.DefinirAsync(command);

        Recusas(result).Should().Equal(
            ("casasArredondamento", "ConfiguracaoClassificacao.CasasArredondamentoObrigatorio"),
            ("regraOrdemAlocacaoCodigo", "ConfiguracaoClassificacao.RegraNaoEncontrada"));
        await NadaFoiGravadoAsync(cenario, processo);
    }

    [Fact(DisplayName = "Regra de cálculo inexistente sem arredondamento recusa só a regra: o arredondamento não é cobrado")]
    public async Task RegraDeCalculoInexistenteSemArredondamento_RecusaSoARegra()
    {
        (CenarioDaClassificacao cenario, ProcessoSeletivo processo) = ComCatalogoBasico();
        DefinirClassificacaoCommand command = ComandoSemArredondamento(processo.Id) with { RegraCalculoCodigo = RegraInexistente };

        Result result = await cenario.DefinirAsync(command);

        Recusas(result).Should().Equal(("regraCalculoCodigo", "ConfiguracaoClassificacao.RegraNaoEncontrada"));
    }

    [Fact(DisplayName = "Regra de cálculo de tipo inválido sem arredondamento recusa só o tipo, no campo da regra")]
    public async Task RegraDeCalculoDeTipoInvalidoSemArredondamento_RecusaSoOTipo()
    {
        ProcessoSeletivo processo = NovoProcesso("PSIQ 2026", TiposDeProcessoDeTeste.IndigenaEQuilombola);
        CenarioDaClassificacao cenario = Com(processo)
            .ComRegra(Calculo, TipoRegra.RegraArredondamento)
            .ComRegra(Ordem, TipoRegra.RegraOrdemAlocacao);
        DefinirClassificacaoCommand command = ComandoSemArredondamento(processo.Id);

        Result result = await cenario.DefinirAsync(command);

        Recusas(result).Should().Equal(("regraCalculoCodigo", "ConfiguracaoClassificacao.RegraTipoInvalido"));
    }

    [Fact(DisplayName = "Arredondamento informado e inexistente em cálculo local recusa só a regra, sem dizer que ele é obrigatório")]
    public async Task ArredondamentoInformadoEInexistente_NaoRecusaComoObrigatorio()
    {
        (CenarioDaClassificacao cenario, ProcessoSeletivo processo) = ComCatalogoBasico();
        DefinirClassificacaoCommand command = ComandoDeMediaPonderada(processo.Id) with { RegraArredondamentoCodigo = RegraInexistente };

        Result result = await cenario.DefinirAsync(command);

        Recusas(result).Should().Equal(("regraArredondamentoCodigo", "ConfiguracaoClassificacao.RegraNaoEncontrada"));
    }

    [Fact(DisplayName = "Regra de cálculo e ordem de alocação inexistentes saem juntas, na ordem do pedido")]
    public async Task CalculoEOrdemInexistentes_SomamAsDuasRecusas()
    {
        (CenarioDaClassificacao cenario, ProcessoSeletivo processo) = ComCatalogoBasico();
        DefinirClassificacaoCommand command = ComandoSemArredondamento(processo.Id) with { RegraCalculoCodigo = RegraInexistente, RegraOrdemAlocacaoCodigo = RegraInexistente };

        Result result = await cenario.DefinirAsync(command);

        Recusas(result).Should().Equal(
            ("regraCalculoCodigo", "ConfiguracaoClassificacao.RegraNaoEncontrada"),
            ("regraOrdemAlocacaoCodigo", "ConfiguracaoClassificacao.RegraNaoEncontrada"));
        await NadaFoiGravadoAsync(cenario, processo);
    }

    [Fact(DisplayName = "Falta de baseadoEmEnem e regra inexistente saem juntas")]
    public async Task BaseadoEmEnemAusenteERegraInexistente_SomamAsDuasRecusas()
    {
        (CenarioDaClassificacao cenario, ProcessoSeletivo processo) = ComCatalogoBasico();
        DefinirClassificacaoCommand command = ComandoDeMediaPonderada(processo.Id) with { BaseadoEmEnem = null, RegraOrdemAlocacaoCodigo = RegraInexistente };

        Result result = await cenario.DefinirAsync(command);

        Recusas(result).Should().Equal(
            ("baseadoEmEnem", CodigosDeValidacao.Obrigatorio),
            ("regraOrdemAlocacaoCodigo", "ConfiguracaoClassificacao.RegraNaoEncontrada"));
    }

    [Fact(DisplayName = "Só campo inválido, com as regras no catálogo, recusa só o campo")]
    public async Task SoCampoInvalido_RecusaSoOCampo()
    {
        (CenarioDaClassificacao cenario, ProcessoSeletivo processo) = ComCatalogoBasico();

        Result result = await cenario.DefinirAsync(ComandoDeMediaPonderada(processo.Id) with { EscalaNotaFinal = -1m });

        Recusas(result).Should().Equal(("escalaNotaFinal", "ConfiguracaoClassificacao.EscalaNotaFinalInvalida"));
        await NadaFoiGravadoAsync(cenario, processo);
    }

    [Fact(DisplayName = "A recusa do cadastro de Pesos por Área sai depois das dos campos e antes das do catálogo")]
    public async Task CamposPesosECatalogo_SaemNessaOrdem()
    {
        (CenarioDaClassificacao cenario, ProcessoSeletivo processo) = ComCatalogoBasico();
        cenario.ComResolucaoCadastrada(null);
        DefinirClassificacaoCommand command = ComandoEnemLocal(processo.Id, "Res. inexistente") with { CasasArredondamento = 0, RegraOrdemAlocacaoCodigo = RegraInexistente };

        Result result = await cenario.DefinirAsync(command);

        Recusas(result).Should().Equal(
            ("casasArredondamento", "ConfiguracaoClassificacao.CasasArredondamentoObrigatorio"),
            ("resolucaoPesoAreaEnem", "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemNaoEncontrada"),
            ("regraOrdemAlocacaoCodigo", "ConfiguracaoClassificacao.RegraNaoEncontrada"));
    }

    [Fact(DisplayName = "Regra de cálculo inexistente não consulta o cadastro de Pesos por Área")]
    public async Task RegraDeCalculoInexistente_NaoConsultaOCadastroDePesos()
    {
        (CenarioDaClassificacao cenario, ProcessoSeletivo processo) = ComCatalogoBasico();
        DefinirClassificacaoCommand command = ComandoEnemLocal(processo.Id, ResolucaoDePesos) with { RegraCalculoCodigo = RegraInexistente };

        Result result = await cenario.DefinirAsync(command);

        Recusas(result).Should().Equal(("regraCalculoCodigo", "ConfiguracaoClassificacao.RegraNaoEncontrada"));
        await cenario.NaoConsultouOCadastroDePesosAsync();
    }

    [Fact(DisplayName = "Regra de eliminação inexistente sai depois das recusas dos campos e sem o campo")]
    public async Task EliminacaoInexistenteEEscalaInvalida_SaiDepoisDosCampos()
    {
        (CenarioDaClassificacao cenario, ProcessoSeletivo processo) = ComCatalogoBasico();
        RegraEliminacaoInput eliminacao = new(RegraInexistente, "v1", null, null, null, null);
        DefinirClassificacaoCommand command = ComandoComEliminacao(processo.Id, eliminacao) with { EscalaNotaFinal = 0m };

        Result result = await cenario.DefinirAsync(command);

        Recusas(result).Should().Equal(
            ("escalaNotaFinal", "ConfiguracaoClassificacao.EscalaNotaFinalInvalida"),
            ((string?)null, "RegraEliminacao.RegraNaoEncontrada"));
        await NadaFoiGravadoAsync(cenario, processo);
    }

    [Fact(DisplayName = "Regra de cálculo inexistente com eliminação válida do ENEM recusa só a regra")]
    public async Task RegraDeCalculoInexistenteComEliminacao_NaoRecusaPorConsequencia()
    {
        ProcessoSeletivo processo = NovoProcessoEnem();
        CenarioDaClassificacao cenario = Com(processo)
            .ComRegra(RegraDeArredondamentoDaNota.Truncar.Codigo(), TipoRegra.RegraArredondamento)
            .ComRegra(Ordem, TipoRegra.RegraOrdemAlocacao)
            .ComRegra(RegraDeEliminacaoPorCalculo.ZeroEmAreaDoEnem.Codigo(), TipoRegra.RegraEliminacao);
        RegraEliminacaoInput zeroEmArea = new(RegraDeEliminacaoPorCalculo.ZeroEmAreaDoEnem.Codigo(), "v1", null, null, null, null);
        DefinirClassificacaoCommand command = ComandoEnemLocalComEliminacao(processo.Id, zeroEmArea) with { RegraCalculoCodigo = RegraInexistente };

        Result result = await cenario.DefinirAsync(command);

        Recusas(result).Should().Equal(("regraCalculoCodigo", "ConfiguracaoClassificacao.RegraNaoEncontrada"));
    }

    [Fact(DisplayName = "Nenhuma recusa repete o código nem a versão enviados")]
    public async Task Recusas_NaoRepetemOValorEnviado()
    {
        (CenarioDaClassificacao cenario, ProcessoSeletivo processo) = ComCatalogoBasico();
        DefinirClassificacaoCommand command = ComandoDeMediaPonderada(processo.Id) with
        {
            RegraCalculoCodigo = RegraInexistente,
            RegraCalculoVersao = "versao-enviada",
            RegraOrdemAlocacaoCodigo = RegraInexistente,
        };

        Result result = await cenario.DefinirAsync(command);

        result.Errors.Select(static erro => erro.Error.Message).Should().NotContain(mensagem =>
            mensagem.Contains(RegraInexistente, StringComparison.Ordinal) || mensagem.Contains("versao-enviada", StringComparison.Ordinal));
    }

    private static CenarioDaClassificacao ComCatalogoDaClassificacaoImportada(ProcessoSeletivo processo) =>
        Com(processo)
            .ComRegra(RegraDeCalculoDaNota.ClassificacaoImportada.Codigo(), TipoRegra.RegraCalculo)
            .ComRegra(Ordem, TipoRegra.RegraOrdemAlocacao);

    private static DefinirClassificacaoCommand ImportadaSemEnem(Guid processoId) =>
        ComandoEnemLocal(processoId, null, baseadoEmEnem: false, RegraDeCalculoDaNota.ClassificacaoImportada.Codigo());

    [Theory(DisplayName = "Código de arredondamento vazio ou em branco com versão vai ao catálogo e é recusado no campo do código")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ArredondamentoEmBrancoComVersao_RecusaComoRegraNaoEncontrada(string codigo)
    {
        ProcessoSeletivo processo = NovoProcesso("PSIQ 2026", TiposDeProcessoDeTeste.IndigenaEQuilombola);
        CenarioDaClassificacao cenario = ComCatalogoDaClassificacaoImportada(processo);
        DefinirClassificacaoCommand command = ImportadaSemEnem(processo.Id) with { RegraArredondamentoCodigo = codigo, RegraArredondamentoVersao = "v1" };

        Result result = await cenario.DefinirAsync(command);

        Recusas(result).Should().Equal(
            ("regraArredondamentoCodigo", "ConfiguracaoClassificacao.ArredondamentoIndevido"),
            ("regraArredondamentoCodigo", "ConfiguracaoClassificacao.RegraNaoEncontrada"));
        await NadaFoiGravadoAsync(cenario, processo);
    }

    [Fact(DisplayName = "Arredondamento não declarado não é consultado nem recusado na classificação importada")]
    public async Task ArredondamentoNaoDeclarado_NaoConsultaNemRecusa()
    {
        ProcessoSeletivo processo = NovoProcesso("PSIQ 2026", TiposDeProcessoDeTeste.IndigenaEQuilombola);
        CenarioDaClassificacao cenario = ComCatalogoDaClassificacaoImportada(processo);

        Result result = await cenario.DefinirAsync(ImportadaSemEnem(processo.Id));

        result.IsSuccess.Should().BeTrue();
        processo.Classificacao.Should().NotBeNull();
        await cenario.ConsultouSoAsRegrasDoCatalogoAsync((RegraDeCalculoDaNota.ClassificacaoImportada.Codigo(), "v1"), (Ordem, "v1"));
    }
}
