namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A consulta prévia do candidato (ADR-0143) avalia a oferta que ele tem no seletor, ainda sem salvar:
/// ela vale mais do que a gravada na inscrição, e, nula, é o seletor sem oferta — nunca a gravada.
/// </summary>
public sealed class AvaliarInscricaoQueryHandlerTests
{
    private static readonly Guid ProcessoId = Guid.CreateVersion7();
    private static readonly Guid OfertaA = Guid.CreateVersion7();
    private static readonly Guid OfertaB = Guid.CreateVersion7();

    [Fact(DisplayName = "A oferta do corpo vale mais do que a gravada: quem escolheu a oferta A na tela não vê a modalidade que só a oferta B tem")]
    public async Task Handle_OfertaNoCorpo_PrevaleceSobreAGravada()
    {
        AvaliacaoDaInscricaoDto naTela = await AvaliarAsync(gravada: OfertaB, noCorpo: OfertaA);

        naTela.Modalidades.Itens.Select(static m => m.Codigo).Should().Equal("AC");
    }

    [Fact(DisplayName = "Oferta nula no corpo é o seletor desmarcado: a avaliação não volta para a oferta gravada")]
    public async Task Handle_OfertaNulaNoCorpo_NaoUsaAGravada()
    {
        AvaliacaoDaInscricaoDto semOferta = await AvaliarAsync(gravada: OfertaA, noCorpo: null);

        semOferta.Modalidades.Itens.Select(static m => m.Codigo).Should().Equal("AC", "AC_FEM");
    }

    private static async Task<AvaliacaoDaInscricaoDto> AvaliarAsync(Guid? gravada, Guid? noCorpo)
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, GrafoConfiguracao grafoBase) = DivulgacaoDeTeste.VersaoConforme(ProcessoId);
        GrafoConfiguracao grafo = OfertasDeTeste.ComDistribuicao(
            grafoBase, OfertasDeTeste.Distribuicao(OfertaA, "AC"), OfertasDeTeste.Distribuicao(OfertaB, "AC", "AC_FEM"));
        Inscricao inscricao = Inscricao.Iniciar(ProcessoId, cpf, "sub-1", "https://issuer", versao.Id).Value!;
        if (gravada is { } oferta)
        {
            inscricao.Salvar(
                oferta, null, new Dictionary<string, JsonElement>(), new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(), [],
                ContextoDaVersaoFixadaResolver.Admitidas(grafo) with { NomeCivilDoLogin = PersonasDeTeste.NomeFernandaDrumond }, PrecondicaoIfMatch.DeTags([inscricao.ETag])).IsSuccess.Should().BeTrue();
        }

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(inscricao);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);
        IUserContext usuario = Substitute.For<IUserContext>();
        usuario.Cpf.Returns(cpf.Valor);
        usuario.UserId.Returns("sub-1");

        AvaliacaoDaInscricaoRequest corpo = new(
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                [OfertasDeTeste.FatoDaAcaoAfirmativa] = JsonSerializer.SerializeToElement(OfertasDeTeste.ValorDaAcaoAfirmativa),
            },
            null,
            noCorpo);

        Result<AvaliacaoDaInscricaoDto> resultado = await AvaliarInscricaoQueryHandler.Handle(
            new AvaliarInscricaoQuery(ProcessoId, corpo),
            inscricaoRepository, processoRepository, DivulgacaoDeTeste.Certame(ProcessoId), registroCodecs, DivulgacaoDeTeste.Fuso(),
            Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(), usuario, DivulgacaoDeTeste.Relogio,
            CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        return resultado.Value!;
    }
}
