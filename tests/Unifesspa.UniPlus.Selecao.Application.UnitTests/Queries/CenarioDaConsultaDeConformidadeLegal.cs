namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Os repositórios, o leitor de modalidades e o fuso com que a consulta pública de conformidade legal é chamada.
/// Parte de um processo que o repositório devolve, sem regra vigente, com as modalidades do cadastro vivo e o
/// fuso institucional resolvido; cada teste troca só o que ele prova.
/// </summary>
internal sealed record CenarioDaConsultaDeConformidadeLegal(
    IProcessoSeletivoRepository Processos,
    IObrigatoriedadeLegalRepository Obrigatoriedades,
    IModalidadeReader Modalidades,
    IResolvedorFusoInstitucional Fuso)
{
    internal static CenarioDaConsultaDeConformidadeLegal ComOProcesso(ProcessoSeletivo processo)
    {
        IProcessoSeletivoRepository processos = Substitute.For<IProcessoSeletivoRepository>();
        processos.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        return SobRepositorio(processos);
    }

    internal static CenarioDaConsultaDeConformidadeLegal SemNenhumProcesso()
    {
        IProcessoSeletivoRepository processos = Substitute.For<IProcessoSeletivoRepository>();
        processos.ObterComConfiguracaoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ProcessoSeletivo?)null);

        return SobRepositorio(processos);
    }

    internal CenarioDaConsultaDeConformidadeLegal ComRegrasVigentesNaDataDeCorte(DateOnly dataDeCorte, params ObrigatoriedadeLegal[] regras)
    {
        Obrigatoriedades.ObterVigentesParaTipoProcessoAsync(Arg.Any<string>(), dataDeCorte, Arg.Any<CancellationToken>()).Returns(regras);

        return this;
    }

    internal CenarioDaConsultaDeConformidadeLegal ComRegrasVigentesEmQualquerData(params ObrigatoriedadeLegal[] regras)
    {
        Obrigatoriedades.ObterVigentesParaTipoProcessoAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(regras);

        return this;
    }

    internal CenarioDaConsultaDeConformidadeLegal SemModalidadeNoCadastro()
    {
        IModalidadeReader modalidades = Substitute.For<IModalidadeReader>();
        modalidades.ObterVivaPorCodigoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((ModalidadeView?)null);

        return this with { Modalidades = modalidades };
    }

    internal CenarioDaConsultaDeConformidadeLegal ComFusoInstitucionalIndisponivel() =>
        this with { Fuso = new ResolvedorFusoIndisponivelDeTeste() };

    internal Task<Result<ConformidadeLegalProcessoSeletivoDto>> ConsultarAsync(ObterConformidadeLegalProcessoSeletivoQuery consulta) =>
        ObterConformidadeLegalProcessoSeletivoQueryHandler.Handle(
            consulta,
            Processos,
            ConferenciaDeConformidade(),
            Fuso,
            CadastrosVivos.CatalogoDeFatos(),
            CancellationToken.None);

    private ConferenciaDeConformidadeLegal ConferenciaDeConformidade() =>
        new(
            Obrigatoriedades,
            Modalidades,
            CadastrosVivos.TiposDocumento(),
            CadastrosVivos.TiposEtapa(),
            CadastrosVivos.TiposDeficiencia(),
            CadastrosVivos.RegrasDesempate());

    private static CenarioDaConsultaDeConformidadeLegal SobRepositorio(IProcessoSeletivoRepository processos) =>
        new(processos, Substitute.For<IObrigatoriedadeLegalRepository>(), CadastrosVivos.Modalidades(), new ResolvedorFusoDeTeste());
}
