namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.DublesDoCongelamentoDeTeste;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.PublicacaoDeTeste;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.RegrasLegaisDeTeste;

/// <summary>
/// As dependências dos três handlers que congelam uma versão (Publicar, Retificar e FecharRetificacao) e a
/// chamada a cada um. Os cadastros vêm com o que a suíte usa; o teste troca só o que quer descrever. A lista
/// de dependências de cada handler fica aqui, de modo que uma mudança de assinatura se faz num lugar só.
/// </summary>
internal sealed class DependenciasDosHandlersQueCongelam
{
    private readonly ServicosDoCongelamentoDeTeste _servicos;

    private DependenciasDosHandlersQueCongelam(ProcessoSeletivo processo)
    {
        Processo = processo;
        _servicos = ServicosDoCongelamentoDeTeste.Para(processo)
            .ComObrigatoriedadesLegais(RepositorioCom());
    }

    public ProcessoSeletivo Processo { get; }

    public ServicosDoCongelamentoDeTeste Servicos => _servicos;

    public IProcessoSeletivoRepository Repositorio => _servicos.Repositorio;

    public IAtoDoProcessoRepository Atos
    {
        get => _servicos.Atos;
        set => _servicos.ComAtos(value);
    }

    public ISnapshotPublicacaoCanonicalizer Canonicalizer
    {
        get => _servicos.Canonicalizer;
        set => _servicos.ComCanonicalizer(value);
    }

    public ITipoAtoPublicadoReader TiposDeAto
    {
        get => _servicos.TiposDeAto;
        set => _servicos.ComTiposDeAto(value);
    }

    public IObrigatoriedadeLegalRepository ObrigatoriedadesLegais
    {
        get => _servicos.ObrigatoriedadesLegais;
        set => _servicos.ComObrigatoriedadesLegais(value);
    }

    public IModalidadeReader Modalidades
    {
        get => _servicos.Modalidades;
        set => _servicos.ComModalidades(value);
    }

    public ITipoDocumentoReader TiposDocumento
    {
        get => _servicos.TiposDocumento;
        set => _servicos.ComTiposDocumento(value);
    }

    public IFatoCandidatoReader CatalogoDeFatos
    {
        get => _servicos.CatalogoDeFatos;
        set => _servicos.ComCatalogoDeFatos(value);
    }

    public IResolvedorDaUnidadeResponsavel Unidades
    {
        get => _servicos.Unidades;
        set => _servicos.ComUnidades(value);
    }

    public IRegistroCodecsEnvelope Codecs
    {
        get => _servicos.Codecs;
        set => _servicos.ComCodecs(value);
    }

    public static DependenciasDosHandlersQueCongelam Para(ProcessoSeletivo processo) => new(processo);

    /// <summary>O processo já publicado, com a versão publicada como a vigente do repositório.</summary>
    public static DependenciasDosHandlersQueCongelam ParaProcessoPublicado()
    {
        (ProcessoSeletivo processo, VersaoConfiguracao versaoAtual) = ProcessoPublicado();

        return ComVersaoVigente(processo, versaoAtual);
    }

    /// <summary>O processo publicado, com a sessão editorial de retificação aberta.</summary>
    public static DependenciasDosHandlersQueCongelam ParaProcessoEmRetificacao()
    {
        (ProcessoSeletivo processo, VersaoConfiguracao versaoAtual) = ProcessoPublicado();
        AbrirRetificacao(processo, versaoAtual);

        return ComVersaoVigente(processo, versaoAtual);
    }

    public Task<(Result Resposta, IEnumerable<object> Eventos)> PublicarAsync() =>
        _servicos.PublicarAsync(
            new PublicarProcessoSeletivoCommand(Processo.Id, "001/2026", null, null, DocumentoEditalId: Guid.CreateVersion7(), Ato: NovoAto()));

    public Task<(Result Resposta, IEnumerable<object> Eventos)> RetificarAsync() =>
        _servicos.RetificarAsync(
            new RetificarProcessoSeletivoCommand(
                Processo.Id, "Correção do prazo", "001/2026-R1", null, null, DocumentoEditalId: Guid.CreateVersion7(), Ato: NovoAto()));

    public Task<(Result Resposta, IEnumerable<object> Eventos)> FecharRetificacaoAsync() =>
        _servicos.FecharRetificacaoAsync(
            new FecharRetificacaoCommand(
                Processo.Id, "001/2026-R1", null, null, DocumentoEditalId: Guid.CreateVersion7(), Ato: NovoAto(), Precondicao: PrecondicaoIfMatch.Curinga));

    private static DependenciasDosHandlersQueCongelam ComVersaoVigente(ProcessoSeletivo processo, VersaoConfiguracao versaoAtual)
    {
        DependenciasDosHandlersQueCongelam dependencias = Para(processo);
        dependencias.Repositorio.ObterVersaoAtualAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(versaoAtual);

        return dependencias;
    }
}
