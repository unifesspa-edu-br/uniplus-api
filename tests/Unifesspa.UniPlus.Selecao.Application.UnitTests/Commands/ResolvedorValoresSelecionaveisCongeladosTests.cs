namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// issue #1077 — §2: defesa em profundidade da fronteira Application. O caminho esperado para
/// um fato de escopo-processo sem oferta é <c>ProcessoSeletivo.PendenciaDeFatoColetadoSemValoresOfertados</c>,
/// avaliado ANTES deste resolvedor rodar — os testes abaixo chamam o resolvedor DIRETO,
/// contornando o gate, para provar que ele também nunca devolve sucesso com lista vazia.
/// </summary>
public sealed class ResolvedorValoresSelecionaveisCongeladosTests
{
    private static ProcessoSeletivo NovoProcesso() => ProcessoSeletivo.Criar(
        "PS Resolvedor", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!);

    private static FatoCandidatoView FatoCategoricoDeEscopoProcesso(string codigo) => new(
        Id: Guid.CreateVersion7(),
        Codigo: codigo,
        Nome: codigo,
        Descricao: null,
        Dominio: "CATEGORICO",
        Origem: "DECLARADO",
        Cardinalidade: "MULTIVALORADO",
        ValoresDominio: null,
        PontoResolucao: "INSCRICAO",
        Binding: $"CAMPO_INSCRICAO:{codigo}",
        ValoresDominioDeclarados: null);

    [Fact(DisplayName = "Resolver recusa CONDICAO_ATENDIMENTO coletável sem nenhuma condição ofertada")]
    public void Resolver_CondicaoAtendimentoSemOferta_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(
            [FatoColetado.Criar("CONDICAO_ATENDIMENTO", 0, "Condição de atendimento", TipoRenderizacao.SelecaoMultipla, false, null).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Dictionary<string, FatoCandidatoView> catalogo = new(StringComparer.Ordinal)
        {
            ["CONDICAO_ATENDIMENTO"] = FatoCategoricoDeEscopoProcesso("CONDICAO_ATENDIMENTO"),
        };

        Result<IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>> resultado =
            ResolvedorValoresSelecionaveisCongelados.Resolver(processo, catalogo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.FatoColetadoSemValoresOfertados");
    }

    [Fact(DisplayName = "Resolver aceita CONDICAO_ATENDIMENTO coletável com condição ofertada e devolve lista não vazia")]
    public void Resolver_CondicaoAtendimentoComOferta_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar(
                [OfertaCondicao.Criar(Guid.CreateVersion7(), "PCD", "Pessoa com deficiência")], [], []).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(
            [FatoColetado.Criar("CONDICAO_ATENDIMENTO", 0, "Condição de atendimento", TipoRenderizacao.SelecaoMultipla, false, null).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Dictionary<string, FatoCandidatoView> catalogo = new(StringComparer.Ordinal)
        {
            ["CONDICAO_ATENDIMENTO"] = FatoCategoricoDeEscopoProcesso("CONDICAO_ATENDIMENTO"),
        };

        Result<IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>> resultado =
            ResolvedorValoresSelecionaveisCongelados.Resolver(processo, catalogo);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!["CONDICAO_ATENDIMENTO"].Should().ContainSingle(v => v.Codigo == "PCD");
    }
}
