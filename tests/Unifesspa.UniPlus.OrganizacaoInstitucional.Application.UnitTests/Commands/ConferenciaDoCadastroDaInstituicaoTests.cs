namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.Enderecos;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Instituicoes;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class ConferenciaDoCadastroDaInstituicaoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 6, 22, 17, 10, 0, TimeSpan.Zero);

    private readonly IInstituicaoRepository _repo = Substitute.For<IInstituicaoRepository>();
    private readonly IUnidadeRepository _unidades = Substitute.For<IUnidadeRepository>();
    private readonly ConferenciaDoCadastroDaInstituicao _conferencia;

    public ConferenciaDoCadastroDaInstituicaoTests()
    {
        _conferencia = new ConferenciaDoCadastroDaInstituicao(_repo, _unidades);
    }

    private Task<Result<Instituicao>> ConferirNovaAsync(CriarInstituicaoCommand command) =>
        _conferencia.ConferirNovaAsync(command, Agora, CancellationToken.None);

    private Task<Result<Instituicao>> ConferirAtualizacaoAsync(AtualizarInstituicaoCommand command) =>
        _conferencia.ConferirAtualizacaoAsync(command, Agora, CancellationToken.None);

    private Instituicao InstituicaoCadastrada()
    {
        Instituicao existente = InstituicaoDeTeste.Criar().Value!;
        _repo.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        return existente;
    }

    private void UnidadeRaizDoTipo(Guid id, TipoUnidade tipo) =>
        _unidades.ObterPorIdParaLeituraAsync(id, Arg.Any<CancellationToken>())
            .Returns(ComandosDeInstituicaoDeTeste.UnidadeDoTipo(tipo));

    [Fact(DisplayName = "Nova com payload válido e sem Instituição viva devolve a Instituição montada")]
    public async Task ConferirNova_PayloadValido_DevolveAInstituicao()
    {
        Result<Instituicao> resultado = await ConferirNovaAsync(ComandosDeInstituicaoDeTeste.Criacao());

        resultado.Value!.CodigoEmec.Should().Be("3990");
    }

    [Fact(DisplayName = "Nova com payload inválido acumula os erros e não consulta o repositório — validação vence I/O")]
    public async Task ConferirNova_PayloadInvalido_AcumulaOsErrosSemConsultar()
    {
        Result<Instituicao> resultado = await ConferirNovaAsync(ComandosDeInstituicaoDeTeste.Criacao() with { CodigoEmec = "", Sigla = "" });

        resultado.Errors.Select(erro => erro.Field).Should().Equal("codigoEmec", "sigla");
        await _repo.DidNotReceive().ExisteAlgumaVivaAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Nova com endereço mas sem cidade da sede recusa antes de consultar o repositório")]
    public async Task ConferirNova_EnderecoSemCidade_RecusaSemConsultar()
    {
        Result<Instituicao> resultado = await ConferirNovaAsync(
            ComandosDeInstituicaoDeTeste.Criacao() with { Endereco = ComandosDeInstituicaoDeTeste.EnderecoEmMaraba() });

        resultado.Error!.Code.Should().Be(EnderecoReferenciaErrorCodes.CidadeObrigatoriaComEndereco);
        await _repo.DidNotReceive().ExisteAlgumaVivaAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Nova com endereço e cidade coerente leva o endereço à Instituição")]
    public async Task ConferirNova_EnderecoECidadeCoerentes_LevaOEndereco()
    {
        CriarInstituicaoCommand comando = ComandosDeInstituicaoDeTeste.Criacao() with
        {
            CidadeCodigoIbge = "1504208",
            CidadeNome = "Marabá",
            CidadeUf = "PA",
            Endereco = ComandosDeInstituicaoDeTeste.EnderecoEmMaraba(),
        };

        Result<Instituicao> resultado = await ConferirNovaAsync(comando);

        resultado.Value!.Endereco!.Cep.Should().Be("68507590");
    }

    [Fact(DisplayName = "Nova com cidade carimba a proveniência do Geo e o instante da criação")]
    public async Task ConferirNova_ComCidade_CarimbaProvenienciaEInstante()
    {
        CriarInstituicaoCommand comando = ComandosDeInstituicaoDeTeste.Criacao() with
        {
            CidadeCodigoIbge = "1504208",
            CidadeNome = "Marabá",
            CidadeUf = "PA",
        };

        Result<Instituicao> resultado = await ConferirNovaAsync(comando);

        resultado.Value!.CidadeDaSede.Should().Be(new CidadeDaSede("1504208", "Marabá", "PA", ReferenciaCidadeGeo.OrigemGeoApi, Agora));
    }

    [Fact(DisplayName = "Nova sem cidade deixa proveniência e instante nulos")]
    public async Task ConferirNova_SemCidade_NaoCarimba()
    {
        Result<Instituicao> resultado = await ConferirNovaAsync(ComandosDeInstituicaoDeTeste.Criacao());

        resultado.Value!.CidadeDaSede.Should().Be(CidadeDaSede.Nenhuma);
    }

    [Fact(DisplayName = "Nova com Instituição viva existente recusa sem consultar o vínculo")]
    public async Task ConferirNova_ComInstituicaoViva_RecusaSemConsultarOVinculo()
    {
        _repo.ExisteAlgumaVivaAsync(Arg.Any<CancellationToken>()).Returns(true);

        Result<Instituicao> resultado = await ConferirNovaAsync(ComandosDeInstituicaoDeTeste.Criacao(Guid.CreateVersion7()));

        resultado.Error!.Code.Should().Be(InstituicaoErrorCodes.JaExisteInstituicaoViva);
        await _unidades.DidNotReceive().ObterPorIdParaLeituraAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Nova com unidade raiz inexistente recusa com UnidadeRaizNaoEncontrada")]
    public async Task ConferirNova_RaizInexistente_Recusa()
    {
        Result<Instituicao> resultado = await ConferirNovaAsync(ComandosDeInstituicaoDeTeste.Criacao(Guid.CreateVersion7()));

        resultado.Error!.Code.Should().Be(InstituicaoErrorCodes.UnidadeRaizNaoEncontrada);
    }

    [Fact(DisplayName = "Nova com unidade raiz que não é reitoria recusa com UnidadeRaizNaoEhReitoria")]
    public async Task ConferirNova_RaizNaoReitoria_Recusa()
    {
        Guid raizId = Guid.CreateVersion7();
        UnidadeRaizDoTipo(raizId, TipoUnidade.Centro);

        Result<Instituicao> resultado = await ConferirNovaAsync(ComandosDeInstituicaoDeTeste.Criacao(raizId));

        resultado.Error!.Code.Should().Be(InstituicaoErrorCodes.UnidadeRaizNaoEhReitoria);
    }

    [Fact(DisplayName = "Nova com unidade raiz reitoria leva o vínculo à Instituição")]
    public async Task ConferirNova_RaizReitoria_LevaOVinculo()
    {
        Guid raizId = Guid.CreateVersion7();
        UnidadeRaizDoTipo(raizId, TipoUnidade.Reitoria);

        Result<Instituicao> resultado = await ConferirNovaAsync(ComandosDeInstituicaoDeTeste.Criacao(raizId));

        resultado.Value!.UnidadeRaizId.Should().Be(raizId);
    }

    [Fact(DisplayName = "Nova sem unidade raiz declarada não consulta nenhuma Unidade")]
    public async Task ConferirNova_SemRaiz_NaoConsultaUnidade()
    {
        await ConferirNovaAsync(ComandosDeInstituicaoDeTeste.Criacao());

        await _unidades.DidNotReceive().ObterPorIdParaLeituraAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Atualização com payload inválido recusa sem buscar a Instituição por Id — validação vence 404")]
    public async Task ConferirAtualizacao_PayloadInvalido_RecusaSemBuscar()
    {
        Result<Instituicao> resultado = await ConferirAtualizacaoAsync(
            ComandosDeInstituicaoDeTeste.Atualizacao(Guid.CreateVersion7()) with { Nome = "" });

        resultado.Error!.Code.Should().Be(InstituicaoErrorCodes.NomeObrigatorio);
        await _repo.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Atualização de Instituição inexistente recusa antes de consultar o vínculo")]
    public async Task ConferirAtualizacao_Inexistente_RecusaAntesDoVinculo()
    {
        Result<Instituicao> resultado = await ConferirAtualizacaoAsync(
            ComandosDeInstituicaoDeTeste.Atualizacao(Guid.CreateVersion7(), Guid.CreateVersion7()));

        resultado.Error!.Code.Should().Be(InstituicaoErrorCodes.NaoEncontrada);
        await _unidades.DidNotReceive().ObterPorIdParaLeituraAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Atualização com unidade raiz inexistente recusa com UnidadeRaizNaoEncontrada")]
    public async Task ConferirAtualizacao_RaizInexistente_Recusa()
    {
        Instituicao existente = InstituicaoCadastrada();

        Result<Instituicao> resultado = await ConferirAtualizacaoAsync(
            ComandosDeInstituicaoDeTeste.Atualizacao(existente.Id, Guid.CreateVersion7()));

        resultado.Error!.Code.Should().Be(InstituicaoErrorCodes.UnidadeRaizNaoEncontrada);
    }

    [Fact(DisplayName = "Atualização com unidade raiz que não é reitoria recusa com UnidadeRaizNaoEhReitoria")]
    public async Task ConferirAtualizacao_RaizNaoReitoria_Recusa()
    {
        Instituicao existente = InstituicaoCadastrada();
        Guid raizId = Guid.CreateVersion7();
        UnidadeRaizDoTipo(raizId, TipoUnidade.Centro);

        Result<Instituicao> resultado = await ConferirAtualizacaoAsync(ComandosDeInstituicaoDeTeste.Atualizacao(existente.Id, raizId));

        resultado.Error!.Code.Should().Be(InstituicaoErrorCodes.UnidadeRaizNaoEhReitoria);
    }

    [Fact(DisplayName = "Atualização com reitoria como raiz, ou sem raiz, devolve a Instituição encontrada")]
    public async Task ConferirAtualizacao_RaizReitoriaOuAusente_DevolveAInstituicao()
    {
        Instituicao existente = InstituicaoCadastrada();
        Guid raizId = Guid.CreateVersion7();
        UnidadeRaizDoTipo(raizId, TipoUnidade.Reitoria);

        Result<Instituicao> comRaiz = await ConferirAtualizacaoAsync(ComandosDeInstituicaoDeTeste.Atualizacao(existente.Id, raizId));
        Result<Instituicao> semRaiz = await ConferirAtualizacaoAsync(ComandosDeInstituicaoDeTeste.Atualizacao(existente.Id));

        comRaiz.Value.Should().BeSameAs(existente);
        semRaiz.Value.Should().BeSameAs(existente);
    }

    [Fact(DisplayName = "Só a Unidade que é reitoria viva não é recusada")]
    public void RecusaDoVinculo_SoAReitoriaVivaNaoEhRecusada()
    {
        ConferenciaDoCadastroDaInstituicao.RecusaDoVinculo(ComandosDeInstituicaoDeTeste.UnidadeDoTipo(TipoUnidade.Reitoria)).Should().BeNull();
        ConferenciaDoCadastroDaInstituicao.RecusaDoVinculo(ComandosDeInstituicaoDeTeste.UnidadeDoTipo(TipoUnidade.Centro))!
            .Code.Should().Be(InstituicaoErrorCodes.UnidadeRaizNaoEhReitoria);
        ConferenciaDoCadastroDaInstituicao.RecusaDoVinculo(null)!.Code.Should().Be(InstituicaoErrorCodes.UnidadeRaizNaoEncontrada);
    }
}
