namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

internal static class MontagemDaOfertaDeCurso
{
    internal static readonly Guid CursoId = Guid.CreateVersion7();

    internal static readonly Guid LocalOfertaId = Guid.CreateVersion7();

    internal static UnidadeOfertante Unidade() =>
        UnidadeOfertante.Criar(
            Guid.CreateVersion7(), "FACET", "Faculdade de Computação e Engenharia Elétrica", "Faculdade").Value!;

    /// <summary>
    /// Cria a oferta a partir de uma declaração válida; o teste informa só o que difere dela, com <c>ajuste</c>.
    /// </summary>
    internal static Result<OfertaCurso> Criar(
        Func<DeclaracaoDaOfertaDeCurso, DeclaracaoDaOfertaDeCurso>? ajuste = null,
        UnidadeOfertante? unidade = null)
    {
        DeclaracaoDaOfertaDeCurso declaracao = new(
            "ENF_BACHARELADO_MARABA", "REGULAR", "PRESENCIAL", "EXTENSIVO", "REGULAR", ["MATUTINO"],
            "123456", "ENG-01", 40, null, "Portaria MEC 123/2020");

        return OfertaCurso.Criar(CursoId, LocalOfertaId, unidade ?? Unidade(), ajuste?.Invoke(declaracao) ?? declaracao);
    }
}
