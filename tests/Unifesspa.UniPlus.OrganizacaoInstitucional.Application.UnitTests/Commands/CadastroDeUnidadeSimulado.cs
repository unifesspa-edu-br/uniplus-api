namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.UnitTests.Commands;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Abstractions;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Unidades;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A conferência e a gravação da Unidade montadas sobre repositórios, unidade de trabalho e cache simulados,
/// com os comandos do CEPS e os cenários de identificador em uso e de superior que os testes repetem.
/// </summary>
internal sealed class CadastroDeUnidadeSimulado
{
    public CadastroDeUnidadeSimulado()
    {
        Conferencia = new ConferenciaDoCadastroDaUnidade(Unidades, Instituicoes);
        Gravacao = new GravacaoDaUnidade(Unidades, UnitOfWork, Cache);
    }

    public IUnidadeRepository Unidades { get; } = Substitute.For<IUnidadeRepository>();

    public IInstituicaoRepository Instituicoes { get; } = Substitute.For<IInstituicaoRepository>();

    public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

    public IUnidadeCacheInvalidator Cache { get; } = Substitute.For<IUnidadeCacheInvalidator>();

    public ConferenciaDoCadastroDaUnidade Conferencia { get; }

    public GravacaoDaUnidade Gravacao { get; }

    public static CriarUnidadeCommand Criacao() => new(
        "Centro de Processos Seletivos",
        null,
        "ceps",
        "CEPS",
        "0001",
        null,
        TipoUnidade.Centro.Codigo(),
        false,
        UnidadeDeTeste.InicioDaVigencia,
        null);

    /// <summary>Reapresenta os identificadores do CEPS: nenhuma conferência de unicidade é devida.</summary>
    public static AtualizarUnidadeCommand AtualizacaoSemMudancaDeIdentificador(Guid id) => new(
        id,
        "Centro de Processos Seletivos (novo nome)",
        null,
        "ceps",
        "CEPS",
        "0001",
        null,
        TipoUnidade.Centro.Codigo(),
        false,
        null);

    /// <summary>O CEPS já gravado, que o repositório encontra pelo Id.</summary>
    public Unidade UnidadeCadastrada(string codigo = "0001")
    {
        Unidade existente = UnidadeDeTeste.Criar(new IdentificadoresDaUnidade("ceps", "CEPS", codigo, null)).Value!;
        Unidades.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        return existente;
    }

    /// <summary>Uma Unidade existente, que o repositório encontra pelo Id devolvido.</summary>
    public Guid SuperiorExistente()
    {
        Guid superiorId = Guid.NewGuid();
        Unidades.ObterPorIdAsync(superiorId, Arg.Any<CancellationToken>()).Returns(UnidadeDeTeste.Criar().Value!);
        return superiorId;
    }

    public void SlugEmUso() =>
        Unidades.SlugExisteEntreVivosAsync(Arg.Any<Slug>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(true);

    public void SiglaEmUso() =>
        Unidades.SiglaExisteEntreVivosAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(true);

    public void CodigoEmUso() =>
        Unidades.CodigoExisteEntreVivosAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(true);

    public Task SlugNaoConsultadoAsync() =>
        Unidades.DidNotReceive().SlugExisteEntreVivosAsync(Arg.Any<Slug>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());

    public Task SiglaNaoConsultadaAsync() =>
        Unidades.DidNotReceive().SiglaExisteEntreVivosAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());

    public Task CodigoNaoConsultadoAsync() =>
        Unidades.DidNotReceive().CodigoExisteEntreVivosAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
}
