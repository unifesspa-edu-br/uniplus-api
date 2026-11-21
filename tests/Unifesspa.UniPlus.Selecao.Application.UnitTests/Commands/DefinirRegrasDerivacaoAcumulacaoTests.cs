namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static ApoioDaDefinicaoDasRegrasDeDerivacao;

/// <summary>
/// Acumulação das recusas (ADR-0125) do <see cref="DefinirRegrasDerivacaoCommandHandler"/>: forma, semântica e
/// coerência com o domínio do fato, cada grupo na ordem do pedido, e o corte só do que depende do dado recusado.
/// </summary>
public sealed class DefinirRegrasDerivacaoAcumulacaoTests
{
    [Fact(DisplayName = "ADR-0125: violações de forma acumulam entre configurações e regras, com a posição prefixada ao campo")]
    public async Task Handle_DuasViolacoesDeForma_AcumulaComPosicaoPrefixada()
    {
        ProcessoSeletivo processo = ProcessoBase();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirRegrasDerivacaoCommand command = new(processo.Id,
            [new ConfiguracaoDerivacaoInput("", [new RegraDerivacaoInput(0, "AC", null)]), ConfigDerivado(new RegraDerivacaoInput(-1, "", null))]);

        Result resultado = await HandleAsync(mocks, command);

        resultado.Errors.Select(e => e.Field).Should().BeEquivalentTo(
            ["configuracoes[0].codigoFato", "configuracoes[1].regras[0].ordem", "configuracoes[1].regras[0].contribui"]);
    }

    [Fact(DisplayName = "ADR-0125: operador desconhecido e ordem inválida da mesma regra saem juntos, cada um no campo próprio")]
    public async Task Handle_OperadorDesconhecidoEOrdemInvalida_SaemJuntosNaPosicaoDaRegra()
    {
        ProcessoSeletivo processo = ProcessoComModalidadeAcEColeta();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirRegrasDerivacaoCommand command = new(processo.Id,
            [ConfigDerivado(new RegraDerivacaoInput(-1, "AC", [[Condicao("COR_RACA", "PARECIDO_COM", "PRETA")]]))]);

        Result resultado = await HandleAsync(mocks, command);

        resultado.Errors.Select(e => e.Field).Should().Equal(
            "configuracoes[0].regras[0].ordem", "configuracoes[0].regras[0].quando[0][0].operador");
    }

    [Fact(DisplayName = "ADR-0125: duas regras inválidas saem juntas, cada uma na própria posição")]
    public async Task Handle_DuasRegrasInvalidas_SaemJuntas()
    {
        ProcessoSeletivo processo = ProcessoComModalidadeAcEColeta();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirRegrasDerivacaoCommand command = new(processo.Id,
        [
            ConfigDerivado(
                new RegraDerivacaoInput(0, "AC", [[Condicao("COR_RACA", "MAIOR_IGUAL", "PRETA")]]),
                new RegraDerivacaoInput(1, "AC", [[Condicao("BAIXA_RENDA", "IGUAL", true)]])),
        ]);

        Result resultado = await HandleAsync(mocks, command);

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            ("configuracoes[0].regras[0].quando[0][0].operador", "PredicadoDnf.OperadorIncompativelComDominio"),
            ("configuracoes[0].regras[1].quando[0][0].fato", "PredicadoDnf.FatoNaoColetadoPeloProcesso"));
    }

    [Fact(DisplayName = "ADR-0125: a forma inválida de uma regra corta a semântica dela, mas não esconde a recusa de outra regra")]
    public async Task Handle_FormaInvalidaDeUmaRegra_NaoEscondeASemanticaDeOutra()
    {
        ProcessoSeletivo processo = ProcessoComModalidadeAcEColeta();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirRegrasDerivacaoCommand command = new(processo.Id,
        [
            ConfigDerivado(
                new RegraDerivacaoInput(0, "AC", [[Condicao("BAIXA_RENDA", "OPERADOR_INEXISTENTE", true)]]),
                new RegraDerivacaoInput(1, "AC", [[Condicao("COR_RACA", "MAIOR_IGUAL", "PRETA")]])),
        ]);

        Result resultado = await HandleAsync(mocks, command);

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            ("configuracoes[0].regras[0].quando[0][0].operador", "CondicaoDnf.OperadorInvalido"),
            ("configuracoes[0].regras[1].quando[0][0].operador", "PredicadoDnf.OperadorIncompativelComDominio"));
    }

    [Fact(DisplayName = "Regra válida ao lado de uma configuração inválida não grava nada")]
    public async Task Handle_ConfiguracaoInvalidaEOutraValida_NaoGrava()
    {
        ProcessoSeletivo processo = ProcessoComModalidadeAcEColeta();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirRegrasDerivacaoCommand command = new(processo.Id,
            [ConfigDerivado(new RegraDerivacaoInput(0, "AC", null)), new ConfiguracaoDerivacaoInput("BOGUS", [new RegraDerivacaoInput(0, "AC", null)])]);

        Result resultado = await HandleAsync(mocks, command);

        resultado.Errors.Should().ContainSingle(e => e.Field == "configuracoes[1].codigoFato");
        processo.RegrasDerivacao.Should().BeEmpty();
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "ADR-0125: a forma de todas as configurações sai antes da semântica, cada grupo na ordem do pedido")]
    public async Task Handle_SemanticaDaPrimeiraEFormaDaSegunda_FormaSaiPrimeiro()
    {
        ProcessoSeletivo processo = ProcessoComModalidadeAcEColeta();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirRegrasDerivacaoCommand command = new(processo.Id,
        [
            ConfigDerivado(new RegraDerivacaoInput(0, "AC", [[Condicao("COR_RACA", "MAIOR_IGUAL", "PRETA")]])),
            new ConfiguracaoDerivacaoInput("", [new RegraDerivacaoInput(0, "AC", null)]),
        ]);

        Result resultado = await HandleAsync(mocks, command);

        resultado.Errors.Select(e => e.Field).Should().Equal(
            "configuracoes[1].codigoFato", "configuracoes[0].regras[0].quando[0][0].operador");
    }

    [Fact(DisplayName = "ADR-0125: a recusa do fato alvo sai antes da coerência de outra configuração, no campo próprio de cada uma")]
    public async Task Handle_CoerenciaDaPrimeiraEAlvoDesconhecidoDaSegunda_AlvoSaiPrimeiro()
    {
        ProcessoSeletivo processo = ProcessoComModalidadeAcEColeta();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirRegrasDerivacaoCommand command = new(processo.Id,
        [
            ConfigDerivado(new RegraDerivacaoInput(0, null, null)),
            new ConfiguracaoDerivacaoInput("BOGUS", [new RegraDerivacaoInput(0, "AC", null)]),
        ]);

        Result resultado = await HandleAsync(mocks, command);

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            ("configuracoes[1].codigoFato", "ConfiguracaoDerivacaoFato.FatoDesconhecido"),
            ("configuracoes[0].regras", "ConfiguracaoDerivacaoFato.ContribuicaoIncoerenteComODominio"));
    }

    [Fact(DisplayName = "O fato alvo desconhecido e o não derivável são recusados no campo do código do fato da própria configuração")]
    public async Task Handle_AlvoDesconhecidoENaoDerivavel_RecusadosNoCodigoDoFato()
    {
        ProcessoSeletivo processo = ProcessoComModalidadeAcEColeta();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirRegrasDerivacaoCommand command = new(processo.Id,
        [
            ConfigDerivado(new RegraDerivacaoInput(0, "AC", null)),
            new ConfiguracaoDerivacaoInput("BOGUS", [new RegraDerivacaoInput(0, "AC", null)]),
            new ConfiguracaoDerivacaoInput("COR_RACA", [new RegraDerivacaoInput(0, "PRETA", null)]),
        ]);

        Result resultado = await HandleAsync(mocks, command);

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            ("configuracoes[1].codigoFato", "ConfiguracaoDerivacaoFato.FatoDesconhecido"),
            ("configuracoes[2].codigoFato", "ConfiguracaoDerivacaoFato.FatoNaoDerivavel"));
    }
}
