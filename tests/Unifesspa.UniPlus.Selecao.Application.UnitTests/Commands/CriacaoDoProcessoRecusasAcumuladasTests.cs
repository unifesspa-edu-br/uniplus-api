namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Cenarios;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Depois da permissão na unidade, a criação do processo devolve juntas todas as recusas que não dependem umas das
/// outras, na ordem em que quem cria as corrige, e o código principal é o da primeira (ADR-0125). Nada é gravado.
/// </summary>
public sealed class CriacaoDoProcessoRecusasAcumuladasTests
{
    private const string Identificador = "psiq-2026";

    private readonly CenarioDaCriacaoDoProcesso _cenario = new();

    private static CriacaoDoProcessoSeletivoBuilder Pedido() => new(CenarioDaCriacaoDoProcesso.UnidadeId);

    private static (string? Campo, string Codigo)[] Recusas(Result<Guid> resultado) =>
        [.. resultado.Errors.Select(static recusa => (recusa.Field, recusa.Error.Code))];

    [Fact(DisplayName = "Identificador fora do formato, localidade não declarada e tipo desativado saem juntos, e o código principal é o do identificador")]
    public async Task IdentificadorLocalidadeETipo_SaemJuntos()
    {
        _cenario.DesativarOTipo();

        Result<Guid> resultado = await _cenario.CriarAsync(Pedido().ComIdentificadorLegivel("PSIQ-2026").ComLocalidade(null, null, null).Construir());

        Recusas(resultado).Should().Equal(
            ("identificadorLegivel", ProcessoSeletivoErrorCodes.IdentificadorLegivelFormatoInvalido),
            ("localidadeCodigoIbge", ProcessoSeletivoErrorCodes.LocalidadeAusente),
            ("tipoProcessoOrigemId", ProcessoSeletivoErrorCodes.TipoProcessoNaoEncontradoOuInativo));
        resultado.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.IdentificadorLegivelFormatoInvalido);
        await _cenario.Repository.DidNotReceiveWithAnyArgs().IdentificadorLegivelEmUsoAsync(default, default, default);
        await _cenario.NadaFoiGravadoAsync();
    }

    [Fact(DisplayName = "Localidade com código, nome e UF inválidos devolve as três causas, cada uma no seu campo")]
    public async Task LocalidadeComTresCausas_CadaUmaNoSeuCampo()
    {
        Result<Guid> resultado = await _cenario.CriarAsync(Pedido().ComLocalidade("12", "  ", string.Empty).Construir());

        Recusas(resultado).Should().Equal(
            ("localidadeNome", CidadeReferenciaErrorCodes.NomeObrigatorio),
            ("localidadeUf", CidadeReferenciaErrorCodes.UfObrigatoria),
            ("localidadeCodigoIbge", CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido));
        await _cenario.NadaFoiGravadoAsync();
    }

    [Fact(DisplayName = "Identificador com tamanho e formato inválidos devolve as duas causas no campo do identificador")]
    public async Task IdentificadorComTamanhoEFormato_DuasCausas()
    {
        Result<Guid> resultado = await _cenario.CriarAsync(Pedido().ComIdentificadorLegivel("PS").Construir());

        Recusas(resultado).Should().Equal(
            ("identificadorLegivel", ProcessoSeletivoErrorCodes.IdentificadorLegivelTamanho),
            ("identificadorLegivel", ProcessoSeletivoErrorCodes.IdentificadorLegivelFormatoInvalido));
        await _cenario.NadaFoiGravadoAsync();
    }

    [Fact(DisplayName = "Unidade sem cidade, localidade não declarada e tipo desativado saem juntos, na ordem unidade, localidade, tipo")]
    public async Task UnidadeSemCidadeLocalidadeETipo_SaemJuntos()
    {
        _cenario.UnidadeReader.ObterPorIdAsync(CenarioDaCriacaoDoProcesso.UnidadeId, Arg.Any<CancellationToken>())
            .Returns(CenarioDaCriacaoDoProcesso.UnidadeSemCidade);
        _cenario.DesativarOTipo();

        Result<Guid> resultado = await _cenario.CriarAsync(Pedido().ComLocalidade(null, null, null).Construir());

        Recusas(resultado).Should().Equal(
            ("unidadeAdministradoraOrigemId", ProcessoSeletivoErrorCodes.UnidadeAdministradoraSemCidade),
            ("localidadeCodigoIbge", ProcessoSeletivoErrorCodes.LocalidadeAusente),
            ("tipoProcessoOrigemId", ProcessoSeletivoErrorCodes.TipoProcessoNaoEncontradoOuInativo));
        await _cenario.NadaFoiGravadoAsync();
    }

    [Fact(DisplayName = "Identificador válido já usado e localidade não declarada saem juntos, com o uso por último")]
    public async Task IdentificadorEmUsoELocalidade_UsoPorUltimo()
    {
        MarcarIdentificadorComoUsado();

        Result<Guid> resultado = await _cenario.CriarAsync(Pedido().ComLocalidade(null, null, null).Construir());

        Recusas(resultado).Should().Equal(
            ("localidadeCodigoIbge", ProcessoSeletivoErrorCodes.LocalidadeAusente),
            ("identificadorLegivel", ProcessoSeletivoErrorCodes.IdentificadorLegivelEmUso));
        resultado.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.LocalidadeAusente);
        await _cenario.NadaFoiGravadoAsync();
    }

    [Fact(DisplayName = "Identificador já usado como única recusa mantém o código do uso, e a mensagem não repete o valor")]
    public async Task IdentificadorEmUsoSozinho_MensagemSemOValor()
    {
        MarcarIdentificadorComoUsado();

        Result<Guid> resultado = await _cenario.CriarAsync(Pedido().Construir());

        Recusas(resultado).Should().Equal(("identificadorLegivel", ProcessoSeletivoErrorCodes.IdentificadorLegivelEmUso));
        resultado.Error!.Message.Should().NotContain(Identificador);
        await _cenario.NadaFoiGravadoAsync();
    }

    [Fact(DisplayName = "Todas as recusas independentes saem na ordem nome, origem, identificador, unidade, localidade, tipo e uso")]
    public async Task OrdemCompleta_DasRecusas()
    {
        _cenario.DesativarOTipo();
        _cenario.UnidadeReader.ObterPorIdAsync(CenarioDaCriacaoDoProcesso.UnidadeId, Arg.Any<CancellationToken>())
            .Returns((UnidadeView?)null);
        MarcarIdentificadorComoUsado();

        Result<Guid> resultado = await _cenario.CriarAsync(
            Pedido().ComLocalidade(null, null, null).Construir() with { Nome = string.Empty, OrigemCandidatos = null });

        resultado.Errors.Select(static recusa => recusa.Field).Should().Equal(
            "nome", "origemCandidatos", "unidadeAdministradoraOrigemId", "localidadeCodigoIbge", "tipoProcessoOrigemId", "identificadorLegivel");
        resultado.Errors[^1].Error.Code.Should().Be(ProcessoSeletivoErrorCodes.IdentificadorLegivelEmUso);
    }

    [Fact(DisplayName = "Sem unidade informada, a falta dela, a localidade e o tipo saem juntos, sem ler nenhum cadastro")]
    public async Task SemUnidade_RecusasSemLeitura()
    {
        CriarProcessoSeletivoCommand comando = Pedido().ComLocalidade(null, null, null).Construir() with
        {
            UnidadeAdministradoraOrigemId = Guid.Empty,
            TipoProcessoOrigemId = Guid.Empty,
        };

        Result<Guid> resultado = await _cenario.CriarAsync(comando);

        Recusas(resultado).Should().Equal(
            ("unidadeAdministradoraOrigemId", CodigosDeValidacao.Obrigatorio),
            ("localidadeCodigoIbge", ProcessoSeletivoErrorCodes.LocalidadeAusente),
            ("tipoProcessoOrigemId", CodigosDeValidacao.Obrigatorio));
        await _cenario.NadaFoiLidoNemGravadoAsync();
    }

    private void MarcarIdentificadorComoUsado() =>
        _cenario.Repository.IdentificadorLegivelEmUsoAsync(
                IdentificadorLegivel.Criar(Identificador).Value, null, Arg.Any<CancellationToken>())
            .Returns(true);
}
