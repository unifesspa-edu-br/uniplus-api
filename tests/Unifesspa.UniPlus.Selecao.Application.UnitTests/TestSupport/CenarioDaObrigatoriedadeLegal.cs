namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using Microsoft.EntityFrameworkCore;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ObrigatoriedadesLegais;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.LeitoresDeCadastroParaObrigatoriedadeLegal;

/// <summary>
/// Os colaboradores de criar e atualizar uma obrigatoriedade legal, com cadastros vivos por padrão: o teste
/// troca só o que o cenário muda e executa o handler.
/// </summary>
internal sealed class CenarioDaObrigatoriedadeLegal
{
    internal const string TipoProcessoDoCenario = "PS_NOVO";

    internal IObrigatoriedadeLegalRepository Repositorio { get; } = Substitute.For<IObrigatoriedadeLegalRepository>();

    internal IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

    internal ITipoProcessoReader TiposDeProcesso { get; set; } = TipoProcessoAtivo(TipoProcessoDoCenario);

    internal ITipoEtapaReader TiposDeEtapa { get; set; } = TipoEtapaReaderAtivo();

    internal IModalidadeReader Modalidades { get; set; } = ModalidadeReaderViva();

    internal ITipoDocumentoReader TiposDeDocumento { get; set; } = TipoDocumentoReaderVivo();

    internal ITipoDeficienciaReader TiposDeDeficiencia { get; set; } = CadastrosVivos.TiposDeficiencia();

    internal IRegraCatalogoReader RegrasDeDesempate { get; set; } = CadastrosVivos.RegrasDesempate();

    internal static CriarObrigatoriedadeLegalCommand ComandoDeCriar(PredicadoObrigatoriedade? predicado = null) =>
        new(
            TipoProcessoDoCenario,
            CategoriaObrigatoriedade.Etapa.Codigo(),
            "REGRA_NOVA",
            predicado ?? new EtapaObrigatoria(TipoEtapaCodigo),
            "Descrição da regra.",
            "Lei de teste.",
            new DateOnly(2026, 1, 1),
            null,
            null,
            null);

    internal static AtualizarObrigatoriedadeLegalCommand ComandoDeAtualizar(Guid id, PredicadoObrigatoriedade? predicado = null) =>
        new(
            id,
            TipoProcessoDoCenario,
            CategoriaObrigatoriedade.Etapa.Codigo(),
            "REGRA_GRAVADA",
            predicado ?? new EtapaObrigatoria(TipoEtapaCodigo),
            "Descrição da regra.",
            "Lei de teste.",
            new DateOnly(2026, 1, 1),
            null,
            null,
            null);

    internal static ObrigatoriedadeLegal RegraGravada() =>
        ObrigatoriedadeLegal.Criar(
            TipoProcessoDoCenario,
            CategoriaObrigatoriedade.Etapa,
            "REGRA_GRAVADA",
            new EtapaObrigatoria(TipoEtapaCodigo),
            new TextoNormativo("Regra já gravada.", "Lei 12.711/2012", null, null),
            new DateOnly(2026, 1, 1),
            null).Value!;

    internal static DbUpdateException ViolacaoDeUnicidade(string constraint) =>
        new("violação sintética", new ExcecaoDoProvedor(constraint));

    internal Task<Result<Guid>> CriarAsync(CriarObrigatoriedadeLegalCommand comando) =>
        CriarObrigatoriedadeLegalCommandHandler.Handle(comando, Repositorio, Conferencia(), UnitOfWork, CancellationToken.None);

    internal Task<Result> AtualizarAsync(AtualizarObrigatoriedadeLegalCommand comando) =>
        AtualizarObrigatoriedadeLegalCommandHandler.Handle(comando, Repositorio, Conferencia(), UnitOfWork, CancellationToken.None);

    internal void ComRegraGravada(ObrigatoriedadeLegal regra) =>
        Repositorio.ObterPorIdAsync(regra.Id, Arg.Any<CancellationToken>()).Returns(regra);

    private ConferenciaDaObrigatoriedadeLegal Conferencia() =>
        LeitoresDeCadastroParaObrigatoriedadeLegal.Conferencia(
            TiposDeProcesso, TiposDeEtapa, Modalidades, TiposDeDocumento, TiposDeDeficiencia, RegrasDeDesempate);

    /// <summary>Imita a exceção do provedor, que expõe o SQLSTATE e o nome do índice violado.</summary>
    private sealed class ExcecaoDoProvedor(string constraintName) : Exception("violação de unicidade sintética")
    {
        public string SqlState { get; } = "23505";

        public string ConstraintName => constraintName;
    }
}
