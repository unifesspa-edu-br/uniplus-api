namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;

using Unifesspa.UniPlus.Autorizacao.Application.Commands.Termos;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDePerfis;

/// <summary>A publicação de uma versão do termo pelo handler: autor, validação, conflito e gravação.</summary>
public sealed class PublicacaoDoTermoTests
{
    private readonly ITermoDeResponsabilidadeRepository _termos = Substitute.For<ITermoDeResponsabilidadeRepository>();
    private readonly UnidadeDeTrabalho _unidade = new();

    public PublicacaoDoTermoTests() =>
        _termos.ObterParaPublicarAsync(Arg.Any<CancellationToken>()).Returns((TermosDeTeste.RegistroSemVersao(), (VersaoDoTermo?)null));

    [Fact(DisplayName = "A primeira publicação grava a versão 1, vigente, com o resumo do texto devolvido")]
    public async Task Publicar_Primeira_Grava()
    {
        Result<VersaoDoTermoDto> resultado = await PublicarAsync(new(TermosDeTeste.Texto(), TermosDeTeste.Ato, null));

        resultado.Value!.Numero.Should().Be(1);
        resultado.Value.Vigente.Should().BeTrue();
        resultado.Value.Resumo.Should().Be(Domain.ValueObjects.ConteudoDoTermo.Resumir(resultado.Value.Texto));
        await _termos.Received(1).AdicionarVersaoAsync(Arg.Any<VersaoDoTermo>(), Arg.Any<CancellationToken>());
        _unidade.Gravacoes.Should().Be(1);
    }

    [Fact(DisplayName = "O texto inválido é recusado antes de travar a vigência")]
    public async Task Publicar_TextoInvalido_NaoTrava()
    {
        Result<VersaoDoTermoDto> resultado = await PublicarAsync(new("curto", TermosDeTeste.Ato, null));

        resultado.Error!.Code.Should().Be(TermoErrorCodes.TextoInvalido);
        await _termos.DidNotReceive().ObterParaPublicarAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A vigente esperada errada é recusada sem gravar, com o rastreamento descartado")]
    public async Task Publicar_VigenteAlterada_Descarta()
    {
        Result<VersaoDoTermoDto> resultado = await PublicarAsync(new(TermosDeTeste.Texto(), TermosDeTeste.Ato, Guid.NewGuid()));

        resultado.Error!.Code.Should().Be(TermoErrorCodes.VigenteAlterado);
        (_unidade.Gravacoes, _unidade.Descartes).Should().Be((0, 1));
    }

    [Fact(DisplayName = "O conflito de concorrência na gravação vira recusa retentável, com o rastreamento descartado")]
    public async Task Publicar_ConflitoNaGravacao_Recusa()
    {
        _unidade.Falha = new DbUpdateConcurrencyException();

        Result<VersaoDoTermoDto> resultado = await PublicarAsync(new(TermosDeTeste.Texto(), TermosDeTeste.Ato, null));

        resultado.Error!.Code.Should().Be(TermoErrorCodes.ConflitoDeConcorrencia);
        _unidade.Descartes.Should().Be(1);
    }

    [Fact(DisplayName = "Sem autor identificado no token, nada é publicado")]
    public async Task Publicar_SemAutor_Recusa()
    {
        Result<VersaoDoTermoDto> resultado = await PublicarVersaoDoTermoCommandHandler.Handle(
            new(TermosDeTeste.Texto(), TermosDeTeste.Ato, null), Publicacao(), new Usuario(null), CancellationToken.None);

        resultado.Error!.Code.Should().Be(TermoErrorCodes.AutorNaoIdentificado);
    }

    private Task<Result<VersaoDoTermoDto>> PublicarAsync(PublicarVersaoDoTermoCommand command) =>
        PublicarVersaoDoTermoCommandHandler.Handle(command, Publicacao(), new Usuario(TermosDeTeste.Autor), CancellationToken.None);

    private PublicacaoDoTermo Publicacao() => new(_termos, _unidade, TimeProvider.System);
}
