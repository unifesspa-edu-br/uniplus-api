namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A leitura administrativa do agregado precisa devolver a convenção de contagem declarada
/// (UNI-REQ-0112), como faz com as demais dimensões editáveis.
/// </summary>
/// <remarks>
/// Sem isso, a tela de edição não distingue "ainda não declarei" de "declarei e não sei
/// qual", e reenviaria às cegas depois de um reload — o mesmo motivo pelo qual formulário,
/// divulgação e taxa aparecem ali. A identidade sai completa, com o hash: é ele que permite
/// ao cliente saber se a definição referenciada ainda é a que o catálogo tem.
/// </remarks>
public sealed class ObterProcessoSeletivoQueryHandlerAlgoritmoContagemTests
{
    private const string HashDaConvencao = "0a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f9";

    private static async Task<ProcessoSeletivoDto?> ObterDtoAsync(ReferenciaRegra? algoritmo)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Query Contagem", TipoProcesso.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

        if (algoritmo is not null)
        {
            processo.DefinirAlgoritmoContagemPrazo(algoritmo, PrecondicaoIfMatch.Ausente)
                .IsSuccess.Should().BeTrue();
        }

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        return await ObterProcessoSeletivoQueryHandler.Handle(
            new ObterProcessoSeletivoQuery(processo.Id), repository, CancellationToken.None);
    }

    [Fact(DisplayName = "A convenção declarada é projetada com a identidade completa — código, versão e hash")]
    public async Task Handle_ProjetaAConvencaoDeclarada()
    {
        ReferenciaRegra algoritmo = ReferenciaRegra.Criar(
            AlgoritmoContagemPrazoCodigo.AvancaDataUtil, "v1", HashDaConvencao).Value!;

        ProcessoSeletivoDto? dto = await ObterDtoAsync(algoritmo);

        dto.Should().NotBeNull();
        dto!.AlgoritmoContagemPrazo.Should().NotBeNull();
        dto.AlgoritmoContagemPrazo!.Codigo.Should().Be(AlgoritmoContagemPrazoCodigo.AvancaDataUtil);
        dto.AlgoritmoContagemPrazo.Versao.Should().Be("v1");
        dto.AlgoritmoContagemPrazo.Hash.Should().Be(HashDaConvencao,
            "sem o hash o cliente não consegue saber se a definição referenciada ainda é a do catálogo");
    }

    [Fact(DisplayName = "Sem convenção declarada, a projeção devolve ausência — que é estado válido, não lacuna")]
    public async Task Handle_SemConvencao_ProjetaAusencia()
    {
        ProcessoSeletivoDto? dto = await ObterDtoAsync(algoritmo: null);

        dto.Should().NotBeNull();
        dto!.AlgoritmoContagemPrazo.Should().BeNull(
            "um certame sem contagem que distinga dia útil publica sem declarar convenção");
    }
}
