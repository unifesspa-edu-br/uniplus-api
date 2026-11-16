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

    internal static Result<OfertaCurso> Criar(
        string? codigo = "ENF_BACHARELADO_MARABA",
        string? programaDeOferta = "REGULAR",
        string? formatoPedagogico = "PRESENCIAL",
        string? regimeDeFuncionamento = "EXTENSIVO",
        string? regimeDeTurno = "REGULAR",
        IReadOnlyList<string?>? turnos = null,
        string? eMecCodigo = "123456",
        string? codigoSga = "ENG-01",
        int? vagasAnuaisAutorizadas = 40,
        string? baseLegal = null,
        string? atoAutorizacaoMec = "Portaria MEC 123/2020",
        UnidadeOfertante? unidade = null) =>
        OfertaCurso.Criar(
            CursoId, LocalOfertaId, unidade ?? Unidade(),
            new DeclaracaoDaOfertaDeCurso(
                codigo, programaDeOferta, formatoPedagogico, regimeDeFuncionamento, regimeDeTurno, turnos ?? ["MATUTINO"],
                eMecCodigo, codigoSga, vagasAnuaisAutorizadas, baseLegal, atoAutorizacaoMec));
}
