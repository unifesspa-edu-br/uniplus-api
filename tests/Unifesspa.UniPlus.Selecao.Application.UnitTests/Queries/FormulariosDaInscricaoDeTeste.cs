namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A montagem que os testes dos formulários da inscrição compartilham: uma versão cujo congelado declara
/// formulários na fase de inscrição, o codec que a reidrata e as portas da inscrição do candidato.
/// </summary>
internal static class FormulariosDaInscricaoDeTeste
{
    internal static readonly Guid ProcessoId = Guid.CreateVersion7();
    internal const string SchemaVersion = "0.0.22";

    internal sealed record FormularioDaVersao(string Codigo, bool Obrigatorio);

    internal static IUserContext UsuarioComCpf(Cpf cpf, string contaId = "conta-1")
    {
        IUserContext contexto = Substitute.For<IUserContext>();
        contexto.Cpf.Returns(cpf.Valor);
        contexto.UserId.Returns(contaId);
        return contexto;
    }

    /// <param name="comBlocoDeComprovacao">O formulário ganha o bloco de comprovação documental; sem ele, só tem a revisão.</param>
    /// <param name="comExigenciaCondicional">A versão congela uma exigência condicional na fase do formulário.</param>
    /// <param name="formularios">Os formulários que a versão declara na fase que coleta inscrição, na ordem dada; sem eles, só o de inscrição.</param>
    internal static (VersaoConfiguracao Versao, EnvelopeReidratado Envelope, Guid FaseDaInscricao) MontarVersaoComFormularioDeInscricao(
        bool comBlocoDeComprovacao = false, bool comExigenciaCondicional = false, IReadOnlyList<FormularioDaVersao>? formularios = null)
    {
        ProcessoSeletivo processoBase = ProcessoSeletivoConformeBuilder.Criar("PS 2026 — Formulário da inscrição", out Guid faseDaInscricao);
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do(processoBase.CronogramaFases);
        IReadOnlyList<FormularioDaVersao> declarados = formularios ?? [new(FormularioDeTeste.CodigoDaInscricao, Obrigatorio: true)];
        EnvelopeReidratado envelope = new(
            ConfiguracaoComFormularios(processoBase, [.. declarados.Select(f =>
                FormularioDaVersaoNaFase(f, faseDaInscricao, cronograma, comBlocoDeComprovacao))]),
            DadosEdital.Criar(
                "001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), Guid.CreateVersion7()).Value!,
            new string('a', 64), "America/Belem", retificacao: null, conformidade: null, dadosDeCatalogo: new DadosDeCatalogoDaConfiguracaoPublicada());

        return (VersaoComExigencias(comExigenciaCondicional ? ExigenciaCondicionalDeComprovante(faseDaInscricao) : string.Empty), envelope, faseDaInscricao);
    }

    internal static VersaoConfiguracao VersaoComDocumento(string documento) => VersaoConfiguracao.Abrir(
        ProcessoId,
        new DocumentoCongelado(Encoding.UTF8.GetBytes(documento), SchemaVersion, "canonical-json/sha256@v1", new string('a', 64)),
        Guid.CreateVersion7(), "user-conta-1", new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));

    internal static IRegistroCodecsEnvelope RegistroReidratando(EnvelopeReidratado envelope)
    {
        IRegistroCodecsEnvelope registro = Substitute.For<IRegistroCodecsEnvelope>();
        registro.LeAVersao(SchemaVersion).Returns(true);
        registro.Reidratar(Arg.Any<VersaoConfiguracao>()).Returns(Result<EnvelopeReidratado>.Success(envelope));
        return registro;
    }

    internal static Task<Result<IReadOnlyList<FormularioApresentadoDoProcessoDto>>> Obter(PortasDaInscricaoDeTeste portas, IUserContext usuario) =>
        ObterFormulariosDaInscricaoQueryHandler.Handle(
            new ObterFormulariosDaInscricaoQuery(ProcessoId), new InscricaoDoDono(portas.Inscricoes, usuario),
            new VersaoDaInscricaoResolver(DivulgacaoDeTeste.Certame(ProcessoId), portas.Processos, portas.Codecs),
            portas.Codecs, CancellationToken.None);

    internal static Inscricao Rascunho(Cpf cpf, VersaoConfiguracao versao) =>
        Inscricao.Iniciar(ProcessoId, cpf, "conta-1", "https://issuer", versao.Id).Value!;

    internal static PortasDaInscricaoDeTeste PortasNaVersao(Cpf cpf, Inscricao inscricao, VersaoConfiguracao versao, EnvelopeReidratado envelope) =>
        PortasNaVersao(cpf, inscricao, versao, RegistroReidratando(envelope));

    internal static PortasDaInscricaoDeTeste PortasNaVersao(Cpf cpf, Inscricao inscricao, VersaoConfiguracao versao, IRegistroCodecsEnvelope codecs)
    {
        PortasDaInscricaoDeTeste portas = new(ProcessoId) { Codecs = codecs };
        portas.ComInscricao(cpf, inscricao);
        portas.Processos.ObterVersoesAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns([versao]);
        portas.Processos.ObterVersoesPorAtoCriadorAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([versao]);
        return portas;
    }

    private static FormularioProcesso FormularioDaVersaoNaFase(
        FormularioDaVersao declarado, Guid faseDaInscricao, CronogramaDoProcesso cronograma, bool comBlocoDeComprovacao) => FormularioProcesso.Criar(
        new FormularioDeclarado(CodigoDoFormulario.Criar(declarado.Codigo).Value!, faseDaInscricao, declarado.Obrigatorio), cronograma, null,
        [
            .. comBlocoDeComprovacao
                ? [PaginaDoFormulario.Criar(new PaginaEstrutura("DOCUMENTOS", 0, TipoPaginaFormulario.Bloco, BlocoSistema.ComprovacaoDocumental), "Documentos", null, null).Value!]
                : Array.Empty<PaginaDoFormulario>(),
            PaginaDoFormulario.Criar(new PaginaEstrutura("REVISAO", 1, TipoPaginaFormulario.Bloco, BlocoSistema.RevisaoEAceite), "Revisão e aceite", null, null).Value!,
        ]).Value!;

    private static ConfiguracaoPublicada ConfiguracaoComFormularios(ProcessoSeletivo processoBase, IReadOnlyList<FormularioProcesso> formularios) => new ConfiguracaoPublicada(
            new CronogramaDeclarado(ConfiguracaoPublicadaDeTeste.Localidade, null, [.. processoBase.CronogramaFases]),
            new AvaliacaoDeclarada([.. processoBase.Etapas], [.. processoBase.CriteriosDesempate], processoBase.Classificacao!),
            new VagasDeclaradas([.. processoBase.DistribuicaoVagas], 1, processoBase.Fada is not null,
                processoBase.Fada, null, processoBase.AtendimentoEspecializado!),
            new ExigenciasDeclaradas([.. processoBase.DocumentosExigidos], []),
            new ColetaDeclarada([], [], [.. formularios], [],
                [], [], null),
            new DivulgacaoETaxaDeclaradas(null, null), null);

    private static VersaoConfiguracao VersaoComExigencias(string exigencias) =>
        VersaoComDocumento($$$"""{"documentosExigidos": {"dataDeReferenciaDosDados": null, "exigencias": [{{{exigencias}}}]}}""");

    private static string ExigenciaCondicionalDeComprovante(Guid faseDaInscricao) =>
        $$"""
        {"tipoDocumentoNome": "Comprovante de residência", "aplicabilidade": "CONDICIONAL", "obrigatorio": true, "exigidoNaFaseId": "{{faseDaInscricao}}",
         "formularioCodigo": "INSCRICAO", "formatosPermitidos": {"lista": null, "qualquer": true}, "modelo": null}
        """;
}
