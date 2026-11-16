namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

/// <summary>A leitura devolve os procedimentos declarados na ordem canônica, com o alvo do tipo que foi declarado.</summary>
public sealed class ProjecaoDosProcedimentosDeRecursoTests
{
    private static readonly Guid Item = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly ReferenciaRegra Regra = ReferenciaRegra.De(RegraProcedimentoRecursoCodigo.Recalculo, "v1", new string('a', 64));

    [Fact(DisplayName = "Sem procedimentos, a leitura é uma lista vazia")]
    public void Nenhum_ListaVazia() =>
        ProjecaoDosProcedimentosDeRecurso.De(ProcedimentosDeRecurso.Nenhum).Should().BeEmpty();

    [Fact(DisplayName = "Cada procedimento projeta o tipo, o alvo de item ou de área, e a referência congelada da regra")]
    public void Procedimentos_ProjetamTipoAlvoERegra()
    {
        ProcedimentosDeRecurso procedimentos = ProcedimentosDeRecurso.De(
        [
            ProcedimentoDeRecursoDeclarado.De(TipoMotivoDeDecisao.Classificacao, AlvoDoProcedimentoDeRecurso.DaArea(AreaDoEnem.Redacao), Regra),
            ProcedimentoDeRecursoDeclarado.De(TipoMotivoDeDecisao.AnaliseDocumental, AlvoDoProcedimentoDeRecurso.DoItem(Item), Regra),
            ProcedimentoDeRecursoDeclarado.De(TipoMotivoDeDecisao.HomologacaoDasInscricoes, null, Regra),
        ]);

        IReadOnlyList<ProcedimentoDeRecursoDto> leitura = ProjecaoDosProcedimentosDeRecurso.De(procedimentos);

        ReferenciaRegraDto referencia = new(Regra.Codigo, Regra.Versao, Regra.Hash);
        leitura.Should().Equal(
            new ProcedimentoDeRecursoDto("ANALISE_DOCUMENTAL", new AlvoDoProcedimentoDeRecursoDto(Item, null), referencia),
            new ProcedimentoDeRecursoDto("CLASSIFICACAO", new AlvoDoProcedimentoDeRecursoDto(null, AreaDoEnem.Redacao), referencia),
            new ProcedimentoDeRecursoDto("HOMOLOGACAO_DAS_INSCRICOES", null, referencia));
    }
}
