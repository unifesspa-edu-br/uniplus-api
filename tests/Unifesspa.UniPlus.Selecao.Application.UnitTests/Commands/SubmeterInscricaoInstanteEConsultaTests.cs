namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.SubmeterDeTeste;

/// <summary>
/// O instante do envio e a consulta depois dele: o dia de que a faixa de data se conta e a residência gravada.
/// </summary>
public sealed class SubmeterInscricaoInstanteEConsultaTests
{
    [Fact(DisplayName = "O relógio que vira o dia durante o envio não muda o dia de que a faixa de data se conta")]
    public async Task Handle_ViradaDoDiaDuranteOEnvio_AvaliaNoDiaDoInstanteUnico()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo([DataAteOEnvio]);
        Inscricao existente = NovaInscricao(cpf, "conta-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        // 23:59:59,9 de 15/01 em Belém; cada leitura seguinte do relógio já cai em 16/01.
        Result<ReciboDaInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, RequestComDataDoEvento(cpf, termo, grafo, "2026-01-16"), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, unitOfWork, UsuarioComCpf(cpf),
            new PortasDoEnvioDeTeste(Relogio: new RelogioQueAvanca(new DateTimeOffset(2026, 1, 16, 2, 59, 59, 900, TimeSpan.Zero), TimeSpan.FromMilliseconds(200))));

        resultado.Errors.Should().ContainSingle(static e => e.Field == "DATA_DO_EVENTO" && e.Error.Code == InscricaoErrorCodes.DataForaDaFaixa)
            .Which.Error.Message.Should().Be("Informe uma data até 15/01/2026.");
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O comprovante leva o mesmo instante com que o envio foi avaliado")]
    public async Task Handle_Envio_ComprovanteLevaOInstanteDaAvaliacao()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo([DataAteOEnvio]);
        Inscricao existente = NovaInscricao(cpf, "conta-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        Result<ReciboDaInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, RequestComDataDoEvento(cpf, termo, grafo, "2026-01-15"), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, Substitute.For<IUnitOfWork>(), UsuarioComCpf(cpf),
            new PortasDoEnvioDeTeste(Relogio: new RelogioQueAvanca(new DateTimeOffset(2026, 1, 16, 2, 59, 59, 900, TimeSpan.Zero), TimeSpan.FromMilliseconds(200))));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Instante.Should().Be(new DateTimeOffset(2026, 1, 16, 2, 59, 59, 900, TimeSpan.Zero));
    }

    [Fact(DisplayName = "A correção da inscrição enviada conta a faixa de data do instante da correção")]
    public async Task Handle_Correcao_ContaAFaixaDoNovoEnvio()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo([DataAteOEnvio]);
        Inscricao existente = NovaInscricao(cpf, "conta-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        Result<ReciboDaInscricaoDto> envio = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, RequestComDataDoEvento(cpf, termo, grafo, "2026-01-15"), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, Substitute.For<IUnitOfWork>(), UsuarioComCpf(cpf),
            new PortasDoEnvioDeTeste(Relogio: new RelogioFixo(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero))));
        envio.IsSuccess.Should().BeTrue(envio.Error?.Message);

        Result<ReciboDaInscricaoDto> correcao = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, RequestComDataDoEvento(cpf, termo, grafo, "2026-01-16"), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, Substitute.For<IUnitOfWork>(), UsuarioComCpf(cpf),
            new PortasDoEnvioDeTeste(Relogio: new RelogioFixo(new DateTimeOffset(2026, 1, 16, 12, 0, 0, TimeSpan.Zero))));

        correcao.IsSuccess.Should().BeTrue(correcao.Error?.Message);
    }

    [Fact(DisplayName = "Regra sobre o subitem de município de um grupo tem o mesmo veredito no envio e na consulta depois do envio, com os grupos vindos do gravado")]
    public async Task Consulta_RegraSobreMunicipioDeGrupo_TemOMesmoVeredito()
    {
        FatoColetado soEmBelem = FatoColetado.Criar(
            "OBSERVACAO_DE_BELEM", 1, "Observação", TipoRenderizacao.Texto, Obrigatoriedade.Nunca,
            [CondicaoPrecondicaoFato.Criar(0, "LOCAL_MUNICIPIO", Operador.Igual, JsonSerializer.SerializeToElement(Belem)).Value!],
            classificacaoProtecao: "PESSOAL", formato: "LIVRE", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal,
            finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;
        GrupoColetado locais = GrupoColetado.Criar(
            "LOCAIS", 1, FormularioDeTeste.Secao, "Locais", minimo: 0, maximo: null, exibicao: null, Obrigatoriedade.Nunca,
            [GrupoMunicipal().Subitens.First(), soEmBelem], FinalidadeFormulario.Inscricao).Value!;
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo(grupos: [locais]);
        Inscricao existente = NovaInscricao(cpf, "conta-1", versao.Id);
        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);
        InscricaoRequest pedido = RequestComApelidoETermoAceito(cpf, termo, grafo) with
        {
            Grupos = new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>>(StringComparer.Ordinal)
            {
                ["LOCAIS"] = [new("local-1", Texto(("LOCAL_MUNICIPIO", Belem), ("OBSERVACAO_DE_BELEM", "Perto do porto")))],
            },
        };

        Result<ReciboDaInscricaoDto> enviada = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, pedido, PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, Substitute.For<IUnitOfWork>(), UsuarioComCpf(cpf), GeoQueConhece());

        enviada.IsSuccess.Should().BeTrue(enviada.Error?.Message);
        existente.Grupos["LOCAIS"].Single().Respostas.Should().ContainKey("OBSERVACAO_DE_BELEM", "no envio a regra viu o município pelo código");
        Result<Unifesspa.UniPlus.Selecao.Application.Services.ContextoDaVersaoFixada> contexto = await Unifesspa.UniPlus.Selecao.Application.Services.ContextoDaVersaoFixadaResolver
            .ResolverAsync(existente.VersaoFormularioId, ProcessoId, processoRepository, registroCodecs, existente.OfertaCursoId, existente.OfertaCursoSegundaOpcaoId, CancellationToken.None);
        Result<ResultadoDaAvaliacaoDaInscricao> consulta = await Unifesspa.UniPlus.Selecao.Application.Services.PreparacaoDaAvaliacaoDaInscricao.AvaliarAsync(
            existente, contexto.Value!, new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            new Dictionary<string, IReadOnlyList<OcorrenciaRespondida>>(StringComparer.Ordinal),
            new EnvioDasRespostas(DivulgacaoDeTeste.Relogio.GetUtcNow(), TimeZoneInfo.Utc), GeoQueConhece().Indisponivel(),
            await Unifesspa.UniPlus.Selecao.Application.Services.ArquivosDaInscricao.CarregarAsync(Substitute.For<IDocumentoDaInscricaoRepository>(), existente.Id, CancellationToken.None),
            CancellationToken.None);

        consulta.IsSuccess.Should().BeTrue(consulta.Error?.Message);
        consulta.Value!.Formulario.Grupos.Single().Ocorrencias.Single().Itens.Single(static i => i.FatoCodigo == "OBSERVACAO_DE_BELEM").Visivel
            .Should().Be(Ternario.Verdadeiro, "na consulta o grupo vem do gravado, com o município como objeto, e a regra o vê pelo código");
    }

    [Fact(DisplayName = "Depois do envio, a consulta usa a residência gravada no envio: a exigência regional tem o mesmo veredito com o Geo fora do ar")]
    public async Task Consulta_DepoisDoEnvio_UsaAResidenciaGravadaSemOGeo()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        DocumentoExigido regional = null!;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo(exigencias: processo =>
        {
            Guid fase = processo.CronogramaFases.First(static f => f.ColetaInscricao).Id;
            regional = DocumentoExigido.Criar(
                fase, Guid.CreateVersion7(), "REGIONAL", "Regional", "CAT", ProtecaoDoDocumentoDeTeste.Pessoal, Aplicabilidade.Condicional, obrigatorio: false, null,
                [CondicaoGatilho.Criar(0, "MUNICIPIO_RESIDENCIA", Operador.Igual, JsonSerializer.SerializeToElement(Belem)).Value!], [], null,
                FormatosPermitidos.Criar(true, null).Value!, null, finalidade: FinalidadeFormulario.Inscricao).Value!;
            return ([regional], [NoExigencia.CriarFolha(regional, 0).Value!]);
        });
        Inscricao existente = NovaInscricao(cpf, "conta-1", versao.Id);
        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        Result<ReciboDaInscricaoDto> enviada = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, RequestComApelidoETermoAceito(cpf, termo, grafo), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, Substitute.For<IUnitOfWork>(), UsuarioComCpf(cpf), GeoQueConhece());
        enviada.IsSuccess.Should().BeTrue(enviada.Error?.Message);

        Result<Unifesspa.UniPlus.Selecao.Application.Services.ContextoDaVersaoFixada> contexto = await Unifesspa.UniPlus.Selecao.Application.Services.ContextoDaVersaoFixadaResolver
            .ResolverAsync(existente.VersaoFormularioId, ProcessoId, processoRepository, registroCodecs, existente.OfertaCursoId, existente.OfertaCursoSegundaOpcaoId, CancellationToken.None);
        Result<ResultadoDaAvaliacaoDaInscricao> consulta = await Unifesspa.UniPlus.Selecao.Application.Services.PreparacaoDaAvaliacaoDaInscricao.AvaliarAsync(
            existente, contexto.Value!, new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            new Dictionary<string, IReadOnlyList<OcorrenciaRespondida>>(StringComparer.Ordinal),
            new EnvioDasRespostas(DivulgacaoDeTeste.Relogio.GetUtcNow(), TimeZoneInfo.Utc),
            GeoQueConhece().Indisponivel(),
            await Unifesspa.UniPlus.Selecao.Application.Services.ArquivosDaInscricao.CarregarAsync(Substitute.For<IDocumentoDaInscricaoRepository>(), existente.Id, CancellationToken.None),
            CancellationToken.None);

        consulta.IsSuccess.Should().BeTrue(consulta.Error?.Message);
        existente.Derivados["MUNICIPIO_RESIDENCIA"].GetRawText().Should().Be(ValorDeMunicipio.Gravado(Belem, "Belém").GetRawText());
        consulta.Value!.Documentos.Itens.Single().Situacao.Should().Be(SituacaoDoDocumento.Pendente);
    }

    [Fact(DisplayName = "Data de nascimento mudada na consulta: a faixa etária sai da data nova e a residência continua a gravada no envio, sem Geo")]
    public async Task Consulta_DataDeNascimentoMudada_RecalculaAFaixaEtariaESegueComAResidenciaGravada()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        DocumentoExigido aos26 = null!, aos16 = null!, deBelem = null!;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo(exigencias: processo =>
        {
            Guid fase = processo.CronogramaFases.First(static f => f.ColetaInscricao).Id;
            DocumentoExigido Doc(string nome, string fato, JsonElement valor) => DocumentoExigido.Criar(
                fase, Guid.CreateVersion7(), nome.ToUpperInvariant(), nome, "CAT", ProtecaoDoDocumentoDeTeste.Pessoal, Aplicabilidade.Condicional, obrigatorio: false, null,
                [CondicaoGatilho.Criar(0, fato, Operador.Igual, valor).Value!], [], null,
                FormatosPermitidos.Criar(true, null).Value!, null, finalidade: FinalidadeFormulario.Inscricao).Value!;
            aos26 = Doc("Aos26", "FAIXA_ETARIA", JsonSerializer.SerializeToElement(26));
            aos16 = Doc("Aos16", "FAIXA_ETARIA", JsonSerializer.SerializeToElement(16));
            deBelem = Doc("DeBelem", "MUNICIPIO_RESIDENCIA", JsonSerializer.SerializeToElement(Belem));
            return ([aos26, aos16, deBelem], [NoExigencia.CriarFolha(aos26, 0).Value!, NoExigencia.CriarFolha(aos16, 1).Value!, NoExigencia.CriarFolha(deBelem, 2).Value!]);
        });
        Inscricao existente = NovaInscricao(cpf, "conta-1", versao.Id);
        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);
        (await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, RequestComApelidoETermoAceito(cpf, termo, grafo), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, Substitute.For<IUnitOfWork>(), UsuarioComCpf(cpf), GeoQueConhece()))
            .IsSuccess.Should().BeTrue();

        Result<Unifesspa.UniPlus.Selecao.Application.Services.ContextoDaVersaoFixada> contexto = await Unifesspa.UniPlus.Selecao.Application.Services.ContextoDaVersaoFixadaResolver
            .ResolverAsync(existente.VersaoFormularioId, ProcessoId, processoRepository, registroCodecs, existente.OfertaCursoId, existente.OfertaCursoSegundaOpcaoId, CancellationToken.None);
        Result<ResultadoDaAvaliacaoDaInscricao> consulta = await Unifesspa.UniPlus.Selecao.Application.Services.PreparacaoDaAvaliacaoDaInscricao.AvaliarAsync(
            existente, contexto.Value!, Texto(("DATA_NASCIMENTO", "2010-01-01")),
            new Dictionary<string, IReadOnlyList<OcorrenciaRespondida>>(StringComparer.Ordinal),
            new EnvioDasRespostas(DivulgacaoDeTeste.Relogio.GetUtcNow(), TimeZoneInfo.Utc), GeoQueConhece().Indisponivel(),
            await Unifesspa.UniPlus.Selecao.Application.Services.ArquivosDaInscricao.CarregarAsync(Substitute.For<IDocumentoDaInscricaoRepository>(), existente.Id, CancellationToken.None),
            CancellationToken.None);

        consulta.IsSuccess.Should().BeTrue(consulta.Error?.Message);
        Dictionary<Guid, SituacaoDoDocumento> situacoes = consulta.Value!.Documentos.Itens.ToDictionary(static d => d.Documento.Id, static d => d.Situacao);
        situacoes[aos16.Id].Should().Be(SituacaoDoDocumento.Pendente, "a faixa etária vem da data de nascimento do corpo");
        situacoes[aos26.Id].Should().NotBe(SituacaoDoDocumento.Pendente);
        situacoes[deBelem.Id].Should().Be(SituacaoDoDocumento.Pendente, "a residência vem da gravada no envio");
    }

    [Fact(DisplayName = "Corrigir a inscrição sem reenviar a naturalidade confere o município gravado de novo e regrava o nome")]
    public async Task Handle_SegundoEnvioSemANaturalidade_RegravaONome()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo();
        Inscricao existente = NovaInscricao(cpf, "conta-1", versao.Id);
        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);
        (await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, RequestComApelidoETermoAceito(cpf, termo, grafo), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, Substitute.For<IUnitOfWork>(), UsuarioComCpf(cpf), GeoQueConhece()))
            .IsSuccess.Should().BeTrue();

        InscricaoRequest comNovoApelido = RequestComApelidoETermoAceito(cpf, termo, grafo, apelido: "Nandinha");
        InscricaoRequest correcao = comNovoApelido with
        {
            Respostas = comNovoApelido.Respostas!.Where(static r => r.Key != "NATURALIDADE_MUNICIPIO").ToDictionary(static r => r.Key, static r => r.Value, StringComparer.Ordinal),
        };
        FakeConferenciaEnderecoGeo geo = GeoQueConhece();

        Result<ReciboDaInscricaoDto> corrigida = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, correcao, PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, Substitute.For<IUnitOfWork>(), UsuarioComCpf(cpf), geo);

        corrigida.IsSuccess.Should().BeTrue(corrigida.Error?.Message);
        existente.Respostas["NATURALIDADE_MUNICIPIO"].GetRawText().Should().Be(ValorDeMunicipio.Gravado(Belem, "Belém").GetRawText());
    }
}
