namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

/// <summary>A leitura devolve os procedimentos declarados na ordem canônica, com o alvo do tipo que foi declarado.</summary>
public sealed class ProjecaoDosProcedimentosDeRecursoTests
{
    private static readonly Guid Item = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly ReferenciaRegra Regra = ReferenciaRegra.De(RegraDeProcedimentoDoRecurso.Recalculo.Codigo(), "v1", new string('a', 64));

    private static CodigoComRotulo<TipoMotivoDeDecisao> Tipo(TipoMotivoDeDecisao tipo) => CodigoComRotulo<TipoMotivoDeDecisao>.De(tipo);

    [Fact(DisplayName = "Sem procedimentos, a leitura é uma lista vazia")]
    public void Nenhum_ListaVazia() =>
        ProjecaoDosProcedimentosDeRecurso.De(ProcedimentosDeRecurso.Nenhum, OrigemDaNotaDaJanela.DaFase).Should().BeEmpty();

    [Fact(DisplayName = "Cada procedimento projeta o tipo, o alvo de item ou de área, e a referência congelada da regra")]
    public void Procedimentos_ProjetamTipoAlvoERegra()
    {
        ProcedimentosDeRecurso procedimentos = ProcedimentosDeRecurso.De(
        [
            ProcedimentoDeRecursoDeclarado.De(TipoMotivoDeDecisao.Classificacao, AlvoDoProcedimentoDeRecurso.DaArea(AreaDoEnem.Redacao), Regra),
            ProcedimentoDeRecursoDeclarado.De(TipoMotivoDeDecisao.AnaliseDocumental, AlvoDoProcedimentoDeRecurso.DoItem(Item), Regra),
            ProcedimentoDeRecursoDeclarado.De(TipoMotivoDeDecisao.HomologacaoDasInscricoes, null, Regra),
        ]);

        IReadOnlyList<ProcedimentoDeRecursoDto> leitura = ProjecaoDosProcedimentosDeRecurso.De(procedimentos, new(FormaDeAvaliacao.Humana, false));

        ReferenciaRegraDto referencia = new(Regra.Codigo, Regra.Versao, Regra.Hash);
        leitura.Should().Equal(
            new ProcedimentoDeRecursoDto(Tipo(TipoMotivoDeDecisao.AnaliseDocumental), new AlvoDoProcedimentoDeRecursoDto(Item, null), referencia, RegraDeProcedimentoDoRecurso.Recorrecao.Codigo()),
            new ProcedimentoDeRecursoDto(Tipo(TipoMotivoDeDecisao.Classificacao), new AlvoDoProcedimentoDeRecursoDto(null, AreaDoEnem.Redacao), referencia, RegraDeProcedimentoDoRecurso.Recalculo.Codigo()),
            new ProcedimentoDeRecursoDto(Tipo(TipoMotivoDeDecisao.HomologacaoDasInscricoes), null, referencia, null));
    }

    [Fact(DisplayName = "A sugestão acompanha a forma da janela e não muda a regra declarada")]
    public void Sugestao_AcompanhaAJanelaESemAlterarODeclarado()
    {
        ProcedimentosDeRecurso procedimentos = ProcedimentosDeRecurso.De(
            [ProcedimentoDeRecursoDeclarado.De(TipoMotivoDeDecisao.AnaliseDocumental, AlvoDoProcedimentoDeRecurso.DoItem(Item), Regra)]);

        ProcedimentoDeRecursoDto objetiva = ProjecaoDosProcedimentosDeRecurso.De(procedimentos, new(FormaDeAvaliacao.Objetiva, false)).Single();

        objetiva.Sugestao.Should().Be(RegraDeProcedimentoDoRecurso.Recalculo.Codigo());
        objetiva.Regra.Codigo.Should().Be(Regra.Codigo);
    }
}
