namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>Montagens comuns dos testes do formulário renderizável: divulgação, registro de codecs, envelope reidratado e chamada ao handler.</summary>
internal static class ApoioDoFormularioRenderizavel
{
    /// <summary>
    /// A versão que o registro de codecs FAKE (<see cref="RegistroReconhecendoVersaoCorrente"/>)
    /// declara como única capacidade de leitura — o caso corrente de todo teste que não é sobre o
    /// verificação da versão em si.
    /// </summary>
    internal const string VersaoCorrenteReconhecida = "0.0.10";

    internal static readonly TimeProvider Relogio = TimeProvider.System;

    /// <summary>
    /// Substituto da porta reconhecendo, como única capacidade de leitura, exatamente
    /// <see cref="VersaoCorrenteReconhecida"/> — o mesmo perfil do registro de produção, que hoje
    /// tem um único codec vivo (ADR-0110 Emenda 2).
    /// </summary>
    internal static readonly IRegistroCodecsEnvelope RegistroReconhecendoVersaoCorrente =
        CriarRegistroReconhecendo(VersaoCorrenteReconhecida);

    /// <summary>
    /// Ato que confirmou a publicidade da versão servida. Único em todo o arquivo: os testes não
    /// são sobre QUAL versão a divulgação aponta — isso é dos testes da vitrine —, e sim sobre o
    /// que a projeção faz com a versão que ela aponta.
    /// </summary>
    internal static readonly Guid IdDoAtoDaDivulgacao = Guid.CreateVersion7();

    /// <summary>Os endereços do acervo público, com o endereço base da borda fixado para o teste, sobre a divulgação lida.</summary>
    internal static EnderecosDosDocumentosDoAto Enderecos(ICertameDivulgadoRepository divulgacao)
    {
        IEnderecoNoAcervoPublico acervo = Substitute.For<IEnderecoNoAcervoPublico>();
        acervo.De(Arg.Any<string>()).Returns(static chamada => new Uri($"https://acervo.teste/{chamada.Arg<string>()}"));
        return new EnderecosDosDocumentosDoAto(divulgacao, acervo);
    }

    internal static Task<Result<FormularioRenderizavelDto>> HandleAsync(
        IProcessoSeletivoRepository repository,
        Guid processoId,
        ICertameDivulgadoRepository? divulgadoRepository = null,
        FinalidadeFormulario finalidade = FinalidadeFormulario.Inscricao)
    {
        ICertameDivulgadoRepository divulgacao = divulgadoRepository ?? RepositorioComDivulgacao(processoId);
        return ObterFormularioRenderizavelQueryHandler.Handle(
            new ObterFormularioRenderizavelQuery(processoId, finalidade),
            repository,
            divulgacao,
            RegistroReconhecendoVersaoCorrente,
            Enderecos(divulgacao),
            CancellationToken.None);
    }

    /// <summary>Repositório cuja linha de divulgação existe e cujo ato já teve os documentos copiados ao acervo — o processo É público.</summary>
    internal static ICertameDivulgadoRepository RepositorioComDivulgacao(Guid processoId)
    {
        CertameDivulgado divulgado = Divulgado(processoId);
        divulgado.RegistrarEnderecos(IdDoAtoDaDivulgacao, "https://acervo.teste/edital.pdf", []);
        return RepositorioDe(divulgado);
    }

    internal static CertameDivulgado Divulgado(Guid processoId)
    {
        CertameDivulgado divulgado = CertameDivulgado.Criar(
            processoId,
            numeroVersao: 1,
            IdDoAtoDaDivulgacao,
            new string('a', 64),
            versaoProjecao: "1",
            new FacetasDoCertameDivulgado("certame-formulario", "Certame", "001/2026", ["AC"], DataHoraFixa, DataHoraFixa.AddDays(30)),
            """{"nome":"documento"}""",
            DataHoraFixa);
        divulgado.AbrirEntradaDoAto(IdDoAtoDaDivulgacao, 1);
        return divulgado;
    }

    internal static ICertameDivulgadoRepository RepositorioDe(CertameDivulgado divulgado)
    {
        ICertameDivulgadoRepository repositorio = Substitute.For<ICertameDivulgadoRepository>();
        repositorio.ObterParaLeituraAsync(divulgado.Id, Arg.Any<CancellationToken>()).Returns(divulgado);
        return repositorio;
    }

    internal static readonly DateTimeOffset DataHoraFixa = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    internal static IRegistroCodecsEnvelope CriarRegistroReconhecendo(params string[] schemaVersionsReconhecidas)
    {
        IRegistroCodecsEnvelope registro = Substitute.For<IRegistroCodecsEnvelope>();
        registro.LeAVersao(Arg.Is<string>(s => schemaVersionsReconhecidas.Contains(s))).Returns(true);
        registro.Reidratar(Arg.Any<VersaoConfiguracao>()).Returns(Result<EnvelopeReidratado>.Success(EnvelopeCom()));
        return registro;
    }

    internal static IRegistroCodecsEnvelope RegistroReidratando(EnvelopeReidratado envelope)
    {
        IRegistroCodecsEnvelope registro = CriarRegistroReconhecendo(VersaoCorrenteReconhecida);
        registro.Reidratar(Arg.Any<VersaoConfiguracao>()).Returns(Result<EnvelopeReidratado>.Success(envelope));
        return registro;
    }

    /// <summary>
    /// A reidratação de um processo com os formulários dados, sobre a estrutura de um processo
    /// conforme. Sem formulários, nenhuma finalidade tem o que servir.
    /// </summary>
    internal static EnvelopeReidratado EnvelopeCom(params FormularioProcesso[] formularios)
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS 2026 — SiSU");
        return new EnvelopeReidratado(
            new ConfiguracaoPublicada(
            new CronogramaDeclarado(ConfiguracaoPublicadaDeTeste.Localidade, null, [.. processo.CronogramaFases]),
            new AvaliacaoDeclarada([.. processo.Etapas], [.. processo.CriteriosDesempate], processo.Classificacao!),
            new VagasDeclaradas([.. processo.DistribuicaoVagas], 1, processo.BonusRegional is not null,
                processo.BonusRegional, null, processo.AtendimentoEspecializado!),
            new ExigenciasDeclaradas([.. processo.DocumentosExigidos], []),
            new ColetaDeclarada([], [], formularios, [],
                [], [], null),
            new DivulgacaoETaxaDeclaradas(null, null), null),
            DadosEdital.Criar(
                "001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), Guid.CreateVersion7()).Value!,
            new string('a', 64), "America/Sao_Paulo", retificacao: null, conformidade: null, new DadosDeCatalogoDaConfiguracaoPublicada());
    }

    /// <summary>Um formulário só com blocos: o de comprovação documental, quando pedido, e o de revisão e aceite.</summary>
    internal static FormularioProcesso Formulario(FinalidadeFormulario finalidade, Guid? faseId, bool comComprovacao) =>
        FormularioProcesso.Criar(
            finalidade, faseId, null,
            [
                .. comComprovacao
                    ? [EtapaFormulario.Criar(new EtapaEstrutura("DOCUMENTOS", 0, TipoEtapaFormulario.Bloco, BlocoSistema.ComprovacaoDocumental), "Documentos", null, null).Value!]
                    : Array.Empty<EtapaFormulario>(),
                EtapaFormulario.Criar(new EtapaEstrutura("REVISAO", 1, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite), "Revisão e aceite", null, null).Value!,
            ]).Value!;

    internal static Task<Result<FormularioRenderizavelDto>> HandleAsync(
        Guid processoId, string envelopeJson, EnvelopeReidratado reidratado, FinalidadeFormulario finalidade,
        ICertameDivulgadoRepository? divulgacao = null)
    {
        ICertameDivulgadoRepository lida = divulgacao ?? RepositorioComDivulgacao(processoId);
        return ObterFormularioRenderizavelQueryHandler.Handle(
            new ObterFormularioRenderizavelQuery(processoId, finalidade),
            MockComVersaoVigente(processoId, envelopeJson),
            lida,
            RegistroReidratando(reidratado),
            Enderecos(lida),
            CancellationToken.None);
    }

    internal static IProcessoSeletivoRepository MockComVersaoVigente(
        Guid processoId, string envelopeJson, string schemaVersion = VersaoCorrenteReconhecida)
    {
        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(
            processoId, new DocumentoCongelado(Encoding.UTF8.GetBytes(envelopeJson), schemaVersion, "canonical-json/sha256@v1", new string('a', 64)),
            Guid.CreateVersion7(), "user-conta-123", Relogio.GetUtcNow());

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterVersoesPorAtoCriadorAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([versao]);
        return repository;
    }
}
