namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Cenarios;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Os dublês e o apoio comuns aos testes da criação do processo: uma unidade com cidade, o tipo Sisu ativo, quem cria
/// com permissão e nenhum identificador em uso. Cada teste muda só o que a regra testada precisa.
/// </summary>
internal sealed class CenarioDaCriacaoDoProcesso
{
    internal static readonly Guid UnidadeId = Guid.NewGuid();

    internal static readonly UnidadeView UnidadeCeps = new(
        UnidadeId, "CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", null, "ADMINISTRATIVA", false, null,
        "1504208", "Marabá", "PA");

    internal static readonly UnidadeView UnidadeSemCidade = new(
        UnidadeId, "CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", null, "ADMINISTRATIVA", false, null);

    internal static readonly TipoProcessoView TipoSisu = new(
        TiposDeProcessoDeTeste.Sisu.OrigemId, "SISU", "SiSU", null);

    internal IProcessoSeletivoRepository Repository { get; } = Substitute.For<IProcessoSeletivoRepository>();

    internal IUnidadeReader UnidadeReader { get; } = Substitute.For<IUnidadeReader>();

    internal ITipoProcessoReader TipoProcessoReader { get; } = Substitute.For<ITipoProcessoReader>();

    internal IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

    internal IVerificadorDeAcesso Verificador { get; } = Substitute.For<IVerificadorDeAcesso>();

    internal ProcessoSeletivo? Persistido { get; private set; }

    internal CenarioDaCriacaoDoProcesso()
    {
        UnidadeReader.ObterPorIdAsync(UnidadeId, Arg.Any<CancellationToken>()).Returns(UnidadeCeps);
        TipoProcessoReader.ObterAtivoPorIdAsync(TipoSisu.Id, Arg.Any<CancellationToken>()).Returns(TipoSisu);
        ConcederAcesso(ResultadoDoAcesso.Permitido);
        Repository.When(r => r.AdicionarAsync(Arg.Any<ProcessoSeletivo>(), Arg.Any<CancellationToken>()))
            .Do(chamada => Persistido = chamada.Arg<ProcessoSeletivo>());
    }

    internal Task<Result<Guid>> CriarAsync(CriarProcessoSeletivoCommand comando) =>
        CriarProcessoSeletivoCommandHandler.Handle(comando, Repository, Verificador, UnidadeReader, TipoProcessoReader, UnitOfWork, CancellationToken.None);

    internal void ConcederAcesso(ResultadoDoAcesso resultado) =>
        Verificador.ExigirAsync(Arg.Any<PermissionRequirement>(), Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>()).Returns(resultado);

    internal void DesativarOTipo() =>
        TipoProcessoReader.ObterAtivoPorIdAsync(TipoSisu.Id, Arg.Any<CancellationToken>()).Returns((TipoProcessoView?)null);

    internal async Task NadaFoiLidoNemGravadoAsync()
    {
        await UnidadeReader.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
        await TipoProcessoReader.DidNotReceiveWithAnyArgs().ObterAtivoPorIdAsync(default, default);
        await Repository.DidNotReceiveWithAnyArgs().IdentificadorLegivelEmUsoAsync(default, default, default);
        await Repository.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
    }

    internal async Task NadaFoiGravadoAsync()
    {
        await Repository.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
        await UnitOfWork.DidNotReceiveWithAnyArgs().SalvarAlteracoesAsync(default);
    }
}
